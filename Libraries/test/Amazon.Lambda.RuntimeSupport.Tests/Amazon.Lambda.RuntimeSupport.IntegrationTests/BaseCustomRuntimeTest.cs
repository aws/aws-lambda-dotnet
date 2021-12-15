using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests
{
    public class BaseCustomRuntimeTest
    {
        protected static readonly RegionEndpoint TestRegion = RegionEndpoint.USWest2;
        protected static readonly string LAMBDA_ASSUME_ROLE_POLICY =
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

        protected string FunctionName { get; }
        protected string DeploymentZipKey { get; }
        protected string DeploymentPackageZipRelativePath { get; }
        protected string TestBucketRoot { get; } = "customruntimetest-";
        protected string ExecutionRoleName { get; } 
        protected string ExecutionRoleArn { get; set; }
        private const string TestsProjectDirectoryName = "Amazon.Lambda.RuntimeSupport.Tests";

        protected BaseCustomRuntimeTest(string functionName, string deploymentZipKey, string deploymentPackageZipRelativePath)
        {
            FunctionName = functionName;
            ExecutionRoleName = FunctionName;
            DeploymentZipKey = deploymentZipKey;
            DeploymentPackageZipRelativePath = deploymentPackageZipRelativePath;
        }

        /// <summary>
        /// Clean up all test resources.
        /// Also cleans up any resources that might be left from previous failed/interrupted tests.
        /// </summary>
        /// <param name="s3Client"></param>
        /// <param name="lambdaClient"></param>
        /// <returns></returns>
        protected async Task CleanUpTestResources(AmazonS3Client s3Client, AmazonLambdaClient lambdaClient,
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

        protected async Task<bool> PrepareTestResources(IAmazonS3 s3Client, IAmazonLambda lambdaClient,
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
        private async Task<bool> ValidateAndSetIamRoleArn(IAmazonIdentityManagementService iamClient)
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

        private async Task CreateBucketWithDeploymentZipAsync(IAmazonS3 s3Client, string bucketName)
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
                await Task.Delay(10000);
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

        private async Task DeleteDeploymentZipAndBucketAsync(IAmazonS3 s3Client, string bucketName)
        {
            try
            {
                await Amazon.S3.Util.AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName);
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

        protected async Task<InvokeResponse> InvokeFunctionAsync(IAmazonLambda lambdaClient, string payload)
        {
            var request = new InvokeRequest
            {
                FunctionName = FunctionName,
                Payload = payload,
                LogType = LogType.Tail
            };
            return await lambdaClient.InvokeAsync(request);
        }

        protected async Task UpdateHandlerAsync(IAmazonLambda lambdaClient, string handler, Dictionary<string, string> environmentVariables = null)
        {
            var updateFunctionConfigurationRequest = new UpdateFunctionConfigurationRequest
            {
                FunctionName = FunctionName,
                Handler = handler,
                Environment = new Model.Environment
                {
                    IsVariablesSet = true,
                    Variables = environmentVariables ?? new Dictionary<string, string>()
                }
            };
            await lambdaClient.UpdateFunctionConfigurationAsync(updateFunctionConfigurationRequest);

            // Wait for eventual consistency of function change.
            var getConfigurationRequest = new GetFunctionConfigurationRequest { FunctionName = FunctionName };
            GetFunctionConfigurationResponse getConfigurationResponse = null;
            do
            {
                await Task.Delay(1000);
                getConfigurationResponse = await lambdaClient.GetFunctionConfigurationAsync(getConfigurationRequest);
            } while (getConfigurationResponse.State == State.Pending);
            await Task.Delay(1000);
        }

        protected async Task CreateFunctionAsync(IAmazonLambda lambdaClient, string bucketName)
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
                Timeout = 30,
                Runtime = Runtime.ProvidedAl2,
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
            await Task.Delay(5000);

            if (!created)
            {
                throw new Exception($"Timed out trying to create Lambda function {FunctionName}");
            }
        }

        protected async Task DeleteFunctionIfExistsAsync(IAmazonLambda lambdaClient)
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
        private string GetDeploymentZipPath()
        {
            var testsProjectDirectory = FindUp(System.Environment.CurrentDirectory, TestsProjectDirectoryName, true);
            if (string.IsNullOrEmpty(testsProjectDirectory))
            {
                throw new NoDeploymentPackageFoundException();
            }

            var deploymentZipFile = Path.Combine(testsProjectDirectory, DeploymentPackageZipRelativePath.Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(deploymentZipFile))
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

        protected class NoDeploymentPackageFoundException : Exception
        {

        }
    }
}
