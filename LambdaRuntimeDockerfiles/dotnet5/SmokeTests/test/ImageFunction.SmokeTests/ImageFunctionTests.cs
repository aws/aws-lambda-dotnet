﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        private readonly string _executionRoleName;
        private string _executionRoleArn;

        private static readonly string LambdaAssumeRolePolicy =
            @"
            {
              ""Version"": ""2012-10-17"",
              ""Statement"": [
                {
                  ""Sid"": """",
                  ""Effect"": ""Allow"",
                  ""Principal"": {
                    ""Service"": ""lambda.amazonaws.com""
                  },
                  ""Action"": ""sts:AssumeRole""
                }
              ]
            }".Trim();

        private readonly string _functionName;
        private readonly string _imageUri;
        private const string TestIdentifier = "image-function-tests";
        private bool _disposed = false;

        public ImageFunctionTests()
        {
            _executionRoleName = $"{TestIdentifier}-{Guid.NewGuid()}";
            _functionName = $"{TestIdentifier}-{Guid.NewGuid()}";
            _lambdaClient = new AmazonLambdaClient(TestRegion);
            _iamClient = new AmazonIdentityManagementServiceClient(TestRegion);
            _imageUri = Environment.GetEnvironmentVariable("AWS_LAMBDA_IMAGE_URI");

            Assert.NotNull(_imageUri);

            SetupAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData("ImageFunction::ImageFunction.Function::ToUpper", "message", "MESSAGE")]
        [InlineData("ImageFunction::ImageFunction.Function::Ping", "ping", "pong")]
        [InlineData("ImageFunction::ImageFunction.Function::HttpsWorksAsync", "", "SUCCESS")]
        [InlineData("ImageFunction::ImageFunction.Function::VerifyLambdaContext", "", "SUCCESS")]
        public async Task SuccessfulTests(string handler, string input, string expectedResponse)
        {
            await UpdateHandlerAsync(handler);

            var payload = JsonConvert.SerializeObject(input);
            var invokeResponse = await InvokeFunctionAsync(payload);

            Assert.True(invokeResponse.HttpStatusCode == HttpStatusCode.OK);
            Assert.True(invokeResponse.FunctionError == null);

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
            "Response payload size (7340034 bytes) exceeded maximum allowed payload size (6291556 bytes).")]
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

        private async Task UpdateHandlerAsync(string handler)
        {
            var request = new UpdateFunctionConfigurationRequest
            {
                FunctionName = _functionName,
                ImageConfig = new ImageConfig()
                {
                    Command = {handler},
                }
            };
            await _lambdaClient.UpdateFunctionConfigurationAsync(request);
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
            await CreateRoleAsync();
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
                        PackageType = PackageType.Image
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

        private async Task CreateRoleAsync()
        {
            var response = await _iamClient.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = _executionRoleName,
                Description = $"Test role for {TestIdentifier}.",
                AssumeRolePolicyDocument = LambdaAssumeRolePolicy
            });
            _executionRoleArn = response.Role.Arn;

            // Wait  5 seconds to let execution role propagate
            await Task.Delay(5000);
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
            await DeleteRoleIfExistsAsync();
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

        private async Task DeleteRoleIfExistsAsync()
        {
            try
            {
                await _iamClient.DeleteRoleAsync(new DeleteRoleRequest
                {
                    RoleName = _executionRoleName
                });
            }
            catch (NoSuchEntityException)
            {
                // No action required
            }
        }

        #endregion
    }
}