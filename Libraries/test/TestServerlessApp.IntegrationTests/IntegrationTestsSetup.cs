using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using Amazon.S3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TestServerlessApp.IntegrationTests.Helpers;

namespace TestServerlessApp.IntegrationTests;

public class IntegrationTestsSetup
{
    private readonly CloudFormationHelper _cloudFormationHelper;
    private readonly S3Helper _s3Helper;

    private string _stackName;
    private string _bucketName;

    public readonly LambdaHelper LambdaHelper;
    public readonly CloudWatchHelper CloudWatchHelper;
    public readonly HttpClient HttpClient;

    public string RestApiUrlPrefix;
    public string HttpApiUrlPrefix;
    public string TestQueueARN;
    public List<LambdaFunction> LambdaFunctions;

    public IntegrationTestsSetup()
    {
        _cloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient(Amazon.RegionEndpoint.USWest2));
        _s3Helper = new S3Helper(new AmazonS3Client(Amazon.RegionEndpoint.USWest2));
        LambdaHelper = new LambdaHelper(new AmazonLambdaClient(Amazon.RegionEndpoint.USWest2));
        CloudWatchHelper = new CloudWatchHelper(new AmazonCloudWatchLogsClient(Amazon.RegionEndpoint.USWest2));
        HttpClient = new HttpClient();
    }
    
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var scriptPath = Path.Combine("..", "..", "..", "DeploymentScript.ps1");
        await CommandLineWrapper.RunAsync($"pwsh {scriptPath}");

        _stackName = GetStackName();
        _bucketName = GetBucketName();
        Assert.That(string.IsNullOrEmpty(_stackName), Is.False);
        Assert.That(string.IsNullOrEmpty(_bucketName), Is.False);

        RestApiUrlPrefix = await _cloudFormationHelper.GetOutputValueAsync(_stackName, "RestApiURL");
        HttpApiUrlPrefix = await _cloudFormationHelper.GetOutputValueAsync(_stackName, "HttpApiURL");
        TestQueueARN = await _cloudFormationHelper.GetOutputValueAsync(_stackName, "TestQueueARN");
        LambdaFunctions = await LambdaHelper.FilterByCloudFormationStackAsync(_stackName);

        var stackStatus = await _cloudFormationHelper.GetStackStatusAsync(_stackName);
        Assert.That(stackStatus, Is.EqualTo(StackStatus.CREATE_COMPLETE));
        Assert.That(await _s3Helper.BucketExistsAsync(_bucketName), Is.True);
        Assert.That(LambdaFunctions.Count, Is.EqualTo(28));
        Assert.That(string.IsNullOrEmpty(RestApiUrlPrefix), Is.False);
        Assert.That(string.IsNullOrEmpty(RestApiUrlPrefix), Is.False);

        await LambdaHelper.WaitTillNotPending(LambdaFunctions.Select(x => x.Name).ToList());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        
        await _cloudFormationHelper.DeleteStackAsync(_stackName);
        Assert.That(await _cloudFormationHelper.IsDeletedAsync(_stackName), Is.True, $"The stack '{_stackName}' still exists and will have to be manually deleted from the AWS console.");

        await _s3Helper.DeleteBucketAsync(_bucketName);
        Assert.That(await _s3Helper.BucketExistsAsync(_bucketName), Is.False, $"The bucket '{_bucketName}' still exists and will have to be manually deleted from the AWS console.");

        var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp", "aws-lambda-tools-defaults.json");
        var token = JObject.Parse(await File.ReadAllTextAsync(filePath));
        token["s3-bucket"] = "test-serverless-app";
        token["stack-name"] = "test-serverless-app";
        await File.WriteAllTextAsync(filePath, token.ToString(Formatting.Indented));
    }

    private string GetStackName()
    {
        var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp", "aws-lambda-tools-defaults.json");
        var token = JObject.Parse(File.ReadAllText(filePath))["stack-name"];
        return token.ToObject<string>();
    }

    private string GetBucketName()
    {
        var filePath = Path.Combine("..", "..", "..", "..", "TestServerlessApp", "aws-lambda-tools-defaults.json");
        var token = JObject.Parse(File.ReadAllText(filePath))["s3-bucket"];
        return token.ToObject<string>();
    }
}