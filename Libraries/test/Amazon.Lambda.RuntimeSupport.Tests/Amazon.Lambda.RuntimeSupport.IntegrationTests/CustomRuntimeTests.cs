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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    public class CustomRuntimeTests : BaseCustomRuntimeTest
    {
        public CustomRuntimeTests() 
            : base("CustomRuntimeFunctionTest-" + DateTime.Now.Ticks, "CustomRuntimeFunctionTest.zip", @"CustomRuntimeFunctionTest\bin\Release\net6.0\CustomRuntimeFunctionTest.zip")
        {
        }

#if SKIP_RUNTIME_SUPPORT_INTEG_TESTS
        [Fact(Skip = "Skipped intentionally by setting the SkipRuntimeSupportIntegTests build parameter.")]
#else
        [Fact]
#endif
        public async Task TestAllHandlersAsync()
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

                    await RunTestSuccessAsync(lambdaClient, "LoggingStressTest", "not-used", "LoggingStressTest-success");
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", null);
                    await RunLoggingTestAsync(lambdaClient, "LoggingTest", "debug");
                    await RunUnformattedLoggingTestAsync(lambdaClient, "LoggingTest");

                    await RunTestSuccessAsync(lambdaClient, "ToUpperAsync", "message", "ToUpperAsync-MESSAGE");
                    await RunTestSuccessAsync(lambdaClient, "PingAsync", "ping", "PingAsync-pong");
                    await RunTestSuccessAsync(lambdaClient, "HttpsWorksAsync", "", "HttpsWorksAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "CertificateCallbackWorksAsync", "", "CertificateCallbackWorksAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "NetworkingProtocolsAsync", "", "NetworkingProtocolsAsync-SUCCESS");
                    await RunTestSuccessAsync(lambdaClient, "HandlerEnvVarAsync", "", "HandlerEnvVarAsync-HandlerEnvVarAsync");
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

        private async Task RunTestExceptionAsync(AmazonLambdaClient lambdaClient, string handler, string input,
            string expectedErrorType, string expectedErrorMessage)
        {
            await UpdateHandlerAsync(lambdaClient, handler);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(input));
            Assert.True(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError != null);
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                JObject exception = (JObject)JsonConvert.DeserializeObject(await sr.ReadToEndAsync());
                Assert.Equal(expectedErrorType, exception["errorType"].ToString());
                Assert.Equal(expectedErrorMessage, exception["errorMessage"].ToString());

                var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));
                var logExpectedException = expectedErrorType != "Function.ResponseSizeTooLarge" ? expectedErrorType : "RuntimeApiClientException";
                Assert.Contains(logExpectedException, log);
            }
        }

        private async Task RunLoggingTestAsync(AmazonLambdaClient lambdaClient, string handler, string logLevel)
        {
            var environmentVariables = new Dictionary<string, string>();
            if(!string.IsNullOrEmpty(logLevel))
            {
                environmentVariables["AWS_LAMBDA_HANDLER_LOG_LEVEL"] = logLevel;
            }
            await UpdateHandlerAsync(lambdaClient, handler, environmentVariables);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            Assert.True(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError == null);

            var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));

            Assert.Contains("info\tA information log", log);
            Assert.Contains("warn\tA warning log", log);
            Assert.Contains("fail\tA error log", log);
            Assert.Contains("crit\tA critical log", log);

            Assert.Contains("info\tA stdout info message", log);

            Assert.Contains("fail\tA stderror error message", log);

            if (string.IsNullOrEmpty(logLevel))
            {
                Assert.DoesNotContain($"a {logLevel} log".ToLower(), log.ToLower());
            }
            else
            {
                Assert.Contains($"a {logLevel} log".ToLower(), log.ToLower());
            }
        }

        private async Task RunUnformattedLoggingTestAsync(AmazonLambdaClient lambdaClient, string handler)
        {
            var environmentVariables = new Dictionary<string, string>();
            environmentVariables["AWS_LAMBDA_HANDLER_LOG_FORMAT"] = "Unformatted";
            await UpdateHandlerAsync(lambdaClient, handler, environmentVariables);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(""));
            Assert.True(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError == null);

            var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(invokeResponse.LogResult));

            Assert.DoesNotContain("info\t", log);
            Assert.DoesNotContain("warn\t", log);
            Assert.DoesNotContain("fail\t", log);
            Assert.DoesNotContain("crit\t", log);

            Assert.Contains("A information log", log);
            Assert.Contains("A warning log", log);
            Assert.Contains("A error log", log);
            Assert.Contains("A critical log", log);
        }


        private async Task RunTestSuccessAsync(AmazonLambdaClient lambdaClient, string handler, string input, string expectedResponse)
        {
            await UpdateHandlerAsync(lambdaClient, handler);

            var invokeResponse = await InvokeFunctionAsync(lambdaClient, JsonConvert.SerializeObject(input));
            Assert.True(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError == null);
            using (var responseStream = invokeResponse.Payload)
            using (var sr = new StreamReader(responseStream))
            {
                var responseString = JsonConvert.DeserializeObject<string>(await sr.ReadToEndAsync());
                Assert.Equal(expectedResponse, responseString);
            }
        }


    }
}
