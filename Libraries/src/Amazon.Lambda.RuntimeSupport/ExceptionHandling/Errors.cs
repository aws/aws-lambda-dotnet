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

namespace Amazon.Lambda.RuntimeSupport.ExceptionHandling
{
    /// <summary>
    /// A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    internal static class Errors
    {
        internal static class UserCodeLoader
        {
            internal static class Internal
            {
                public const string UnableToLocateType = "Internal error: unable to locate '{0}' type.";
                public const string UnableToRetrieveField = "Internal error: unable to retrieve '{0}' field from '{1}'.";
                public const string UnableToSetField = "Internal error: unable to set '{0}.{1}' field.";
            }

            public const string CouldNotFindHandlerAssembly = "Could not find the specified handler assembly with the file name '{0}'. The assembly should be located in the root of your uploaded .zip file.";
            public const string UnableToLoadAssembly = "Unable to load assembly '{0}'.";
            public const string UnableToLoadType = "Unable to load type '{0}' from assembly '{1}'.";
            public const string DeserializeMissingAttribute = "Could not find the LambdaSerializerAttribute on the assembly '{0}' or method '{1}' while attempting to deserialize input data of type '{2}'. To use types other than System.IO.Stream as input/output parameters, the assembly or Lambda function should be annotated with Amazon.Lambda.LambdaSerializerAttribute.";
            public const string SerializeMissingAttribute = "Could not find the LambdaSerializerAttribute on the assembly '{0}' or method '{1}' while attempting to serialize output data of type '{2}'. To use types other than System.IO.Stream as input/output parameters, the assembly or Lambda function should be annotated with Amazon.Lambda.LambdaSerializerAttribute.";
            public const string MethodTooManyParams = "Method '{0}' of type '{1}' is not supported: the method has more than 2 parameters.";
            public const string MethodSecondParamNotContext = "Method '{0}' of type '{1}' is not supported: the method has 2 parameters, but the second parameter is not of type '{2}'.";
            public const string NoMatchingMethod = "Unable to find method '{0}' in type '{1}' from assembly '{2}': Found no methods matching method name '{3}'.";
            public const string InvalidClassNoSerializerType = "The '{0}' class is invalid: no 'SerializerType' property detected.";
            public const string InvalidClassSerializerTypeWrongType = "The '{0}' class is invalid: 'SerializerType' property cannot be converted to type '{1}'.";
            public const string SerializerTypeNotSet = "The 'SerializerType' property on attribute class '{0}' is not set.";
            public const string SerializerMissingConstructor = "The indicated serializer '{0}' does not define a public zero-parameter constructor.";
            public const string InvalidClassNoILambdaSerializer = "The '{0}' class is invalid: it does not implement the 'ILambdaSerializer' interface.";
            public const string HandlerTypeGeneric = "The type '{0}' cannot be used as a Lambda Handler type because it is generic. Handler methods cannot be located in generic types. Please specify a handler method in a non-generic type.";
            public const string HandlerTypeAbstract = "The instance method '{0}' cannot be used as a Lambda Handler because its type '{1}' is abstract. Please either use a static handler or specify a type that is not abstract.";
            public const string HandlerNotClassOrStruct = "The type '{0}' cannot be used as a handler type since it is not a class or a struct. Please specify a handler method on a class or struct.";
            public const string HandlerMethodAbstract = "The method '{0}' cannot be used as a Lambda Handler because it is an abstract method. Handler methods cannot be abstract. Please specify a non-abstract handler method.";
            public const string HandlerMethodGeneric = "The method '{0}' cannot be used as a Lambda Handler because it is a generic method. Handler methods cannot be generic. Please specify a non-generic handler method.";
            public const string HandlerMethodAsyncVoid = "The method '{0}' cannot be used as a Lambda Handler because it is an 'async void' method. Handler methods cannot be 'async void'. Please specify a method that is not 'async void'.";
            public const string HandlerMethodParams = "The method '{0}' cannot be used as a Lambda Handler because it is a 'params' method. Please specify a method that does not use 'params'.";
            public const string HandlerMethodVararg = "The method '{0}' cannot be used as a Lambda Handler because it is a 'vararg (variable arguments)' method. Please specify a method that is not 'vararg'.";
            public const string TypeMissingLogMethod = "The type '{0}' does not contain expected method 'Log'.";
            public const string TypeNotMatchingShape = "The type '{0}' does not match the expected shape of '{1}': '{2}'.";
            public const string TypeMissingExpectedProperty = "The type '{0}' does not contain expected property '{1}' of type '{2}'.";
            public const string PropertyNotReadable = "The property '{0}' of type '{1}' is not readable.";
            public const string ILambdaSerializerMismatch_TypeNotInterface = "Type {0} is not an interface.";
            public const string ILambdaSerializerMismatch_DeserializeMethodNotFound = "Deserialize' method not found.";
            public const string ILambdaSerializerMismatch_DeserializeMethodNotGeneric = "Deserialize' method is not generic, expected to be generic.";
            public const string ILambdaSerializerMismatch_DeserializeMethodHasTooManyParams = "Deserialize' method has '{0}' parameters, expected '1'.";
            public const string ILambdaSerializerMismatch_DeserializeMethodHasWrongParam = "Deserialize' method has parameter of type '{0}', expected type '{1}'.";
            public const string ILambdaSerializerMismatch_DeserializeMethodHasWrongNumberGenericArgs = "Deserialize' method has '{0}' generic arguments, expected '1'.";
            public const string ILambdaSerializerMismatch_DeserializeMethodHasWrongReturn = "Deserialize' method has return type of '{0}', expected 'T'.";
            public const string ILambdaSerializerMismatch_SerializeMethodNotFound = "Serialize' method not found.";
            public const string ILambdaSerializerMismatch_SerializeMethodNotGeneric = "Serialize' method is not generic, expected to be generic.";
            public const string ILambdaSerializerMismatch_SerializeMethodHasWrongReturn = "Serialize' method has return type of '{0}', expected 'void'.";
            public const string ILambdaSerializerMismatch_SerializeMethodHasWrongNumberOfParameters = "Serialize' method has '{0}' parameters, expected '2'.";
            public const string ILambdaSerializerMismatch_SerializeMethodHasWrongNumberGenericArgs = "Serialize' method has '{0}' generic arguments, expected '1'.";
            public const string ILambdaSerializerMismatch_SerializeMethodHasWrongFirstParam = "Serialize' method's first parameter is of type '{0}', expected 'T'.";
            public const string ILambdaSerializerMismatch_SerializeMethodHasWrongSecondParam = "Serialize' method's second parameter is of type '{0}', expected '{1}'.";
            public const string MethodHasOverloads = "The method '{0}' in type '{1}' appears to have a number of overloads. To call this method please specify a complete method signature. Possible candidates are:\n{2}.";
        }

        internal static class HandlerInfo
        {
            public const string EmptyHandler = "Empty lambda function handler. The valid format is 'ASSEMBLY{0}TYPE{1}METHOD'.";
            public const string InvalidHandler = "Invalid lambda function handler: '{0}'. The valid format is 'ASSEMBLY{1}TYPE{2}METHOD'.";
            public const string MissingAssembly = "Invalid lambda function handler: '{0}', the assembly is missing. The valid format is 'ASSEMBLY{1}TYPE{2}METHOD'.";
            public const string MissingType = "Invalid lambda function handler: '{0}', the type is missing. The valid format is 'ASSEMBLY{1}TYPE{2}METHOD'.";
            public const string MissingMethod = "Invalid lambda function handler: '{0}', the method is missing. The valid format is 'ASSEMBLY{1}TYPE{2}METHOD'.";
        }

        internal static class LambdaBootstrap
        {
            internal static class Internal
            {
                public const string LambdaResponseTooLong = "The Lambda function returned a response that is too long to serialize. The response size limit for a Lambda function is 6MB.";
            }
        }
    }
}
