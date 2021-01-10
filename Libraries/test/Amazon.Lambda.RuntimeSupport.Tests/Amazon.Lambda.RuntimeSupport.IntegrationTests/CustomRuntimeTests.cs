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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    public class CustomRuntimeTests
    {
        private static readonly RegionEndpoint TestRegion = RegionEndpoint.USWest2;
        private static readonly string LAMBDA_ASSUME_ROLE_POLICY =
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
        }
        ".Trim();

        private const string ExecutionRoleName = "runtimesupporttestingrole";
        private const string TestBucketRoot = "runtimesupporttesting-";
        private const string FunctionName = "CustomRuntimeFunctionTest";
        private const string DeploymentZipKey = "CustomRuntimeFunctionTest.zip";
        private const string DeploymentPackageZipRelativePath = @"CustomRuntimeFunctionTest\bin\Release\netcoreapp2.2\CustomRuntimeFunctionTest.zip";
        private const string TestsProjectDirectoryName = "Amazon.Lambda.RuntimeSupport.Tests";

        private static string ExecutionRoleArn { get; set; }

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
                    await RunTestExceptionAsync(lambdaClient, "TooLargeResponseBodyAsync", "", "Function.ResponseSizeTooLarge", "Response payload size (7340060 bytes) exceeded maximum allowed payload size (6291556 bytes).");
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
            }
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

        /// <summary>
        /// Clean up all test resources.
        /// Also cleans up any resources that might be left from previous failed/interrupted tests.
        /// </summary>
        /// <param name="s3Client"></param>
        /// <param name="lambdaClient"></param>
        /// <returns></returns>
        private async Task CleanUpTestResources(AmazonS3Client s3Client, AmazonLambdaClient lambdaClient,
            AmazonIdentityManagementServiceClient iamClient, bool roleAlreadyExisted)
        {
            await DeleteFunctionIfExistsAsync(lambdaClient);

            var listBucketsResponse = await s3Client.ListBucketsAsync();
            foreach (var bucket in listBucketsResponse.Buckets)
            {
                if (bucket.BucketName.StartsWith(TestBucketRoot))
                {
                    await DeleteDeploymentZipAndBucketAsync(s3Client, bucket.BucketName);
                }
            }

            if (!roleAlreadyExisted)
            {
                try
                {
                    var deleteRoleRequest = new DeleteRoleRequest
                    {
                        RoleName = ExecutionRoleName
                    };
                    await iamClient.DeleteRoleAsync(deleteRoleRequest);
                }
                catch (Exception)
                {
                    // no problem - it's best effort
                }
            }
        }

        private async Task<bool> PrepareTestResources(AmazonS3Client s3Client, AmazonLambdaClient lambdaClient,
            AmazonIdentityManagementServiceClient iamClient)
        {
            var roleAlreadyExisted = await ValidateAndSetIamRoleArn(iamClient);

            var testBucketName = TestBucketRoot + Guid.NewGuid().ToString();
            await CreateBucketWithDeploymentZipAsync(s3Client, testBucketName);
            await CreateFunctionAsync(lambdaClient, testBucketName);

            return roleAlreadyExisted;
        }

        /// <summary>
        /// Create the role if it's not there already.
        /// Return true if it already existed.
        /// </summary>
        /// <returns></returns>
        private static async Task<bool> ValidateAndSetIamRoleArn(AmazonIdentityManagementServiceClient iamClient)
        {
            var getRoleRequest = new GetRoleRequest
            {
                RoleName = ExecutionRoleName
            };
            try
            {
                ExecutionRoleArn = (await iamClient.GetRoleAsync(getRoleRequest)).Role.Arn;
                return true;
            }
            catch (NoSuchEntityException)
            {
                // create the role
                var createRoleRequest = new CreateRoleRequest
                {
                    RoleName = ExecutionRoleName,
                    Description = "Test role for CustomRuntimeTests.",
                    AssumeRolePolicyDocument = LAMBDA_ASSUME_ROLE_POLICY
                };
                ExecutionRoleArn = (await iamClient.CreateRoleAsync(createRoleRequest)).Role.Arn;

                // Wait for role to propagate.
                await Task.Delay(10000);
                return false;
            }
        }

        private async Task CreateBucketWithDeploymentZipAsync(AmazonS3Client s3Client, string bucketName)
        {
            // create bucket if it doesn't exist
            var listBucketsResponse = await s3Client.ListBucketsAsync();
            if (listBucketsResponse.Buckets.Find((bucket) => bucket.BucketName == bucketName) == null)
            {
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = bucketName
                };
                await s3Client.PutBucketAsync(putBucketRequest);
            }

            // write or overwrite deployment package
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = DeploymentZipKey,
                FilePath = GetDeploymentZipPath()
            };
            await s3Client.PutObjectAsync(putObjectRequest);

            // Wait for bucket to propagate.
            await Task.Delay(5000);
        }

        private async Task DeleteDeploymentZipAndBucketAsync(AmazonS3Client s3Client, string bucketName)
        {
            // Delete the deployment zip.
            // This is idempotent - it works even if the object is not there.
            var deleteObjectRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = DeploymentZipKey
            };
            await s3Client.DeleteObjectAsync(deleteObjectRequest);

            // Delete the bucket.
            // Make idempotent by checking exception.
            var deleteBucketRequest = new DeleteBucketRequest
            {
                BucketName = bucketName
            };
            try
            {
                await s3Client.DeleteBucketAsync(deleteBucketRequest);
            }
            catch (AmazonS3Exception e)
            {
                // If it's just telling us the bucket's not there then continue, otherwise throw.
                if (!e.Message.Contains("The specified bucket does not exist"))
                {
                    throw;
                }
            }
        }

        private async Task<InvokeResponse> InvokeFunctionAsync(AmazonLambdaClient lambdaClient, string payload)
        {
            var request = new InvokeRequest
            {
                FunctionName = FunctionName,
                Payload = payload
            };
            return await lambdaClient.InvokeAsync(request);
        }

        private static async Task UpdateHandlerAsync(AmazonLambdaClient lambdaClient, string handler)
        {
            var updateFunctionConfigurationRequest = new UpdateFunctionConfigurationRequest
            {
                FunctionName = FunctionName,
                Handler = handler
            };
            await lambdaClient.UpdateFunctionConfigurationAsync(updateFunctionConfigurationRequest);
        }

        private static async Task CreateFunctionAsync(AmazonLambdaClient lambdaClient, string bucketName)
        {
            await DeleteFunctionIfExistsAsync(lambdaClient);

            var createRequest = new CreateFunctionRequest
            {
                FunctionName = FunctionName,
                Code = new FunctionCode
                {
                    S3Bucket = bucketName,
                    S3Key = DeploymentZipKey
                },
                Handler = "PingAsync",
                MemorySize = 512,
                Runtime = Runtime.Provided,
                Role = ExecutionRoleArn
            };

            var startTime = DateTime.Now;
            var created = false;
            while (DateTime.Now < startTime.AddSeconds(30))
            {
                try
                {
                    await lambdaClient.CreateFunctionAsync(createRequest);
                    created = true;
                    break;
                }
                catch (InvalidParameterValueException ipve)
                {
                    // Wait for the role to be fully propagated through AWS
                    if (ipve.Message == "The role defined for the function cannot be assumed by Lambda.")
                    {
                        await Task.Delay(2000);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            if (!created)
            {
                throw new Exception($"Timed out trying to create Lambda function {FunctionName}");
            }
        }

        private static async Task DeleteFunctionIfExistsAsync(AmazonLambdaClient lambdaClient)
        {
            var request = new DeleteFunctionRequest
            {
                FunctionName = FunctionName
            };

            try
            {
                var response = await lambdaClient.DeleteFunctionAsync(request);
            }
            catch (ResourceNotFoundException)
            {
                // no problem
            }
        }

        /// <summary>
        /// Get the path of the deployment package for testing the custom runtime.
        /// This assumes that the 'dotnet lambda package -c Release' command was run as part of the pre-build of this csproj.
        /// </summary>
        /// <returns></returns>
        private static string GetDeploymentZipPath()
        {
            var testsProjectDirectory = FindUp(System.Environment.CurrentDirectory, TestsProjectDirectoryName, true);
            if(string.IsNullOrEmpty(testsProjectDirectory))
            {
                throw new NoDeploymentPackageFoundException();
            }

            var deploymentZipFile = Path.Combine(testsProjectDirectory, DeploymentPackageZipRelativePath);

            if(!File.Exists(deploymentZipFile))
            {
                throw new NoDeploymentPackageFoundException();
            }

            return deploymentZipFile;
        }

        private static string FindUp(string path, string fileOrDirectoryName, bool combine)
        {
            var fullPath = Path.Combine(path, fileOrDirectoryName);
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                return combine ? fullPath : path;
            }
            else
            {
                var upDirectory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(upDirectory))
                {
                    return null;
                }
                else
                {
                    return FindUp(upDirectory, fileOrDirectoryName, combine);
                }
            }
        }

        class NoDeploymentPackageFoundException : Exception
        {

        }
    }
}
