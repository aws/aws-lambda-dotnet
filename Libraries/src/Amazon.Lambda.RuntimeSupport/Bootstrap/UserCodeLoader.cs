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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.ExceptionHandling;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    /// <summary>
    /// Loads user code and prepares to invoke it.
    /// </summary>
    internal class UserCodeLoader
    {
        private const string UserInvokeException = "An exception occurred while invoking customer handler.";
        private const string LambdaLoggingActionFieldName = "_loggingAction";

        internal const string LambdaCoreAssemblyName = "Amazon.Lambda.Core";

        private readonly InternalLogger _logger;
        private readonly string _handlerString;
        private bool _customerLoggerSetUpComplete;
        private HandlerInfo _handler;
        private Action<Stream, ILambdaContext, Stream> _invokeDelegate;
        internal MethodInfo CustomerMethodInfo { get; private set; }

        /// <summary>
        /// Initializes UserCodeLoader with a given handler and internal logger.
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="logger"></param>
        public UserCodeLoader(string handler, InternalLogger logger)
        {
            if (string.IsNullOrEmpty(handler))
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _logger = logger;
            _handlerString = handler;
        }

        /// <summary>
        /// Loads customer assembly, type, and method.
        /// After this call returns without errors, it is possible to invoke
        /// the customer method through the Invoke method.
        /// </summary>
        public void Init(Action<string> customerLoggingAction)
        {
            Assembly customerAssembly = null;

            try
            {
                _logger.LogDebug($"UCL : Parsing handler string '{_handlerString}'");
                _handler = new HandlerInfo(_handlerString);

                // Set the logging action private field on the Amazon.Lambda.Core.LambdaLogger type which is part of the
                // public Amazon.Lambda.Core package when it is loaded.
                AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
                {
                    _logger.LogInformation($"UCL : Loaded assembly {args.LoadedAssembly.FullName} into default ALC.");
                    if (!_customerLoggerSetUpComplete && string.Equals(LambdaCoreAssemblyName, args.LoadedAssembly.GetName().Name, StringComparison.Ordinal))
                    {
                        _logger.LogDebug(
                            $"UCL : Load context loading '{LambdaCoreAssemblyName}', attempting to set {Types.LambdaLoggerTypeName}.{LambdaLoggingActionFieldName} to logging action.");
                        SetCustomerLoggerLogAction(args.LoadedAssembly, customerLoggingAction, _logger);
                        _customerLoggerSetUpComplete = true;
                    }
                };

                _logger.LogDebug($"UCL : Attempting to load assembly '{_handler.AssemblyName}'");
                customerAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(_handler.AssemblyName);
            }
            catch (FileNotFoundException fex)
            {
                _logger.LogError(fex, "An error occured on UCL Init");
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.CouldNotFindHandlerAssembly, fex.FileName);
            }
            catch (LambdaValidationException validationException)
            {
                _logger.LogError(validationException, "An error occured on UCL Init");
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An error occured on UCL Init");
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.UnableToLoadAssembly, _handler.AssemblyName);
            }

            _logger.LogDebug($"UCL : Attempting to load type '{_handler.TypeName}'");
            var customerType = customerAssembly.GetType(_handler.TypeName);
            if (customerType == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.UnableToLoadType, _handler.TypeName, _handler.AssemblyName);
            }

            _logger.LogDebug($"UCL : Attempting to find method '{_handler.MethodName}' in type '{_handler.TypeName}'");
            CustomerMethodInfo = FindCustomerMethod(customerType);
            _logger.LogDebug($"UCL : Located method '{CustomerMethodInfo}'");

            _logger.LogDebug($"UCL : Validating method '{CustomerMethodInfo}'");
            UserCodeValidator.ValidateCustomerMethod(CustomerMethodInfo);

            var customerObject = GetCustomerObject(customerType);

            var customerSerializerInstance = GetSerializerObject(customerAssembly);
            _logger.LogDebug($"UCL : Constructing invoke delegate");

            var isPreJit = UserCodeInit.IsCallPreJit();
            var builder = new InvokeDelegateBuilder(_logger, _handler, CustomerMethodInfo);
            _invokeDelegate = builder.ConstructInvokeDelegate(customerObject, customerSerializerInstance, isPreJit);
            if (isPreJit)
            {
                _logger.LogInformation("PreJit: PrepareDelegate");
                RuntimeHelpers.PrepareDelegate(_invokeDelegate);
            }
        }

        /// <summary>
        /// Calls into the customer method.
        /// </summary>
        /// <param name="lambdaData">Input stream.</param>
        /// <param name="lambdaContext">Context for the invocation.</param>
        /// <param name="outStream">Output stream.</param>
        public void Invoke(Stream lambdaData, ILambdaContext lambdaContext, Stream outStream)
        {
            _invokeDelegate(lambdaData, lambdaContext, outStream);
        }

        internal static void SetCustomerLoggerLogAction(Assembly coreAssembly, Action<string> customerLoggingAction, InternalLogger internalLogger)
        {
            if (coreAssembly == null)
            {
                throw new ArgumentNullException(nameof(coreAssembly));
            }

            if (customerLoggingAction == null)
            {
                throw new ArgumentNullException(nameof(customerLoggingAction));
            }

            internalLogger.LogDebug($"UCL : Retrieving type '{Types.LambdaLoggerTypeName}'");
            var lambdaILoggerType = coreAssembly.GetType(Types.LambdaLoggerTypeName);
            if (lambdaILoggerType == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.Internal.UnableToLocateType, Types.LambdaLoggerTypeName);
            }

            internalLogger.LogDebug($"UCL : Retrieving field '{LambdaLoggingActionFieldName}'");
            var loggingActionField = lambdaILoggerType.GetTypeInfo().GetField(LambdaLoggingActionFieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (loggingActionField == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.Internal.UnableToRetrieveField, LambdaLoggingActionFieldName, Types.LambdaLoggerTypeName);
            }

            internalLogger.LogDebug($"UCL : Setting field '{LambdaLoggingActionFieldName}'");
            try
            {
                loggingActionField.SetValue(null, customerLoggingAction);
            }
            catch (Exception e)
            {
                throw LambdaExceptions.ValidationException(e, Errors.UserCodeLoader.Internal.UnableToSetField,
                    Types.LambdaLoggerTypeName, LambdaLoggingActionFieldName);
            }
        }

        /// <summary>
        /// Constructs customer-specified serializer, specified either on the method,
        /// the assembly, or not specified at all.
        /// Returns null if serializer not specified.
        /// </summary>
        /// <param name="customerAssembly">Assembly that contains customer code.</param>
        /// <returns>Instance of serializer object defined with LambdaSerializerAttribute</returns>
        private object GetSerializerObject(Assembly customerAssembly)
        {
            // try looking up the LambdaSerializerAttribute on the method
            _logger.LogDebug($"UCL : Searching for LambdaSerializerAttribute at method level");
            var customerSerializerAttribute = CustomerMethodInfo.GetCustomAttributes().SingleOrDefault(a => Types.IsLambdaSerializerAttribute(a.GetType()));

            _logger.LogDebug($"UCL : LambdaSerializerAttribute at method level {(customerSerializerAttribute != null ? "found" : "not found")}");

            // only check the assembly if the LambdaSerializerAttribute does not exist on the method
            if (customerSerializerAttribute == null)
            {
                _logger.LogDebug($"UCL : Searching for LambdaSerializerAttribute at assembly level");
                customerSerializerAttribute = customerAssembly.GetCustomAttributes()
                    .SingleOrDefault(a => Types.IsLambdaSerializerAttribute(a.GetType()));
                _logger.LogDebug($"UCL : LambdaSerializerAttribute at assembly level {(customerSerializerAttribute != null ? "found" : "not found")}");
            }

            var serializerAttributeExists = customerSerializerAttribute != null;
            _logger.LogDebug($"UCL : LambdaSerializerAttribute {(serializerAttributeExists ? "found" : "not found")}");

            if (serializerAttributeExists)
            {
                _logger.LogDebug($"UCL : Constructing custom serializer");
                return ConstructCustomSerializer(customerSerializerAttribute);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Attempts to find MethodInfo in given type
        /// Returns null if no matching method was found
        /// </summary>
        /// <param name="type">Type that contains customer method.</param>
        /// <returns>Method information of customer method.</returns>
        /// <exception cref="LambdaValidationException">Thrown when failed to find customer method in container type.</exception>
        private MethodInfo FindCustomerMethod(Type type)
        {
            // These are split because finding by name is slightly faster
            // and it's also the more common case.
            // RuntimeMethodInfo::ToString() always contains a ' ' character.
            // So one of the two lookup methods would always return null.
            var customerMethodInfo = FindCustomerMethodByName(type.GetTypeInfo()) ??
                                     FindCustomerMethodBySignature(type.GetTypeInfo());

            if (customerMethodInfo == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.NoMatchingMethod,
                    _handler.MethodName, _handler.TypeName, _handler.AssemblyName, _handler.MethodName);
            }

            return customerMethodInfo;
        }

        private MethodInfo FindCustomerMethodByName(TypeInfo typeInfo)
        {
            try
            {
                var mi = typeInfo.GetMethod(_handler.MethodName, Constants.DefaultFlags);
                if (mi == null)
                {
                    var parentType = typeInfo.BaseType;

                    // check if current type is System.Object (parentType is null) and leave
                    if (parentType == null)
                    {
                        return null;
                    }

                    // check base type
                    return FindCustomerMethodByName(parentType.GetTypeInfo());
                }

                return mi;
            }
            catch (AmbiguousMatchException)
            {
                throw GetMultipleMethodsValidationException(typeInfo);
            }
        }

        private MethodInfo FindCustomerMethodBySignature(TypeInfo typeInfo)
        {
            // get all methods
            var matchingMethods = typeInfo.GetMethods(Constants.DefaultFlags)
                .Where(mi => SignatureMatches(_handler.MethodName, mi))
                .ToList();

            // check for single match in these methods
            if (matchingMethods.Count == 1)
            {
                return matchingMethods[0];
            }
            else if (matchingMethods.Count > 1)
            {
                // should never happen because signatures are unique but ...
                throw GetMultipleMethodsValidationException(typeInfo);
            }
            else
            {
                var parentType = typeInfo.BaseType;

                // check if current type is System.Object (parentType is null) and leave
                if (parentType == null)
                {
                    return null;
                }

                // check base type
                return FindCustomerMethodBySignature(parentType.GetTypeInfo());
            }
        }

        private static bool SignatureMatches(string methodSignature, MethodInfo method)
        {
            return string.Equals(methodSignature, method.ToString(), StringComparison.Ordinal);
        }

        private static bool NameMatches(string methodName, MethodInfo method)
        {
            return string.Equals(methodName, method.Name, StringComparison.Ordinal);
        }

        private Exception GetMultipleMethodsValidationException(TypeInfo typeInfo)
        {
            var signatureList = typeInfo.GetMethods(Constants.DefaultFlags)
                .Where(mi => SignatureMatches(_handler.MethodName, mi) || NameMatches(_handler.MethodName, mi))
                .Select(mi => mi.ToString()).ToList();
            var signatureListText = string.Join("\n", signatureList);

            throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.MethodHasOverloads,
                _handler.MethodName, typeInfo.FullName, signatureListText);
        }

        /// <summary>
        /// Constructs an instance of the customer-specified serializer
        /// </summary>
        /// <param name="serializerAttribute">Serializer attribute used to define the input/output serializer.</param>
        /// <returns></returns>
        /// <exception cref="LambdaValidationException">Thrown when serializer doesn't satisfy serializer type requirements.</exception>
        /// <exception cref="LambdaUserCodeException">Thrown when failed to instantiate serializer type.</exception>
        private object ConstructCustomSerializer(Attribute serializerAttribute)
        {
            var attributeType = serializerAttribute.GetType();
            var serializerTypeProperty = attributeType.GetTypeInfo().GetProperty("SerializerType");
            if (serializerTypeProperty == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.InvalidClassNoSerializerType, attributeType.FullName);
            }

            if (!Types.TypeType.GetTypeInfo().IsAssignableFrom(serializerTypeProperty.PropertyType))
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.InvalidClassSerializerTypeWrongType,
                    attributeType.FullName, Types.TypeType.FullName);
            }

            var serializerType = serializerTypeProperty.GetValue(serializerAttribute) as Type;
            if (serializerType == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.SerializerTypeNotSet,
                    attributeType.FullName);
            }

            var serializerTypeInfo = serializerType.GetTypeInfo();

            var constructor = serializerTypeInfo.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.SerializerMissingConstructor, serializerType.FullName);
            }

            var iLambdaSerializerType = serializerTypeInfo.GetInterface(Types.ILambdaSerializerTypeName);
            if (iLambdaSerializerType == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.InvalidClassNoILambdaSerializer, serializerType.FullName);
            }

            _logger.LogDebug($"UCL : Validating type '{iLambdaSerializerType.FullName}'");
            UserCodeValidator.ValidateILambdaSerializerType(iLambdaSerializerType);

            object customSerializerInstance;
            customSerializerInstance = constructor.Invoke(null);

            return customSerializerInstance;
        }

        /// <summary>
        /// Constructs an instance of the customer type, or returns null
        /// if the customer method is static and does not require an object
        /// </summary>
        /// <param name="customerType">Type of the customer handler container.</param>
        /// <returns>Instance of customer handler container type</returns>
        /// <exception cref="LambdaUserCodeException">Thrown when failed to instantiate customer type.</exception>
        private object GetCustomerObject(Type customerType)
        {
            _logger.LogDebug($"UCL : Validating type '{_handler.TypeName}'");
            UserCodeValidator.ValidateCustomerType(customerType, CustomerMethodInfo);

            var isHandlerStatic = CustomerMethodInfo.IsStatic;
            if (isHandlerStatic)
            {
                _logger.LogDebug($"UCL : Not constructing customer object, customer method is static");
                _logger.LogDebug($"UCL : Running static constructor for type '{_handler.TypeName}'");

                // Make sure the static initializer for the type runs now, during the init phase.
                RuntimeHelpers.RunClassConstructor(customerType.TypeHandle);

                return null;
            }

            _logger.LogDebug($"UCL : Instantiating type '{_handler.TypeName}'");

            return Activator.CreateInstance(customerType);
        }
    }
}
