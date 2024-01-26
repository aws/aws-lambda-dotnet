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
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Environment = System.Environment;

namespace ImageFunction.SmokeTests
{
    public class ImageFunctionTests : IDisposable
    {
        private static readonly RegionEndpoint TestRegion = RegionEndpoint.USWest2;
        private readonly AmazonLambdaClient _lambdaClient;
        private readonly AmazonIdentityManagementServiceClient _iamClient;
        private string _executionRoleArn;

        private readonly string _functionName;
        private readonly string _imageUri;
        private const string TestIdentifier = "image-function-tests";
        private bool _disposed = false;

        public ImageFunctionTests()
        {
            _functionName = $"{TestIdentifier}-{Guid.NewGuid()}";
            var lambdaConfig = new AmazonLambdaConfig()
            {
                RegionEndpoint = TestRegion
            };
            _lambdaClient = new AmazonLambdaClient(lambdaConfig);
            _iamClient = new AmazonIdentityManagementServiceClient(TestRegion);
            _executionRoleArn = Environment.GetEnvironmentVariable("AWS_LAMBDA_SMOKETESTS_LAMBDA_ROLE");
            _imageUri = Environment.GetEnvironmentVariable("AWS_LAMBDA_IMAGE_URI");

            Assert.NotNull(_executionRoleArn);
            Assert.NotNull(_imageUri);

            SetupAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData("ImageFunction::ImageFunction.Function::ToUpper", "message", "MESSAGE")]
        [InlineData("ImageFunction::ImageFunction.Function::Ping", "ping", "pong")]
        [InlineData("ImageFunction::ImageFunction.Function::HttpsWorksAsync", "", "SUCCESS")]
        [InlineData("ImageFunction::ImageFunction.Function::VerifyLambdaContext", "", "SUCCESS")]
        [InlineData("ImageFunction::ImageFunction.Function::VerifyTzData", "", "SUCCESS")]
        public async Task SuccessfulTests(string handler, string input, string expectedResponse)
        {
            await UpdateHandlerAsync(handler);

            var payload = JsonConvert.SerializeObject(input);
            var invokeResponse = await InvokeFunctionAsync(payload);

            Assert.True(invokeResponse.HttpStatusCode == HttpStatusCode.OK);
            if(invokeResponse.FunctionError != null)
            {
                throw new Exception($"Lambda function {handler} failed: {invokeResponse.FunctionError}");
            }    

            await using var responseStream = invokeResponse.Payload;
            using var sr = new StreamReader(responseStream);
            var responseString = JsonConvert.DeserializeObject<string>(await sr.ReadToEndAsync());
            Assert.Equal(expectedResponse, responseString);
        }

        [Theory]
        [InlineData("ImageFunction::ImageFunction.Function::ThrowExceptionAsync", "", "Exception", "Exception thrown from an async handler.")]
        [InlineData("ImageFunction::ImageFunction.Function::ThrowException", "", "Exception", "Exception thrown from a synchronous handler.")]
        [InlineData("ImageFunction::ImageFunction.Function::AggregateExceptionNotUnwrappedAsync", "", "AggregateException", "AggregateException thrown from an async handler.")]
        [InlineData("ImageFunction::ImageFunction.Function::AggregateExceptionNotUnwrapped", "", "AggregateException", "AggregateException thrown from a synchronous handler.")]
        [InlineData("ImageFunction::ImageFunction.Function::TooLargeResponseBody", "", "Function.ResponseSizeTooLarge",
            "Response payload size exceeded maximum allowed payload size (6291556 bytes).")]
        public async Task ExceptionTests(string handler, string input, string expectedErrorType, string expectedErrorMessage)
        {
            await UpdateHandlerAsync(handler);

            var payload = JsonConvert.SerializeObject(input);
            var invokeResponse = await InvokeFunctionAsync(payload);
            Assert.True(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError != null);

            await using var responseStream = invokeResponse.Payload;
            using var sr = new StreamReader(responseStream);
            var exception = (JObject)JsonConvert.DeserializeObject(await sr.ReadToEndAsync());
            Assert.Equal(expectedErrorType, exception["errorType"].ToString());
            Assert.Equal(expectedErrorMessage, exception["errorMessage"].ToString());
        }

