using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using Amazon.Lambda;
using Amazon.Lambda.Model;

using Amazon.Lambda.Tools;
using Amazon.Lambda.Tools.Commands;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;

using Amazon.SQS;

using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;


namespace Amazon.Lambda.Tools.Test
{
    public class DeployTest : IDisposable
    {
        const string LAMBDATOOL_TEST_ROLE = "lambdatools-test-role2";
        IAmazonIdentityManagementService _iamCient;
        string _roleArn;
        public DeployTest()
        {
            this._iamCient = new AmazonIdentityManagementServiceClient();

            Task.Run(async () => 
            {
                try
                {
                    this._roleArn = (await this._iamCient.GetRoleAsync(new GetRoleRequest { RoleName = LAMBDATOOL_TEST_ROLE })).Role.Arn;
                }
                catch (NoSuchEntityException)
                {
                    // Role is not found so create a role with no permissions other then Lambda can assume the role. 
                    // The role is deleted and reused in other runs of the test to make the test run faster.
                    this._roleArn = new RoleHelper(this._iamCient).CreateDefaultRole(LAMBDATOOL_TEST_ROLE, "arn:aws:iam::aws:policy/PowerUserAccess");
                }
            }).Wait();

            
        }



        [Fact]
        public async Task RunDeployCommand()
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;

            var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TestFunction");
            var command = new DeployFunctionCommand(new ConsoleToolLogger(), fullPath, new string[0]);
            command.FunctionName = "test-function-" + DateTime.Now.Ticks;
            command.Handler = "TestFunction::TestFunction.Function::ToUpper";
            command.Timeout = 10;
            command.MemorySize = 512;
            command.Role = this._roleArn;
            command.Configuration = "Release";
            command.TargetFramework = "netcoreapp1.0";
            command.Runtime = "dotnetcore1.0";

