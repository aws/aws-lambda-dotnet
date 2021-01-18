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
using System.Reflection;
using Amazon.Lambda.RuntimeSupport.ExceptionHandling;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    internal static class UserCodeValidator
    {
        /// <summary>
        /// Throws exception if type is not one we can work with
        /// </summary>
        /// <param name="type">Container type of customer method.</param>
        /// <param name="method">Method information of customer method.</param>
        /// <exception cref="LambdaValidationException">Throws when customer type is not lambda compatible.</exception>
        internal static void ValidateCustomerType(Type type, MethodInfo method)
        {
            var typeInfo = type.GetTypeInfo();

            // generic customer type is not supported
            if (typeInfo.IsGenericType)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerTypeGeneric,
                    type.FullName);
            }

            // abstract customer type is not support if customer method is not static
            if (!method.IsStatic && typeInfo.IsAbstract)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerTypeAbstract,
                    method.ToString(), type.FullName);
            }

            var isClass = typeInfo.IsClass;
            var isStruct = typeInfo.IsValueType && !typeInfo.IsPrimitive && !typeInfo.IsEnum;

            // customer type must be class or struct
            if (!isClass && !isStruct)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerNotClassOrStruct,
                    type.FullName);
            }
        }

        /// <summary>
        /// Validate customer method signature
        /// Throws exception if method is not one we can work with
        /// </summary>
        /// <param name="method">MethodInfo of customer method</param>
        /// <exception cref="LambdaValidationException">Thrown when customer method doesn't satisfy method requirements</exception>
        internal static void ValidateCustomerMethod(MethodInfo method)
        {
            if (method.IsAbstract)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerMethodAbstract,
                    method.ToString());
            }

            if (method.IsGenericMethod)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerMethodGeneric,
                    method.ToString());
            }

            var asyncAttribute = method.GetCustomAttribute(Types.AsyncStateMachineAttributeType);
            var isAsync = asyncAttribute != null;
            var isVoid = method.ReturnType == Types.VoidType;
            if (isVoid && isAsync)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerMethodAsyncVoid,
                    method.ToString());
            }

            var inputParameters = method.GetParameters();
            if (inputParameters.Length > 0)
            {
                var lastParameter = inputParameters[inputParameters.Length - 1];
                var paramArrayAttribute = lastParameter.GetCustomAttribute(Types.ParamArrayAttributeType);
                if (paramArrayAttribute != null)
                {
                    throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerMethodParams,
                        method.ToString());
                }
            }

            // detect VarArgs methods
            if ((method.CallingConvention & CallingConventions.VarArgs) == CallingConventions.VarArgs)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.HandlerMethodVararg,
                    method.ToString());
            }
        }

        /// <summary>
        /// Validates object serializer used for serialization and deserialization of input and output
        /// Throws exception if the specified ILambdaSerializer is a type we can work with
        /// </summary>
        /// <param name="type">Type of the customer's serializer.</param>
        /// <exception cref="LambdaValidationException">Thrown when customer serializer doesn't match with expected serializer definition</exception>
        internal static void ValidateILambdaSerializerType(Type type)
        {
            var typeInfo = type.GetTypeInfo();

            var mismatchReason = CheckILambdaSerializerType(typeInfo);
            if (!string.IsNullOrEmpty(mismatchReason))
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.TypeNotMatchingShape,
                    type.FullName, Types.ILambdaSerializerTypeName, mismatchReason);
            }
        }

        /// <summary>
        /// Checks that the ILambdaSerializer type is correct, returning null if type is as expected
        /// or a non-null string with the reason if type is not correct.
        /// </summary>
        /// <param name="typeInfo">TypeInfo of the customer serializer.</param>
        /// <returns>Error string if validation fails else null.</returns>
        private static string CheckILambdaSerializerType(TypeInfo typeInfo)
        {
            if (!typeInfo.IsInterface)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_TypeNotInterface, typeInfo.FullName);
            }

            // check that the Deserialize method exists and is generic
            var deserializeMethodInfo = typeInfo.GetMethod("Deserialize");
            if (deserializeMethodInfo == null)
            {
                return Errors.UserCodeLoader.ILambdaSerializerMismatch_DeserializeMethodNotFound;
            }

            if (!deserializeMethodInfo.IsGenericMethod)
            {
                return Errors.UserCodeLoader.ILambdaSerializerMismatch_DeserializeMethodNotGeneric;
            }

            // verify that Stream is the only input
            var deserializeInputs = deserializeMethodInfo.GetParameters();
            if (deserializeInputs.Length != 1)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_DeserializeMethodHasTooManyParams, deserializeInputs.Length);
            }

            if (deserializeInputs[0].ParameterType != Types.StreamType)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_DeserializeMethodHasWrongParam, deserializeInputs[0].ParameterType.FullName,
                    Types.StreamType.FullName);
            }

            // verify that T is the return type
            var deserializeOutputType = deserializeMethodInfo.ReturnType;
            var deserializeGenericArguments = deserializeMethodInfo.GetGenericArguments();
            if (deserializeGenericArguments.Length != 1)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_DeserializeMethodHasWrongNumberGenericArgs,
                    deserializeGenericArguments.Length);
            }

            if (deserializeGenericArguments[0] != deserializeOutputType)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_DeserializeMethodHasWrongReturn, deserializeOutputType.FullName);
            }

            // check that the Serialize method exists, is generic, and returns void
            var serializeMethodInfo = typeInfo.GetMethod("Serialize");
            if (serializeMethodInfo == null)
            {
                return Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodNotFound;
            }

            if (!serializeMethodInfo.IsGenericMethod)
            {
                return Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodNotGeneric;
            }

            if (serializeMethodInfo.ReturnType != Types.VoidType)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodHasWrongReturn, serializeMethodInfo.ReturnType.FullName);
            }

            // verify that T is the first input and Stream is the second input
            var serializeInputs = serializeMethodInfo.GetParameters();
            var serializeGenericArguments = serializeMethodInfo.GetGenericArguments();
            if (serializeInputs.Length != 2)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodHasWrongNumberOfParameters, serializeInputs.Length);
            }

            if (serializeGenericArguments.Length != 1)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodHasWrongNumberGenericArgs, serializeGenericArguments.Length);
            }

            if (serializeInputs[0].ParameterType != serializeGenericArguments[0])
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodHasWrongFirstParam, serializeInputs[0].ParameterType.FullName);
            }

            if (serializeInputs[1].ParameterType != Types.StreamType)
            {
                return LambdaExceptions.FormatMessage(Errors.UserCodeLoader.ILambdaSerializerMismatch_SerializeMethodHasWrongSecondParam, serializeInputs[1].ParameterType.FullName,
                    Types.StreamType.FullName);
            }

            // all good!
            return null;
        }

        /// <summary>
        /// Validates ILambdaContext properties and methods
        /// Throws exception if context type is not one we can work with
        /// This checks the set of members on the first version of ILambdaContext type.
        /// DO NOT update this code when new members are added to ILambdaContext type,
        /// it will break existing Lambda deployment packages which still use older version of ILambdaContext.
        /// </summary>
        /// <param name="iLambdaContextType">Type of context passed for the invocation.</param>
        /// <exception cref="LambdaValidationException">Thrown when context doesn't contain required properties & methods.</exception>
        internal static void ValidateILambdaContextType(Type iLambdaContextType)
        {
            if (iLambdaContextType == null)
                return;

            ValidateInterfaceStringProperty(iLambdaContextType, "AwsRequestId");
            ValidateInterfaceStringProperty(iLambdaContextType, "FunctionName");
            ValidateInterfaceStringProperty(iLambdaContextType, "FunctionVersion");
            ValidateInterfaceStringProperty(iLambdaContextType, "InvokedFunctionArn");
            ValidateInterfaceStringProperty(iLambdaContextType, "LogGroupName");
            ValidateInterfaceStringProperty(iLambdaContextType, "LogStreamName");
            ValidateInterfaceProperty<int>(iLambdaContextType, "MemoryLimitInMB");
            ValidateInterfaceProperty<TimeSpan>(iLambdaContextType, "RemainingTime");

            var clientContextProperty = ValidateInterfaceProperty(iLambdaContextType, "ClientContext", Types.IClientContextTypeName);
            var iClientContextType = clientContextProperty.PropertyType;
            ValidateInterfaceProperty<IDictionary<string, string>>(iClientContextType, "Environment");
            ValidateInterfaceProperty<IDictionary<string, string>>(iClientContextType, "Custom");
            ValidateInterfaceProperty(iClientContextType, "Client", Types.IClientApplicationTypeName);

            var identityProperty = ValidateInterfaceProperty(iLambdaContextType, "Identity", Types.ICognitoIdentityTypeName);
            var iCognitoIdentityType = identityProperty.PropertyType;
            ValidateInterfaceStringProperty(iCognitoIdentityType, "IdentityId");
            ValidateInterfaceStringProperty(iCognitoIdentityType, "IdentityPoolId");

            var loggerProperty = ValidateInterfaceProperty(iLambdaContextType, "Logger", Types.ILambdaLoggerTypeName);
            var iLambdaLoggerType = loggerProperty.PropertyType;
            var logMethod = iLambdaLoggerType.GetTypeInfo().GetMethod("Log", new[] {Types.StringType}, null);
            if (logMethod == null)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.TypeMissingLogMethod, iLambdaLoggerType.FullName);
            }
        }

        private static void ValidateInterfaceStringProperty(Type type, string propName)
        {
            ValidateInterfaceProperty<string>(type, propName);
        }

        private static void ValidateInterfaceProperty<T>(Type type, string propName)
        {
            var propType = typeof(T);
            ValidateInterfaceProperty(type, propName, propType.FullName);
        }

        private static PropertyInfo ValidateInterfaceProperty(Type type, string propName, string propTypeName)
        {
            var propertyInfo = type.GetTypeInfo().GetProperty(propName, Constants.DefaultFlags);
            if (propertyInfo == null || !string.Equals(propertyInfo.PropertyType.FullName, propTypeName, StringComparison.Ordinal))
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.TypeMissingExpectedProperty, type.FullName, propName, propTypeName);
            }

            if (!propertyInfo.CanRead)
            {
                throw LambdaExceptions.ValidationException(Errors.UserCodeLoader.PropertyNotReadable, propName, type.FullName);
            }

            return propertyInfo;
        }
    }
}