        /// <summary>
        /// This test is checking the logic added to the bootstrap.sh to change the SSL_CERT_FILE
        /// environment variable for AL2023.
        /// </summary>
        /// <param name="envName"></param>
        /// <param name="expectedValue"></param>
        /// <param name="setValue"></param>
        /// <returns></returns>
        [Theory]
#if NET8_0_OR_GREATER
        [InlineData("SSL_CERT_FILE", "\"/tmp/noop\"", null)]
        [InlineData("SSL_CERT_FILE", "\"/tmp/my-bundle\"", "/tmp/my-bundle")]
#else
        [InlineData("SSL_CERT_FILE", "", null)]
        [InlineData("SSL_CERT_FILE", "/tmp/my-bundle", "/tmp/my-bundle")]
#endif
        public async Task CheckEnvironmentVariable(string envName, string expectedValue, string setValue)
        {
            var envVariables = new Dictionary<string, string>();
            if(setValue != null)
            {
                envVariables[envName] = setValue;
            }    

            await UpdateHandlerAsync("ImageFunction::ImageFunction.Function::GetEnvironmentVariable", envVariables);

            var payload = JsonConvert.SerializeObject(envName);
            var invokeResponse = await InvokeFunctionAsync(payload);

            Assert.True(invokeResponse.HttpStatusCode == System.Net.HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError == null, "Failed invoke with error: " + invokeResponse.FunctionError);

            await using var responseStream = invokeResponse.Payload;
            var actualValue = new StreamReader(responseStream).ReadToEnd();

            Assert.Equal(expectedValue, actualValue);
        }

        private async Task UpdateHandlerAsync(string handler, Dictionary<string, string> environmentVariables = null)
        {
            var updateFunctionConfigurationRequest = new UpdateFunctionConfigurationRequest
            {
                FunctionName = _functionName,
                ImageConfig = new ImageConfig()
                {
                    Command = {handler},
                },
                Environment = new Amazon.Lambda.Model.Environment
                {
                    Variables = environmentVariables ?? new Dictionary<string, string>()
                }
            };
            await _lambdaClient.UpdateFunctionConfigurationAsync(updateFunctionConfigurationRequest);

            await WaitUntilHelper.WaitUntil(async () =>
            {
                var getFunctionRequest = new GetFunctionRequest()
                {
                    FunctionName = _functionName
                };
                var getFunctionResponse = await _lambdaClient.GetFunctionAsync(getFunctionRequest);
                return getFunctionResponse.Configuration.LastUpdateStatus != LastUpdateStatus.Successful;
            }, TimeSpan.Zero, TimeSpan.FromMinutes(5), CancellationToken.None);
        }

        private async Task<InvokeResponse> InvokeFunctionAsync(string payload)
        {
            var request = new InvokeRequest
            {
                FunctionName = _functionName,
                Payload = string.IsNullOrEmpty(payload) ? null : payload
            };
            return await _lambdaClient.InvokeAsync(request);
        }

        #region Setup

        private async Task SetupAsync()
        {
            await CreateFunctionAsync();
        }

        private async Task CreateFunctionAsync()
        {
            var tryCount = 3;
            while (true)
            {
                try
                {
                    await _lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
                    {
                        FunctionName = _functionName,
                        Code = new FunctionCode
                        {
                            ImageUri = _imageUri
                        },
                        MemorySize = 512,
                        Role = _executionRoleArn,
                        PackageType = PackageType.Image,
                        Timeout = 30,
                        Architectures = new List<string> {GetArchitecture()}
                    });
                    break;
                }
                catch (InvalidParameterValueException)
                {
                    tryCount--;
                    if (tryCount == 0)
                    {
                        throw;
                    }

                    // Wait another 5 seconds to let execution role propagate
                    await Task.Delay(5000);
                }
            }

            var endTime = DateTime.Now.AddSeconds(30);
            var isActive = false;
            while (DateTime.Now < endTime)
            {
                var response = await _lambdaClient.GetFunctionConfigurationAsync(new GetFunctionConfigurationRequest
                {
                    FunctionName = _functionName
                });
                if (response.State == State.Active)
                {
                    isActive = true;
                    break;
                }

                await Task.Delay(2000);
            }

            if (!isActive)
            {
                throw new Exception($"Timed out trying to create Lambda function {_functionName}");
            }
        }

        private static string GetArchitecture()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case System.Runtime.InteropServices.Architecture.X86:
                case System.Runtime.InteropServices.Architecture.X64:
                    return Amazon.Lambda.Architecture.X86_64;
                case System.Runtime.InteropServices.Architecture.Arm:
                case System.Runtime.InteropServices.Architecture.Arm64:
                    return Amazon.Lambda.Architecture.Arm64;
                default:
                    throw new NotImplementedException(RuntimeInformation.ProcessArchitecture.ToString());
            }
        }

        #endregion

        #region TearDown

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            // Dispose of managed resources here.
            if (disposing)
            {
                TearDownAsync().GetAwaiter().GetResult();

                _lambdaClient?.Dispose();
                _iamClient?.Dispose();
            }

            // Dispose of any unmanaged resources not wrapped in safe handles.
            _disposed = true;
        }

        private async Task TearDownAsync()
        {
            await DeleteFunctionIfExistsAsync();
        }

        private async Task DeleteFunctionIfExistsAsync()
        {
            try
            {
                await _lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest
                {
                    FunctionName = _functionName
                });
            }
            catch (ResourceNotFoundException)
            {
                // No action required
            }
        }

        #endregion
    }
}