            var created = await command.ExecuteAsync();
            try
            {
                Assert.True(created);

                var invokeRequest = new InvokeRequest
                {
                    FunctionName = command.FunctionName,
                    LogType = LogType.Tail,
                    Payload = "\"hello world\""
                };
                var response = await command.LambdaClient.InvokeAsync(invokeRequest);

                var payload = new StreamReader(response.Payload).ReadToEnd();
                var log = System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(response.LogResult));
                Assert.Equal("\"HELLO WORLD\"", payload);
            }
            finally
            {
                if (created)
                {
                    await command.LambdaClient.DeleteFunctionAsync(command.FunctionName);
                }
            }
        }

        [Fact]
        public async Task FixIssueOfDLQBeingCleared()
        {
            var sqsClient = new AmazonSQSClient(RegionEndpoint.USEast2);

            var queueUrl = (await sqsClient.CreateQueueAsync("lambda-test-" + DateTime.Now.Ticks)).QueueUrl;
            var queueArn = (await sqsClient.GetQueueAttributesAsync(queueUrl, new List<string> { "QueueArn" })).QueueARN;
            try
            {

                var assembly = this.GetType().GetTypeInfo().Assembly;

                var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TestFunction");
                var initialDeployCommand = new DeployFunctionCommand(new ConsoleToolLogger(), fullPath, new string[0]);
                initialDeployCommand.FunctionName = "test-function-" + DateTime.Now.Ticks;
                initialDeployCommand.Handler = "TestFunction::TestFunction.Function::ToUpper";
                initialDeployCommand.Timeout = 10;
                initialDeployCommand.MemorySize = 512;
                initialDeployCommand.Role = this._roleArn;
                initialDeployCommand.Configuration = "Release";
                initialDeployCommand.TargetFramework = "netcoreapp1.0";
                initialDeployCommand.Runtime = "dotnetcore1.0";
                initialDeployCommand.DeadLetterTargetArn = queueArn;


                var created = await initialDeployCommand.ExecuteAsync();
                try
                {
                    Assert.True(created);

                    var funcConfig = await initialDeployCommand.LambdaClient.GetFunctionConfigurationAsync(initialDeployCommand.FunctionName);
                    Assert.Equal(queueArn, funcConfig.DeadLetterConfig?.TargetArn);

                    var redeployCommand = new DeployFunctionCommand(new ConsoleToolLogger(), fullPath, new string[0]);
                    redeployCommand.FunctionName = initialDeployCommand.FunctionName;
                    redeployCommand.Configuration = "Release";
                    redeployCommand.TargetFramework = "netcoreapp1.0";
                    redeployCommand.Runtime = "dotnetcore1.0";

                    var redeployed = await redeployCommand.ExecuteAsync();
                    Assert.True(redeployed);

                    funcConfig = await initialDeployCommand.LambdaClient.GetFunctionConfigurationAsync(initialDeployCommand.FunctionName);
                    Assert.Equal(queueArn, funcConfig.DeadLetterConfig?.TargetArn);

                    redeployCommand = new DeployFunctionCommand(new ConsoleToolLogger(), fullPath, new string[0]);
                    redeployCommand.FunctionName = initialDeployCommand.FunctionName;
                    redeployCommand.Configuration = "Release";
                    redeployCommand.TargetFramework = "netcoreapp1.0";
                    redeployCommand.Runtime = "dotnetcore1.0";
                    redeployCommand.DeadLetterTargetArn = "";

                    redeployed = await redeployCommand.ExecuteAsync();
                    Assert.True(redeployed);

                    funcConfig = await initialDeployCommand.LambdaClient.GetFunctionConfigurationAsync(initialDeployCommand.FunctionName);
                    Assert.Null(funcConfig.DeadLetterConfig?.TargetArn);
                }
                finally
                {
                    if (created)
                    {
                        await initialDeployCommand.LambdaClient.DeleteFunctionAsync(initialDeployCommand.FunctionName);
                    }
                }
            }
            finally
            {
                await sqsClient.DeleteQueueAsync(queueUrl);
            }
        }

        [Fact]
        public async Task DeployStepFunctionWithTemplateSubstitution()
        {
            var cfClient = new AmazonCloudFormationClient(RegionEndpoint.USEast2);
            var s3Client = new AmazonS3Client(RegionEndpoint.USEast2);

            var bucketName = "deploy-step-functions-" + DateTime.Now.Ticks;
            await s3Client.PutBucketAsync(bucketName);
            try
            {


                var assembly = this.GetType().GetTypeInfo().Assembly;

                var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TemplateSubstitutionTestProjects/StateMachineDefinitionStringTest");
                var command = new DeployServerlessCommand(new TestToolLogger(), fullPath, new string[0]);
                command.Configuration = "Release";
                command.TargetFramework = "netcoreapp1.0";
                command.StackName = "DeployStepFunctionWithTemplateSubstitution-" + DateTime.Now.Ticks;
                command.S3Bucket = bucketName;
                command.WaitForStackToComplete = true;

                command.TemplateParameters = new Dictionary<string, string> { { "NonExisting", "Parameter" } };

                var created = await command.ExecuteAsync();
                try
                {
                    Assert.True(created);

                    var describeResponse = await cfClient.DescribeStacksAsync(new DescribeStacksRequest
                    {
                        StackName = command.StackName
                    });

                    Assert.Equal(StackStatus.CREATE_COMPLETE, describeResponse.Stacks[0].StackStatus);
                }
                finally
                {
                    if (created)
                    {
                        try
                        {
                            var deleteCommand = new DeleteServerlessCommand(new ConsoleToolLogger(), fullPath, new string[0]);
                            deleteCommand.StackName = command.StackName;
                            await deleteCommand.ExecuteAsync();
                        }
                        catch
                        {
                            // Bury exception because we don't want to lose any exceptions during the deploy stage.
                        }
                    }
                }
            }
            finally
            {
                await AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName);
            }
        }

        [Fact]
        public void ValidateCompatibleLambdaRuntimesAndTargetFrameworks()
        {
            // Validate that newer versions of the framework then what the current and possible future lambda runtimes throw an error.
            Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore1.0", "netcoreapp1.0");
            Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore1.1", "netcoreapp1.0");
            Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore1.1", "netcoreapp1.1");
            Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore2.0", "netcoreapp1.0");
            Assert.Throws(typeof(LambdaToolsException), (() => Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore1.0", "netcoreapp1.1")));
            Assert.Throws(typeof(LambdaToolsException), (() => Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore1.0", "netcoreapp2.0")));
            Assert.Throws(typeof(LambdaToolsException), (() => Utilities.ValidateTargetFrameworkAndLambdaRuntime("dotnetcore1.1", "netcoreapp2.0")));
        }

        [Fact]
        public async Task TestServerlessPackage()
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;

            var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TestWebApp");
            var command = new PackageCICommand(new ConsoleToolLogger(), fullPath, new string[0]);
            command.Region = "us-west-2";
            command.Configuration = "Release";
            command.TargetFramework = "netcoreapp2.0";
            command.CloudFormationTemplate = "serverless.template";
            command.CloudFormationOutputTemplate = Path.Combine(Path.GetTempPath(),  "output-serverless.template");
            command.S3Bucket = "serverless-package-test-" + DateTime.Now.Ticks;

            if (File.Exists(command.CloudFormationOutputTemplate))
                File.Delete(command.CloudFormationOutputTemplate);


            await command.S3Client.PutBucketAsync(command.S3Bucket);
            try
            {
                Assert.True(await command.ExecuteAsync());
                Assert.True(File.Exists(command.CloudFormationOutputTemplate));
            }
            finally
            {
                await AmazonS3Util.DeleteS3BucketWithObjectsAsync(command.S3Client, command.S3Bucket);
            }

        }

        #region IDisposable Support
        private bool disposedValue = false;    
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this._iamCient.Dispose();                    
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
