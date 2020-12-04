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
using System.Reflection;
using Amazon.Lambda.RuntimeSupport.ExceptionHandling;

namespace Amazon.Lambda.RuntimeSupport.Helpers
{
    internal class HandlerInfo
    {
        /// <summary>
        /// Separator for different handler components.
        /// </summary>
        public const string HandlerSeparator = "::";

        /// <summary>
        /// Name of the user assembly.
        /// Does not contain ".dll" extension.
        /// </summary>
        public AssemblyName AssemblyName { get; }

        /// <summary>
        /// Full type name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Method name.
        /// This value can be equal to MethodInfo.Name (such as "DownloadManifest"),
        /// or it can be equal to MethodInfo.ToString() (such as "System.Uri DownloadManifest(Int64)")
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Constructs an instance of HandlerInfo for a given handler string.
        /// </summary>
        /// <param name="handler"></param>
        public HandlerInfo(string handler)
        {
            if (string.IsNullOrEmpty(handler))
            {
                throw LambdaExceptions.ValidationException(Errors.HandlerInfo.EmptyHandler, HandlerSeparator, HandlerSeparator);
            }

            var parts = handler.Split(new[] {HandlerSeparator}, 3, StringSplitOptions.None);
            if (parts.Length != 3)
            {
                throw LambdaExceptions.ValidationException(Errors.HandlerInfo.InvalidHandler, handler, HandlerSeparator, HandlerSeparator);
            }

            var assemblyName = parts[0].Trim();
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw LambdaExceptions.ValidationException(Errors.HandlerInfo.MissingAssembly, handler, HandlerSeparator, HandlerSeparator);
            }

            var typeName = parts[1].Trim();
            if (string.IsNullOrEmpty(typeName))
            {
                throw LambdaExceptions.ValidationException(Errors.HandlerInfo.MissingType, handler, HandlerSeparator, HandlerSeparator);
            }

            var methodName = parts[2].Trim();
            if (string.IsNullOrEmpty(methodName))
            {
                throw LambdaExceptions.ValidationException(Errors.HandlerInfo.MissingMethod, handler, HandlerSeparator, HandlerSeparator);
            }

            AssemblyName = new AssemblyName(assemblyName);
            TypeName = typeName;
            MethodName = methodName;
        }
    }
}