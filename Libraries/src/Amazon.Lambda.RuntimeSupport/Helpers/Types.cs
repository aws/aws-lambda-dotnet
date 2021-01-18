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
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    /// <summary>
    /// Static methods for working with types
    /// </summary>
    internal static class Types
    {
        // Types only available by name
        public const string IClientContextTypeName = "Amazon.Lambda.Core.IClientContext";
        public const string IClientApplicationTypeName = "Amazon.Lambda.Core.IClientApplication";
        public const string ICognitoIdentityTypeName = "Amazon.Lambda.Core.ICognitoIdentity";
        public const string ILambdaContextTypeName = "Amazon.Lambda.Core.ILambdaContext";
        public const string ILambdaLoggerTypeName = "Amazon.Lambda.Core.ILambdaLogger";
        public const string ILambdaSerializerTypeName = "Amazon.Lambda.Core.ILambdaSerializer";

        public const string LambdaLoggerTypeName = "Amazon.Lambda.Core.LambdaLogger";
        public const string LambdaSerializerAttributeTypeName = "Amazon.Lambda.Core.LambdaSerializerAttribute";

        // CLR types, lazily-loaded

        public static Type AsyncStateMachineAttributeType => AsyncStateMachineAttributeTypeLazy.Value;

        public static Type ParamArrayAttributeType => ParamArrayAttributeTypeLazy.Value;

        public static Type StreamType => StreamTypeLazy.Value;

        public static Type StringType => StringTypeLazy.Value;

        public static Type TaskTType => TaskTTypeLazy.Value;

        public static Type TaskType => TaskTypeLazy.Value;

        public static Type TypeType => TypeTypeLazy.Value;

        public static Type VoidType => VoidTypeLazy.Value;

        // CLR types, lazily-loaded, backing fields

        private static readonly Lazy<Type> AsyncStateMachineAttributeTypeLazy =
            new Lazy<Type>(() => typeof(AsyncStateMachineAttribute));

        private static readonly Lazy<Type> ParamArrayAttributeTypeLazy =
            new Lazy<Type>(() => typeof(ParamArrayAttribute));

        private static readonly Lazy<Type> StreamTypeLazy = new Lazy<Type>(() => typeof(Stream));
        private static readonly Lazy<Type> StringTypeLazy = new Lazy<Type>(() => typeof(string));
        private static readonly Lazy<Type> TaskTTypeLazy = new Lazy<Type>(() => typeof(Task<>));
        private static readonly Lazy<Type> TaskTypeLazy = new Lazy<Type>(() => typeof(Task));
        private static readonly Lazy<Type> TypeTypeLazy = new Lazy<Type>(() => typeof(Type));
        private static readonly Lazy<Type> VoidTypeLazy = new Lazy<Type>(() => typeof(void));

        /// <summary>
        /// Returns true if type is ILambdaContext
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsILambdaContext(Type type)
        {
            return typeof(ILambdaContext).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns true if type is LambdaSerializerAttribute (but not if type extends LambdaSerializerAttribute)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsLambdaSerializerAttribute(Type type)
        {
            return string.Equals(type.FullName, LambdaSerializerAttributeTypeName, StringComparison.Ordinal);
        }
    }
}