/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;
using NUnit.Framework;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    public class CustomRuntimeNET6Tests : CustomRuntimeTests
    {
        public CustomRuntimeNET6Tests()
            : base(
                "CustomRuntimeNET6FunctionTest-" + DateTime.Now.Ticks, 
                "CustomRuntimeFunctionTest.zip", 
                @"CustomRuntimeFunctionTest\bin\Release\net6.0\CustomRuntimeFunctionTest.zip", 
                "CustomRuntimeFunctionTest", 
                TargetFramework.NET6)
        {
        }

#if SKIP_RUNTIME_SUPPORT_INTEG_TESTS
        [Test]
        [Ignore("Skipped intentionally by setting the SkipRuntimeSupportIntegTests build parameter.")]
#else
        [Test]
#endif
        public async Task TestAllNET6HandlersAsync()
        {
            await base.TestAllHandlersAsync();
        }
    }

    public class CustomRuntimeNET8Tests : CustomRuntimeTests
    {
        public CustomRuntimeNET8Tests()
            : base(
                "CustomRuntimeNET8FunctionTest-" + DateTime.Now.Ticks, 
                "CustomRuntimeFunctionTest.zip", 
                @"CustomRuntimeFunctionTest\bin\Release\net8.0\CustomRuntimeFunctionTest.zip", 
                "CustomRuntimeFunctionTest", 
                TargetFramework.NET8)
        {
        }

#if SKIP_RUNTIME_SUPPORT_INTEG_TESTS
        [Test]
        [Ignore("Skipped intentionally by setting the SkipRuntimeSupportIntegTests build parameter.")]
#else
        [Test]
#endif
        public async Task TestAllNET8HandlersAsync()
        {
            await base.TestAllHandlersAsync();
        }
    }

    public class CustomRuntimeTests : BaseCustomRuntimeTest
    {
        public enum TargetFramework { NET6, NET8}

        private TargetFramework _targetFramework;

        public CustomRuntimeTests(
            string functionName, 
            string deploymentZipKey, 
            string deploymentPackageZipRelativePath, 
            string handler, 
            TargetFramework targetFramework) 
            : base(functionName, deploymentZipKey, deploymentPackageZipRelativePath, handler)
        {
            _targetFramework = targetFramework;
        }
        
        protected virtual async Task TestAllHandlersAsync()
        {
            // run all test cases in one test to ensure they run serially
            using (var lambdaClient = new AmazonLambdaClient(TestRegion))
            using (var s3Client = new AmazonS3Client(TestRegion))
            using (var iamClient = new AmazonIdentityManagementServiceClient(TestRegion))
            {
                var roleAlreadyExisted = false;

                try
                {
                    roleAlreadyExisted = await PrepareTestResources(s3Client, lambdaClient, iamClient);

                    // .NET API to address setting memory constraint was added for .NET 8
                    if (_targetFramework == TargetFramework.NET8)
                    {
                        await RunMaxHeapMemoryCheck(lambdaClient, "GetTotalAvailableMemoryBytes");
                        await RunWithoutMaxHeapMemoryCheck(lambdaClient, "GetTotalAvailableMemoryBytes");
                        await RunMaxHeapMemoryCheckWithCustomMemorySettings(lambdaClient, "GetTotalAvailableMemoryBytes");
                    }

                    await RunTestExceptionAsync(lambdaClient, "ExceptionNonAsciiCharacterUnwrappedAsync", "", "Exception", "Unhandled exception with non ASCII character: ♂");
                    await RunTestSuccessAsync(lambdaClient, "UnintendedDisposeTest", "not-used", "UnintendedDisposeTest-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "LoggingStressTest", "not-used", "LoggingStressTest-success");

                    await RunJsonLoggingWithUnhandledExceptionAsync(lambdaClient);

                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Trace, LogConfigSource.LambdaAPI);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Debug, LogConfigSource.LambdaAPI);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Information, LogConfigSource.LambdaAPI);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Warning, LogConfigSource.LambdaAPI);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Error, LogConfigSource.LambdaAPI);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Critical, LogConfigSource.LambdaAPI);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Trace, LogConfigSource.DotnetEnvironment);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Debug, LogConfigSource.DotnetEnvironment);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Information, LogConfigSource.DotnetEnvironment);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Warning, LogConfigSource.DotnetEnvironment);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Error, LogConfigSource.DotnetEnvironment);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", RuntimeLogLevel.Critical, LogConfigSource.DotnetEnvironment);


                    await RunUnformattedLoggingTestAsync(lambdaClient, "LoggingTest");

                    await RunTestSuccessAsync(lambdaClient, "ToUpperAsync", "message", "ToUpperAsync-MESSAGE");
                    await RunTestSuccessAsync(lambdaClient, "PingAsync", "ping", "PingAsync-pong");
                    await RunTestSuccessAsync(lambdaClient, "HttpsWorksAsync", "", "HttpsWorksAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "CertificateCallbackWorksAsync", "", "CertificateCallbackWorksAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "NetworkingProtocolsAsync", "", "NetworkingProtocolsAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "HandlerEnvVarAsync", "", "HandlerEnvVarAsync-CustomRuntimeFunctionTest");
                    await RunTestExceptionAsync(lambdaClient, "AggregateExceptionUnwrappedAsync", "", "Exception", "Exception thrown from an async handler.");
                    await RunTestExceptionAsync(lambdaClient, "AggregateExceptionUnwrapped", "", "Exception", "Exception thrown from a synchronous handler.");
                    await RunTestExceptionAsync(lambdaClient, "AggregateExceptionNotUnwrappedAsync", "", "AggregateException", "AggregateException thrown from an async handler.");
                    await RunTestExceptionAsync(lambdaClient, "AggregateExceptionNotUnwrapped", "", "AggregateException", "AggregateException thrown from a synchronous handler.");
                    await RunTestExceptionAsync(lambdaClient, "TooLargeResponseBodyAsync", "", "Function.ResponseSizeTooLarge", "Response payload size exceeded maximum allowed payload size (6291556 bytes).");
                    await RunTestSuccessAsync(lambdaClient, "LambdaEnvironmentAsync", "", "LambdaEnvironmentAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "LambdaContextBasicAsync", "", "LambdaContextBasicAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "GetTimezoneNameAsync", "", "GetTimezoneNameAsync-UTC");
                }
                catch(NoDeploymentPackageFoundException)
                {
#if DEBUG
                    // The CodePipeline for this project doesn't currently build the deployment in the stage that runs 
                    // this test. For now ignore this test in release mode if the deployment package can't be found.
                    throw;
#endif
                }
                finally
                {
                    await CleanUpTestResources(s3Client, lambdaClient, iamClient, roleAlreadyExisted);
                }
            }
        }

        private async Task RunJsonLoggingWithUnhandledExceptionAsync(AmazonLambdaClient lambdaClient)
        {
            await UpdateHandlerAsync(lambdaClient, "ThrowUnhandledApplicationException", null, RuntimeLogLevel.Information);
            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));

            var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));
            var exceptionLog = log.Split('\n').FirstOrDefault(x => x.Contains("System.ApplicationException"));

            Assert.That(exceptionLog, Is.Not.Null);
            Assert.That(exceptionLog, Does.Contain("\"level\":\"Error\""));
        }

        private async Task RunMaxHeapMemoryCheck(AmazonLambdaClient lambdaClient, string handler)
        {
            await UpdateHandlerAsync(lambdaClient, handler);
            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                string payloadStr = (await sr.ReadToEndAsync()).Replace("\"", "");
                // Function payload response will have format {Handler}-{MemorySize}.
                // To check memory split on the - and grab the second token representing the memory size.
                var tokens = payloadStr.Split('-');
                var memory = long.Parse(tokens[1]);
                Assert.That(memory <= BaseCustomRuntimeTest.FUNCTION_MEMORY_MB * 1048576);
            }
        }

        private async Task RunWithoutMaxHeapMemoryCheck(AmazonLambdaClient lambdaClient, string handler)
        {
            await UpdateHandlerAsync(lambdaClient, handler, new Dictionary<string, string> { { "AWS_LAMBDA_DOTNET_DISABLE_MEMORY_LIMIT_CHECK", "true" } });
            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                string payloadStr = (await sr.ReadToEndAsync()).Replace("\"", "");
                // Function payload response will have format {Handler}-{MemorySize}.
                // To check memory split on the - and grab the second token representing the memory size.
                var tokens = payloadStr.Split('-');
                var memory = long.Parse(tokens[1]);
                Assert.That(memory <= BaseCustomRuntimeTest.FUNCTION_MEMORY_MB * 1048576, Is.False);
            }
        }

        private async Task RunMaxHeapMemoryCheckWithCustomMemorySettings(AmazonLambdaClient lambdaClient, string handler)
        {
            // Set the .NET GC environment variable to say there is 256 MB of memory. The function is deployed with 512 but since the user set
            // it to 256 Lambda should not make any adjustments.
            await UpdateHandlerAsync(lambdaClient, handler, new Dictionary<string, string> { { "DOTNET_GCHeapHardLimit", "0x10000000" } });
            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                string payloadStr = (await sr.ReadToEndAsync()).Replace("\"", "");
                // Function payload response will have format {Handler}-{MemorySize}.
                // To check memory split on the - and grab the second token representing the memory size.
                var tokens = payloadStr.Split('-');
                var memory = long.Parse(tokens[1]);
                Assert.That(memory <= 256 * 1048576);
            }
        }

        private async Task RunTestExceptionAsync(AmazonLambdaClient lambdaClient, string handler, string input,
            string expectedErrorType, string expectedErrorMessage)
        {
            await UpdateHandlerAsync(lambdaClient, handler);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(input));
            Assert.That(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.That(invokeResponse.FunctionError != null);
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                JObject exception = (JObject)JsonConvert.DeserializeObject(await sr.ReadToEndAsync());
                Assert.Equals(expectedErrorType, exception["errorType"].ToString());
                Assert.Equals(expectedErrorMessage, exception["errorMessage"].ToString());

                var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));
                var logExpectedException = expectedErrorType != "Function.ResponseSizeTooLarge" ? expectedErrorType : "RuntimeApiClientException";
                Assert.That(logExpectedException, Does.Contain(log));
            }
        }

        // The .NET Lambda runtime has a legacy environment variable for configuring the log level. This enum is used in the test to choose
        // whether the legacy environment variable should be set or use the new properties in the update configuration api for setting log level.
        enum LogConfigSource { LambdaAPI, DotnetEnvironment}
        private async Task RunLoggingTestAsync(AmazonLambdaClient lambdaClient, string handler, RuntimeLogLevel? runtimeLogLevel, LogConfigSource configSource)
        {
            var environmentVariables = new Dictionary<string, string>();
            if(runtimeLogLevel.HasValue && configSource == LogConfigSource.DotnetEnvironment)
            {
                environmentVariables["AWS_LAMBDA_HANDLER_LOG_LEVEL"] = runtimeLogLevel.Value.ToString().ToLowerInvariant();
            }
            await UpdateHandlerAsync(lambdaClient, handler, environmentVariables, configSource == LogConfigSource.LambdaAPI ? runtimeLogLevel : null); 

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            Assert.That(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.That(invokeResponse.FunctionError == null);

            var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));

            if (!runtimeLogLevel.HasValue)
                runtimeLogLevel = RuntimeLogLevel.Information;

            if (runtimeLogLevel <= RuntimeLogLevel.Trace)
            {
                Assert.That(log, Does.Contain("A trace log"));
            }
            else
            {
                Assert.That(log, Does.Not.Contain("A trace log"));
            }

            if (runtimeLogLevel <= RuntimeLogLevel.Debug)
            {
                Assert.That(log, Does.Contain("A debug log"));
            }
            else
            {
                Assert.That(log, Does.Not.Contain("A debug log"));
            }

            if (runtimeLogLevel <= RuntimeLogLevel.Information)
            {
                Assert.That(log, Does.Contain("A information log"));
                Assert.That(log, Does.Contain("A stdout info message"));
            }
            else
            {
                Assert.That(log, Does.Not.Contain("A information log"));
                Assert.That(log, Does.Not.Contain("A stdout info message"));
            }

            if (runtimeLogLevel <= RuntimeLogLevel.Warning)
            {
                Assert.That(log, Does.Contain("A warning log"));
            }
            else
            {
                Assert.That(log, Does.Not.Contain("A warning log"));
            }

            if (runtimeLogLevel <= RuntimeLogLevel.Error)
            {
                Assert.That(log, Does.Contain("A error log"));
                Assert.That(log, Does.Contain("A stderror error message"));
            }
            else
            {
                Assert.That(log, Does.Not.Contain("A error log"));
                Assert.That(log, Does.Not.Contain("A stderror error message"));
            }

            if (runtimeLogLevel <= RuntimeLogLevel.Critical)
            {
                Assert.That(log, Does.Contain("A critical log"));
            }
            else
            {
                Assert.That(log, Does.Not.Contain("A critical log"));
            }
        }

        private async Task RunUnformattedLoggingTestAsync(AmazonLambdaClient lambdaClient, string handler)
        {
            var environmentVariables = new Dictionary<string, string>();
            environmentVariables["AWS_LAMBDA_HANDLER_LOG_FORMAT"] = "Unformatted";
            await UpdateHandlerAsync(lambdaClient, handler, environmentVariables);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            Assert.That(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.That(invokeResponse.FunctionError == null);

            var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));

            Assert.That(log, Does.Not.Contain("info\t"));
            Assert.That(log, Does.Not.Contain("warn\t"));
            Assert.That(log, Does.Not.Contain("fail\t"));
            Assert.That(log, Does.Not.Contain("crit\t"));

            Assert.That(log, Does.Contain("A information log"));
            Assert.That(log, Does.Contain("A warning log"));
            Assert.That(log, Does.Contain("A error log"));
            Assert.That(log, Does.Contain("A critical log"));
        }


        private async Task RunTestSuccessAsync(AmazonLambdaClient lambdaClient, string handler, string input, string expectedResponse)
        {
            await UpdateHandlerAsync(lambdaClient, handler);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(input));
            Assert.That(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.That(invokeResponse.FunctionError == null);
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                var responseString = JsonConvert.DeserializeObject<string>(await sr.ReadToEndAsync());
                Assert.Equals(expectedResponse, responseString);
            }
        }


    }
}
