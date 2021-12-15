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
using System.Linq.Expressions;
using System.Text;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static Amazon.Lambda.RuntimeSupport.Bootstrap.Constants;
using Amazon.Lambda.RuntimeSupport.Helpers;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    internal class UserCodeInit
    {
        public static bool IsCallPreJit()
        {
            string awsLambdaDotNetPreJitStr = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PREJIT);
            string awsLambdaInitTypeStr = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE_AWS_LAMBDA_INITIALIZATION_TYPE);
            AwsLambdaDotNetPreJit awsLambdaDotNetPreJit;
            bool isParsed = Enum.TryParse(awsLambdaDotNetPreJitStr, true, out awsLambdaDotNetPreJit);
            if (!isParsed)
            {
                awsLambdaDotNetPreJit = AwsLambdaDotNetPreJit.ProvisionedConcurrency;
            }

            return IsPreJitAll(awsLambdaDotNetPreJit) ||
                   (IsPCInvoke(awsLambdaInitTypeStr) && IsPreJitPC(awsLambdaDotNetPreJit));
        }

        private static bool IsPreJitAll(AwsLambdaDotNetPreJit awsLambdaDotNetPreJit)
        {
            return AwsLambdaDotNetPreJit.Always.Equals(awsLambdaDotNetPreJit);
        }

        private static bool IsPCInvoke(string awsLambdaInitTypeStr)
        {
            return AWS_LAMBDA_INITIALIZATION_TYPE_PC.Equals(awsLambdaInitTypeStr);
        }

        private static bool IsPreJitPC(AwsLambdaDotNetPreJit awsLambdaDotNetPreJit)
        {
            return AwsLambdaDotNetPreJit.ProvisionedConcurrency.Equals(awsLambdaDotNetPreJit);
        }

        public static void InitDeserializationAssembly(Expression inputExpression, ParameterExpression inStreamParameter)
        {
            if (inputExpression != null)
            {
                try
                {
                    byte[] byteArray = Encoding.ASCII.GetBytes("''");
                    using (Stream dummyInStream = new MemoryStream(byteArray))
                    {
                        Expression.Lambda(inputExpression, inStreamParameter)
                            .Compile()
                            .DynamicInvoke(dummyInStream);
                    }
                }
                catch
                {
                    // An exception is expected here because the dummy JSON is unlikely to match POCO
                }
            }
        }

        public static void InitSerializationAssembly(Expression outputExpression, ParameterExpression outStreamParameter, Type customerOutputType)
        {
            if (outputExpression == null || customerOutputType == null || customerOutputType == typeof(void))
                return;
            try
            {
                var customerObjectParameter = Expression.Parameter(customerOutputType, "customerObject");
                byte[] byteArray = Encoding.ASCII.GetBytes("''");
                using (Stream dummyInStream = new MemoryStream(byteArray))
                {
                    Expression.Lambda(outputExpression, customerObjectParameter, outStreamParameter)
                        .Compile()
                        .DynamicInvoke(null, dummyInStream);
                }
            }
            catch
            {
                // An exception is expected here because the dummy JSON is unlikely to match POCO
            }
        }

        public static void LoadStringCultureInfo()
        {
            try
            {
                string locale = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE_LANG);

                if (!string.IsNullOrEmpty(locale))
                {
                    // The environment variable also has the text encoding (i.e. en_US.UTF-8). .NET 6
                    // Does not accept culture names with text encoding and throw an exception. 
                    // To avoid that strip off the text encoding.
                    if (locale.Contains("."))
                    {
                        locale = locale.Substring(0, locale.IndexOf("."));
                    }
                    /*A dummy operation on string*/
                    "Lambdalanguages".ToUpper(new CultureInfo(locale));
                }
                else
                {
                    /*A dummy operation on string*/
                    "Lambdalanguages".ToUpper();
                }
            }
            catch (Exception e)
            {
                InternalLogger.GetDefaultLogger().LogError(e, "PreJit: Error with LoadStringCultureInfo");
            }
        }

        public static void PreJitAssembly(Assembly a)
        {
            // Storage to ensure not loading the same assembly twice and optimize calls to GetAssemblies()
            var loaded = new HashSet<string>();

            PrepareAssembly(a);
            LoadReferencedAssemblies(a);

            // Filter to avoid loading all the .net framework
            bool ShouldLoad(string assemblyName)
            {
                return !loaded.Contains(assemblyName) && NotNetFramework(assemblyName);
            }

            bool NotNetFramework(string assemblyName)
            {
                return !assemblyName.StartsWith("Microsoft.")
                       && !assemblyName.StartsWith("System.")
                       && !assemblyName.StartsWith("Newtonsoft.")
                       && !assemblyName.StartsWith("netstandard");
            }

            void PrepareAssembly(Assembly assembly)
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly |
                                                           BindingFlags.NonPublic |
                                                           BindingFlags.Public | BindingFlags.Instance |
                                                           BindingFlags.Static))
                    {
                        try
                        {
                            if (method.IsAbstract)
                                continue;

                            RuntimeHelpers.PrepareMethod(
                                method.MethodHandle);

                        }
                        catch (Exception e)
                        {
                            InternalLogger.GetDefaultLogger().LogError(e, "PreJit: Error with PrepareAssembly");
                        }
                    }
                }
            }

            void LoadReferencedAssemblies(Assembly assembly)
            {

                foreach (AssemblyName an in assembly.GetReferencedAssemblies().Where(x => ShouldLoad(x.FullName)))
                {
                    // Load the assembly and load its dependencies
                    Assembly loadedAssembly = Assembly.Load(an);
                    loaded.Add(an.FullName);
                    PrepareAssembly(loadedAssembly);
                    LoadReferencedAssemblies(loadedAssembly); // Load the referenced assemblies
                }
            }
        }
    }
}