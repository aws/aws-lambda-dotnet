/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.ExceptionHandling;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.Serializers;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    /// <summary>
    /// Builds user delegate from the handler information.
    /// </summary>
    internal class InvokeDelegateBuilder
    {
        private readonly InternalLogger _logger;
        private readonly HandlerInfo _handler;
        private readonly MethodInfo _customerMethodInfo;
        private Type CustomerOutputType { get; set; }

        public InvokeDelegateBuilder(InternalLogger logger, HandlerInfo handler, MethodInfo customerMethodInfo)
        {
            _logger = logger;
            _handler = handler;
            _customerMethodInfo = customerMethodInfo;
        }

        /// <summary>
        /// Constructs the invoke delegate using Expressions
        ///
        /// Serialize & Deserialize calls are only made when a serializer is provided.
        /// Context is only passed when customer method has context parameter.
        /// Lambda return type can be void.
        ///
        /// (inStream, context, outStream) =>
        /// {
        ///     var input = serializer.Deserialize(inStream);
        ///     var output = handler(input, context);
        ///     return serializer.Serialize(output);
        /// }
        ///
        /// </summary>
        /// <param name="customerObject">Wrapped customer object.</param>
        /// <param name="customerSerializerInstance">Instance of lambda input & output serializer.</param>
        /// <returns>Action delegate pointing to customer's handler.</returns>
        public Action<Stream, ILambdaContext, Stream> ConstructInvokeDelegate(object customerObject, object customerSerializerInstance, bool isPreJit)
        {
            var inStreamParameter = Expression.Parameter(Types.StreamType, "inStream");
            var outStreamParameter = Expression.Parameter(Types.StreamType, "outStream");
            var contextParameter = Expression.Parameter(typeof(ILambdaContext), "context");

            _logger.LogDebug($"UCL : Constructing input expression");
            var inputExpression = BuildInputExpressionOrNull(customerSerializerInstance, inStreamParameter, out var iLambdaContextType);
            if (isPreJit)
            {
                _logger.LogInformation("PreJit: inputExpression");
                UserCodeInit.InitDeserializationAssembly(inputExpression, inStreamParameter);
            }

            _logger.LogDebug($"UCL : Constructing context expression");
            var contextExpression = BuildContextExpressionOrNull(iLambdaContextType, contextParameter);

            _logger.LogDebug($"UCL : Constructing handler expression");
            var handlerExpression = CreateHandlerCallExpression(customerObject, inputExpression, contextExpression);

            _logger.LogDebug($"UCL : Constructing output expression");
            var outputExpression = CreateOutputExpression(customerSerializerInstance, outStreamParameter, handlerExpression);
            if (isPreJit)
            {
                _logger.LogInformation("PreJit: outputExpression");
                UserCodeInit.InitSerializationAssembly(outputExpression, outStreamParameter, CustomerOutputType);
            }

            _logger.LogDebug($"UCL : Constructing final expression");

            var finalExpression = Expression.Lambda<Action<Stream, ILambdaContext, Stream>>(outputExpression, inStreamParameter, contextParameter, outStreamParameter);
#if DEBUG
            var finalExpressionDebugView = typeof(Expression)
                .GetTypeInfo()
                .GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(finalExpression);
            _logger.LogDebug($"UCL : Constructed final expression:\n'{finalExpressionDebugView}'");
#endif

            _logger.LogDebug($"UCL : Compiling final expression");
            return finalExpression.Compile();
        }

        /// <summary>
        /// Creates an expression to convert incoming Stream to the customer method inputs.
        /// If customer method takes no inputs or only takes ILambdaContext, return null.
        /// </summary>
        /// <param name="customerSerializerInstance">Instance of lambda input & output serializer.</param>
        /// <param name="inStreamParameter">Input stream parameter.</param>
        /// <param name="iLambdaContextType">Type of context passed for the invocation.</param>
        /// <returns>Expression that deserializes incoming stream to the customer method inputs or null if customer method takes no input.</returns>
        /// <exception cref="LambdaValidationException">Thrown when customer method inputs don't meet lambda requirements.</exception>
        private Expression BuildInputExpressionOrNull(object customerSerializerInstance, Expression inStreamParameter, out Type iLambdaContextType)
        {
            Type inputType = null;
            iLambdaContextType = null;

            // get input types
            var inputTypes = _customerMethodInfo.GetParameters().Select(pi => pi.ParameterType).ToArray();

            // check if there are too many parameters
            if (inputTypes.Length > 2)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.MethodTooManyParams,
                    _customerMethodInfo.Name, _handler.TypeName);
            }

            // if two parameters, check that the second input is ILambdaContext
            if (inputTypes.Length == 2)
            {
                if (!Types.IsILambdaContext(inputTypes[1]))
                {
                    throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.MethodSecondParamNotContext,
                        _customerMethodInfo.Name, _handler.TypeName, Types.ILambdaContextTypeName);
                }

                iLambdaContextType = inputTypes[1];
                inputType = inputTypes[0];
            }

            // if one input, check if input is ILambdaContext
            else if (inputTypes.Length == 1)
            {
                if (Types.IsILambdaContext(inputTypes[0]))
                {
                    iLambdaContextType = inputTypes[0];
                }
                else
                {
                    inputType = inputTypes[0];
                }
            }

            if (inputType != null)
            {
                // deserializer.Deserialize(inStream)
                return CreateDeserializeExpression(customerSerializerInstance, inputType, inStreamParameter);
            }

            if (iLambdaContextType != null)
            {
                _logger.LogDebug($"UCL : Validating iLambdaContextType");
                UserCodeValidator.ValidateILambdaContextType(iLambdaContextType);
            }

            return null;
        }

        /// <summary>
        /// Generates an expression if iLambdaContextType is not null, or returns null
        /// </summary>
        /// <param name="iLambdaContextType">Type of context passed for the invocation.</param>
        /// <param name="contextParameter">Expression that defines context parameter.</param>
        /// <returns>Expression that defines context parameter if iLambdaContextType is not null, or returns null</returns>
        private static Expression BuildContextExpressionOrNull(Type iLambdaContextType, Expression contextParameter)
        {
            // If the Lambda function does not have a context parameter, don't build
            // an expression to construct it.
            return iLambdaContextType == null ? null : contextParameter;
        }

        /// <summary>
        /// Creates expression to invoke the customer method.
        /// If input or context expressions are not null, those expressions are
        /// passed into the method.
        /// </summary>
        /// <param name="customerObject">Wrapped customer object.</param>
        /// <param name="inputExpression">Input expression that defines customer input.</param>
        /// <param name="contextExpression">Context expression that defines context passed for the invocation.</param>
        /// <returns>Expression that unwraps customer object.</returns>
        private Expression CreateHandlerCallExpression(object customerObject, Expression inputExpression, Expression contextExpression)
        {
            Expression customerObjectConstant = null;
            if (customerObject != null)
            {
                // code: [customerObject]
                customerObjectConstant = Expression.Constant(customerObject);
            }

            var inputs = new List<Expression>();
            if (inputExpression != null)
            {
                inputs.Add(inputExpression);
            }

            if (contextExpression != null)
            {
                inputs.Add(contextExpression);
            }

            // [customerObject].handler([input|context|input, context])
            Expression handlerCallExpression = Expression.Call(customerObjectConstant, _customerMethodInfo, inputs);

            var outputType = _customerMethodInfo.ReturnType;
            var taskTType = GetTaskTSubclassOrNull(outputType);
            var outputIsTaskT = taskTType != null;
            var outputIsTask = Types.TaskType.GetTypeInfo().IsAssignableFrom(outputType);

            if (outputIsTaskT)
            {
                // code: (Task<T>)[customerObject].handler(...)
                var handlerConvert = Expression.Convert(handlerCallExpression, taskTType);

                // code: [customerObject].handler(...).GetAwaiter().GetResult()
                handlerCallExpression = Expression.Call(Expression.Call(handlerConvert, taskTType.GetMethod("GetAwaiter"), null), "GetResult", null);
            }
            else if (outputIsTask)
            {
                // code: (Task)([customerObject].handler(...))
                var handlerConvert = Expression.Convert(handlerCallExpression, Types.TaskType);

                // code: [customerObject].handler(...).GetAwaiter().GetResult()
                handlerCallExpression = Expression.Call(Expression.Call(handlerConvert, Types.TaskType.GetMethod("GetAwaiter"), null), "GetResult", null);
            }

            return handlerCallExpression;
        }

        /// <summary>
        /// Creates the final expression. If there is no output data, the final expression
        /// is just the handler call expression. Otherwise, the final expression is the
        /// serialization  expression operating on the handler call expression.
        /// </summary>
        /// <param name="customerSerializerInstance">Instance of lambda input & output serializer.</param>
        /// <param name="outStreamParameter">Expression that defines customer output.</param>
        /// <param name="handlerCallExpression">Expression that defines customer handler call.</param>
        /// <returns>Expression that serializes customer method output to outgoing stream.</returns>
        private Expression CreateOutputExpression(object customerSerializerInstance, Expression outStreamParameter, Expression handlerCallExpression)
        {
            var outputType = _customerMethodInfo.ReturnType;
            var taskTType = GetTaskTSubclassOrNull(outputType);
            var isTaskT = taskTType != null;
            var isTask = Types.TaskType.GetTypeInfo().IsAssignableFrom(outputType);
            var isVoid = outputType == Types.VoidType;

            var hasData = (!isVoid && !isTask) || isTaskT;
            if (hasData)
            {
                if (isTaskT)
                {
                    outputType = taskTType.GenericTypeArguments[0];
                }

                // serializer.Serialize(outputData, outStream)
                return CreateSerializeExpression(customerSerializerInstance, outputType, handlerCallExpression, outStreamParameter);
            }

            CustomerOutputType = outputType;
            return handlerCallExpression;
        }

        /// <summary>
        /// Retrieves the Task&lt;T&gt; type that the given type subclasses,
        /// or null if the type does not subclass Task&lt;T&gt;
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Type GetTaskTSubclassOrNull(Type type)
        {
            if (type == null)
                return null;

            if (type.IsConstructedGenericType)
            {
                var genericDefinition = type.GetGenericTypeDefinition();
                if (genericDefinition == Types.TaskTType)
                    return type;
            }

            var baseType = type.GetTypeInfo().BaseType;
            return GetTaskTSubclassOrNull(baseType);
        }

        /// <summary>
        /// Generates an expression to serialize customer method result into the output stream
        /// </summary>
        /// <param name="customerSerializerInstance">Instance of lambda input & output serializer.</param>
        /// <param name="dataType">Customer input type.</param>
        /// <param name="customerObject">Expression that define customer object.</param>
        /// <param name="outStreamParameter">Expression that defines customer output.</param>
        /// <returns>Expression that serializes returned object to output stream.</returns>
        /// <exception cref="LambdaValidationException">Thrown when customer input is serializable & serializer instance is null.</exception>
        private Expression CreateSerializeExpression(object customerSerializerInstance, Type dataType, Expression customerObject, Expression outStreamParameter)
        {
            // generic types, null for String and Stream converters
            Type[] genericTypes = null;
            var converter = customerSerializerInstance;
            Type iLambdaSerializerType = null;

            // subclasses of Stream are allowed as customer method output
            if (Types.StreamType.GetTypeInfo().IsAssignableFrom(dataType))
            {
                converter = StreamSerializer.Instance;
            }
            else
            {
                if (customerSerializerInstance == null)
                {
                    throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.SerializeMissingAttribute,
                        _handler.AssemblyName, _handler.MethodName, dataType.FullName);
                }

                iLambdaSerializerType = customerSerializerInstance
                    .GetType()
                    .GetTypeInfo()
                    .GetInterface(Types.ILambdaSerializerTypeName);
                genericTypes = new[] {dataType};
            }

            // code: serializer
            Expression converterExpression = Expression.Constant(converter);

            if (iLambdaSerializerType != null)
            {
                // code: (ILambdaSerializer)serializer
                converterExpression = Expression.Convert(converterExpression, iLambdaSerializerType);
            }

            // code: [(ILambdaSerializer)]serializer.Serialize[<T>](handler(...), outStream)
            Expression serializeCall = Expression.Call(
                converterExpression, // variable
                "Serialize", // method name
                genericTypes, // generic type
                customerObject, // arg1 - customer object
                outStreamParameter); // arg2 - out stream
            return serializeCall;
        }

        /// <summary>
        /// Generates an expression to deserialize incoming data to customer method input
        /// </summary>
        /// <param name="customerSerializerInstance">Instance of lambda input & output serializer.</param>
        /// <param name="dataType">Customer input type.</param>
        /// <param name="inStream">Input expression that defines customer input.</param>
        /// <returns>Expression that deserializes incoming data to customer method input.</returns>
        /// <exception cref="LambdaValidationException">Thrown when customer serializer doesn't match with expected serializer definition</exception>
        private Expression CreateDeserializeExpression(object customerSerializerInstance, Type dataType, Expression inStream)
        {
            // generic types, null for String and Stream converters
            Type[] genericTypes = null;
            var converter = customerSerializerInstance;

            if (dataType == Types.StreamType)
            {
                converter = StreamSerializer.Instance;
            }
            else
            {
                if (customerSerializerInstance == null)
                {
                    throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.DeserializeMissingAttribute,
                        _handler.AssemblyName, _handler.MethodName, dataType.FullName);
                }

                genericTypes = new[] {dataType};
            }

            // code: serializer
            var serializer = Expression.Constant(converter);
            // code: serializer.Deserializer[<T>](inStream)
            Expression deserializeCall = Expression.Call(
                serializer, // variable
                "Deserialize", // method name
                genericTypes, // generic type
                inStream); // arg1 - input stream
            return deserializeCall;
        }
    }
}