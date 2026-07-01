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
        var cloudFormationClient = new AmazonCloudFormationClient(Amazon.RegionEndpoint.USWest2);
        _cloudFormationHelper = new CloudFormationHelper(cloudFormationClient);
        _s3Helper = new S3Helper(new AmazonS3Client(Amazon.RegionEndpoint.USWest2));
        LambdaHelper = new LambdaHelper(new AmazonLambdaClient(Amazon.RegionEndpoint.USWest2), cloudFormationClient);
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
        
        // There are 14 Lambda functions in TestCustomAuthorizerApp:
        // CustomAuthorizer, CustomAuthorizerV1, RestApiAuthorizer, SimpleAuthorizer, SimpleRestAuthorizer,
        // ProtectedEndpoint, GetUserInfo, HealthCheck, RestUserInfo, HttpApiV1UserInfo, IHttpResultUserInfo, NonStringUserInfo,
        // SimpleHttpApiUserInfo, SimpleRestApiUserInfo
        Assert.Equal(14, LambdaFunctions.Count);

        await LambdaHelper.WaitTillNotPending(LambdaFunctions.Where(x => x.Name != null).Select(x => x.Name!).ToList());

        // CloudFormation reports CREATE_COMPLETE before API Gateway has fully propagated the deployed
        // stage and the Lambda authorizer invoke permissions to the edge. During that window, requests
        // on the authorizer "allow" path can transiently return 403. REST APIs (v1) settle slower than
        // HTTP APIs (v2), so poll a known allow-path endpoint on each API until it serves traffic
        // correctly rather than relying on a fixed sleep.
        await WarmUpApisAsync();
    }

    /// <summary>
    /// Sends an authenticated GET (valid-token) to <paramref name="url"/>, retrying on a transient 403
    /// from API Gateway. A freshly deployed stage can briefly 403 on the authorizer "allow" path until
    /// the Lambda authorizer wiring finishes propagating; this resends until a stable non-403 response
    /// (200 or 401) is returned. Use this for any test that asserts an authorized request succeeds.
    /// </summary>
    public Task<HttpResponseMessage> GetWithValidTokenAsync(string url)
    {
        return RetryHelper.SendWithRetryOnForbiddenAsync(HttpClient, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "valid-token");
            return request;
        });
    }

    /// <summary>
    /// Polls every authorizer "allow path" endpoint on each deployed API until the custom authorizer
    /// is fully wired and the request succeeds (or a 401 from the backend), confirming the API is
    /// serving traffic before the test suite runs.
    ///
    /// Every distinct authorizer must be warmed individually: API Gateway propagates each authorizer's
    /// invoke wiring separately, so warming one endpoint does not guarantee a sibling authorizer on the
    /// same API is ready. The REST API endpoints are listed first because REST (v1) stages settle slower
    /// than HTTP (v2).
    /// </summary>
    private async Task WarmUpApisAsync()
    {
        var timeout = TimeSpan.FromMinutes(2);
        var pollInterval = TimeSpan.FromSeconds(5);

        // One representative allow-path endpoint per distinct authorizer. A warmed-up authorizer returns
        // a non-403 response on the allow path: either 200 (context present) or 401 (backend rejects
        // missing context). A 403 means API Gateway could not yet invoke/attach the authorizer, so keep
        // waiting.
        var allowPaths = new[]
        {
            $"{RestApiUrl}/api/rest-user-info",          // RestApiAuthorizer (REST API token authorizer)
            $"{RestApiUrl}/api/simple-restapi-user-info",// SimpleRestAuthorizer (IAuthorizerResult REST authorizer)
            $"{HttpApiUrl}/api/user-info",               // CustomAuthorizer (HTTP API v2)
            $"{HttpApiUrl}/api/http-v1-user-info",       // CustomAuthorizerV1 (HTTP API v1)
            $"{HttpApiUrl}/api/simple-httpapi-user-info" // SimpleAuthorizer (IAuthorizerResult HTTP authorizer)
        };

        async Task<bool> EndpointIsReady(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "valid-token");
            var response = await HttpClient.SendAsync(request);
            return response.StatusCode != System.Net.HttpStatusCode.Forbidden;
        }

        foreach (var url in allowPaths)
        {
            var ready = await RetryHelper.WaitForConditionAsync(
                () => EndpointIsReady(url), timeout, pollInterval);
            Console.WriteLine($"[IntegrationTest] Warm-up for '{url}' {(ready ? "succeeded" : "timed out")}.");
        }
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
