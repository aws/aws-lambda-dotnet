using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

namespace Amazon.Lambda.Tools.Test
{
    public class DeployTest : IDisposable
    {
        const string LAMBDATOOL_TEST_ROLE = "lambdatools-test-role";
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
                    this._roleArn = new RoleHelper(this._iamCient).CreateDefaultRole(LAMBDATOOL_TEST_ROLE, null);
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
            command.Region = "us-east-1";
            command.Configuration = "Release";
            command.TargetFramework = "netcoreapp1.0";
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
