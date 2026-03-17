using System.Net.Http;
using Amazon.CloudFormation;
using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using Amazon.S3;
using IntegrationTests.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestCustomAuthorizerApp.IntegrationTests;

public class IntegrationTestContextFixture : IAsyncLifetime
{
    private readonly CloudFormationHelper _cloudFormationHelper;
    private readonly S3Helper _s3Helper;

    private string _stackName = string.Empty;
    private string _bucketName = string.Empty;

    public readonly LambdaHelper LambdaHelper;
    public readonly CloudWatchHelper CloudWatchHelper;
    public readonly HttpClient HttpClient;

    /// <summary>
    /// HTTP API base URL for endpoints attached to AnnotationsHttpApi (no trailing slash)
    /// </summary>
    public string HttpApiUrl = string.Empty;

    /// <summary>
    /// REST API base URL (no trailing slash)
    /// </summary>
    public string RestApiUrl = string.Empty;

    /// <summary>
    /// List of Lambda functions deployed in this stack
    /// </summary>
    public List<LambdaFunction> LambdaFunctions = new();

    public IntegrationTestContextFixture()
    {
        _cloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient(Amazon.RegionEndpoint.USWest2));
        _s3Helper = new S3Helper(new AmazonS3Client(Amazon.RegionEndpoint.USWest2));
        LambdaHelper = new LambdaHelper(new AmazonLambdaClient(Amazon.RegionEndpoint.USWest2));
        CloudWatchHelper = new CloudWatchHelper(new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2));
        HttpClient = new HttpClient();
    }

    public async Task InitializeAsync()
    {
        var scriptPath = Path.Combine("..", "..", "..", "DeploymentScript.ps1");
        Console.WriteLine($"[IntegrationTest] Running deployment script: {scriptPath}");
        await CommandLineWrapper.RunAsync($"pwsh {scriptPath}");
        Console.WriteLine("[IntegrationTest] Deployment script completed successfully.");

        _stackName = GetStackName();
        _bucketName = GetBucketName();
        Console.WriteLine($"[IntegrationTest] Stack name: '{_stackName}', Bucket name: '{_bucketName}'");
        Assert.False(string.IsNullOrEmpty(_stackName), "Stack name should not be empty");
        Assert.False(string.IsNullOrEmpty(_bucketName), "Bucket name should not be empty");

        // Check stack status before querying resources
        var stackStatus = await _cloudFormationHelper.GetStackStatusAsync(_stackName);
        Console.WriteLine($"[IntegrationTest] Stack status after deployment: {stackStatus}");
        Assert.NotNull(stackStatus);
        Assert.Equal(StackStatus.CREATE_COMPLETE, stackStatus);

        // Dynamically construct API URLs from stack resource physical IDs
        // since the serverless.template is managed by the source generator and may not have Outputs
        var region = "us-west-2";
        Console.WriteLine($"[IntegrationTest] Querying stack resources for '{_stackName}'...");
        var httpApiId = await _cloudFormationHelper.GetResourcePhysicalIdAsync(_stackName, "AnnotationsHttpApi");
        var restApiId = await _cloudFormationHelper.GetResourcePhysicalIdAsync(_stackName, "AnnotationsRestApi");
        Console.WriteLine($"[IntegrationTest] AnnotationsHttpApi: {httpApiId}, AnnotationsRestApi: {restApiId}");
        Assert.False(string.IsNullOrEmpty(httpApiId), $"CloudFormation resource 'AnnotationsHttpApi' was not found in stack '{_stackName}'.");
        Assert.False(string.IsNullOrEmpty(restApiId), $"CloudFormation resource 'AnnotationsRestApi' was not found in stack '{_stackName}'.");
        HttpApiUrl = $"https://{httpApiId}.execute-api.{region}.amazonaws.com";
        RestApiUrl = $"https://{restApiId}.execute-api.{region}.amazonaws.com/Prod";
        
        LambdaFunctions = await LambdaHelper.FilterByCloudFormationStackAsync(_stackName);
        Console.WriteLine($"[IntegrationTest] Found {LambdaFunctions.Count} Lambda functions: {string.Join(", ", LambdaFunctions.Select(f => f.Name ?? "(null)"))}");

        Assert.True(await _s3Helper.BucketExistsAsync(_bucketName), $"S3 bucket {_bucketName} should exist");
        
        // There are 10 Lambda functions in TestCustomAuthorizerApp:
        // CustomAuthorizer, CustomAuthorizerV1, RestApiAuthorizer, ProtectedEndpoint, GetUserInfo, HealthCheck, RestUserInfo, HttpApiV1UserInfo, IHttpResultUserInfo, NonStringUserInfo
        Assert.Equal(10, LambdaFunctions.Count);

        await LambdaHelper.WaitTillNotPending(LambdaFunctions.Where(x => x.Name != null).Select(x => x.Name!).ToList());

        // Wait an additional 10 seconds for any other eventual consistency state to finish up.
        await Task.Delay(10000);
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_stackName))
        {
            Console.WriteLine($"[IntegrationTest] Cleaning up stack '{_stackName}'...");
            await _cloudFormationHelper.DeleteStackAsync(_stackName);
            Assert.True(await _cloudFormationHelper.IsDeletedAsync(_stackName), $"The stack '{_stackName}' still exists and will have to be manually deleted from the AWS console.");
        }
        else
        {
            Console.WriteLine("[IntegrationTest] WARNING: Stack name is null/empty, skipping stack deletion.");
        }

        if (!string.IsNullOrEmpty(_bucketName))
        {
            Console.WriteLine($"[IntegrationTest] Cleaning up bucket '{_bucketName}'...");
            await _s3Helper.DeleteBucketAsync(_bucketName);
            Assert.False(await _s3Helper.BucketExistsAsync(_bucketName), $"The bucket '{_bucketName}' still exists and will have to be manually deleted from the AWS console.");
        }
        else
        {
            Console.WriteLine("[IntegrationTest] WARNING: Bucket name is null/empty, skipping bucket deletion.");
        }

        // Reset the aws-lambda-tools-defaults.json to original values
        var filePath = Path.Combine("..", "..", "..", "..", "TestCustomAuthorizerApp", "aws-lambda-tools-defaults.json");
        var token = JObject.Parse(await File.ReadAllTextAsync(filePath));
        token["s3-bucket"] = "test-custom-authorizer-app";
        token["stack-name"] = "test-custom-authorizer";
        await File.WriteAllTextAsync(filePath, token.ToString(Formatting.Indented));
    }

    private string GetStackName()
    {
        var filePath = Path.Combine("..", "..", "..", "..", "TestCustomAuthorizerApp", "aws-lambda-tools-defaults.json");
        var token = JObject.Parse(File.ReadAllText(filePath))["stack-name"];
        return token?.ToObject<string>() ?? string.Empty;
    }

    private string GetBucketName()
    {
        var filePath = Path.Combine("..", "..", "..", "..", "TestCustomAuthorizerApp", "aws-lambda-tools-defaults.json");
        var token = JObject.Parse(File.ReadAllText(filePath))["s3-bucket"];
        return token?.ToObject<string>() ?? string.Empty;
    }
}
