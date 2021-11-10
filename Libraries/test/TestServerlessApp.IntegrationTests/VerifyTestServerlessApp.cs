using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudWatchLogs;
using Amazon.Lambda;
using Amazon.S3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestServerlessApp.IntegrationTests.Helpers;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    public class VerifyTestServerlessApp : IDisposable
    {
        private readonly string _stackName;
        private readonly string _bucketName;

        private readonly CloudFormationHelper _cloudFormationHelper;
        private readonly S3Helper _s3Helper;
        private readonly LambdaHelper _lambdaHelper;
        private readonly CloudWatchHelper _cloudWatchHelper;
        private readonly string _restApiUrlPrefix;
        private readonly string _httpApiUrlPrefix;
        private readonly List<LambdaFunction> _lambdaFunctions;
        private readonly HttpClient _httpClient;

        public VerifyTestServerlessApp()
        {
            _stackName = GetStackName();
            _bucketName = GetBucketName();
            Assert.False(string.IsNullOrEmpty(_stackName));
            Assert.False(string.IsNullOrEmpty(_bucketName));

            _cloudFormationHelper = new CloudFormationHelper(new AmazonCloudFormationClient());
            _s3Helper = new S3Helper(new AmazonS3Client());
            _lambdaHelper = new LambdaHelper(new AmazonLambdaClient());
            _cloudWatchHelper = new CloudWatchHelper(new AmazonCloudWatchLogsClient());
            _restApiUrlPrefix = _cloudFormationHelper.GetOutputValue(_stackName, "RestApiURL").GetAwaiter().GetResult();
            _httpApiUrlPrefix = _cloudFormationHelper.GetOutputValue(_stackName, "HttpApiURL").GetAwaiter().GetResult();
            _lambdaFunctions = _lambdaHelper.FilterByCloudFormationStack(_stackName).GetAwaiter().GetResult();
            _httpClient = new HttpClient();

            Assert.Equal(StackStatus.CREATE_COMPLETE, _cloudFormationHelper.GetStackStatus(_stackName).GetAwaiter().GetResult());
            Assert.True(_s3Helper.BucketExists(_bucketName).GetAwaiter().GetResult());
            Assert.Equal(11, _lambdaFunctions.Count);
            Assert.False(string.IsNullOrEmpty(_restApiUrlPrefix));
            Assert.False(string.IsNullOrEmpty(_httpApiUrlPrefix));
        }

        [Fact]
        public async Task Verify()
        {
            await VerifyGreeter();
            await VerifySimpleCalculator();
            await VerifyComplexCalculator();
        }


        private async Task VerifyGreeter()
        {
            await VerifyGreeterSayHello();
            await VerifyGreeterSayHelloAsync();
        }

        private async Task VerifySimpleCalculator()
        {
            await VerifySimpleCalculatorAdd();
            await VerifySimpleCalculatorSubtract();
            await VerifySimpleCalculatorMultiply();
            await VerifySimpleCalculatorDivideAsync();
            await VerifyPi();
            await VerifyRandom();
            await VerifyRandoms();
        }

        private async Task VerifyComplexCalculator()
        {
            await VerifyComplexCalculatorAdd();
            await VerifyComplexCalculatorSubtract();
        }

        private async Task VerifySimpleCalculatorAdd()
        {
            var response = await _httpClient.GetAsync($"{_restApiUrlPrefix}/SimpleCalculator/Add?x=2&y=4");
            response.EnsureSuccessStatusCode();
            Assert.Equal("6", await response.Content.ReadAsStringAsync());
        }

        private async Task VerifySimpleCalculatorSubtract()
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{_restApiUrlPrefix}/SimpleCalculator/Subtract"),
                Headers = {{ "x", "10" }, {"y", "2"}}
            };
            var response = _httpClient.SendAsync(httpRequestMessage).Result;
            response.EnsureSuccessStatusCode();
            Assert.Equal("8", await response.Content.ReadAsStringAsync());
        }

        private async Task VerifySimpleCalculatorMultiply()
        {
            var response = await _httpClient.GetAsync($"{_restApiUrlPrefix}/SimpleCalculator/Multiply/2/10");
            response.EnsureSuccessStatusCode();
            Assert.Equal("20", await response.Content.ReadAsStringAsync());
        }

        private async Task VerifySimpleCalculatorDivideAsync()
        {
            var response = await _httpClient.GetAsync($"{_restApiUrlPrefix}/SimpleCalculator/DivideAsync/50/5");
            response.EnsureSuccessStatusCode();
            Assert.Equal("10", await response.Content.ReadAsStringAsync());
        }

        private async Task VerifyGreeterSayHello()
        {
            var response = await _httpClient.GetAsync($"{_httpApiUrlPrefix}/Greeter/SayHello?names=Alice&names=Bob");
            response.EnsureSuccessStatusCode();
            var lambdaFunctionName = _lambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "GreeterSayHello"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var logGroupName = _cloudWatchHelper.GetLogGroupName(lambdaFunctionName);
            Assert.True(await _cloudWatchHelper.MessageExistsInRecentLogEvents("Hello Alice", logGroupName, logGroupName));
            Assert.True(await _cloudWatchHelper.MessageExistsInRecentLogEvents("Hello Bob", logGroupName, logGroupName));
        }

        private async Task VerifyGreeterSayHelloAsync()
        {
            var httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{_httpApiUrlPrefix}/Greeter/SayHelloAsync"),
                Headers = {{ "names", new List<string>{"Alice", "Bob"}}}
            };
            var response = _httpClient.SendAsync(httpRequestMessage).Result;
            response.EnsureSuccessStatusCode();
            var lambdaFunctionName = _lambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "GreeterSayHelloAsync"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var logGroupName = _cloudWatchHelper.GetLogGroupName(lambdaFunctionName);
            Assert.True(await _cloudWatchHelper.MessageExistsInRecentLogEvents("Hello Alice, Bob", logGroupName, logGroupName));
        }

        private async Task VerifyPi()
        {
            var lambdaFunctionName = _lambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "PI"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var invokeResponse = await _lambdaHelper.InvokeFunction(lambdaFunctionName);
            Assert.Equal(200, invokeResponse.StatusCode);
            var responsePayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
            Assert.Equal("3.141592653589793", responsePayload);
        }

        private async Task VerifyRandom()
        {
            var lambdaFunctionName = _lambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "Random"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var invokeResponse = await _lambdaHelper.InvokeFunction(lambdaFunctionName, "1000");
            Assert.Equal(200, invokeResponse.StatusCode);
            var responsePayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
            Assert.True(int.TryParse(responsePayload, out var result));
            Assert.True(result < 1000);
        }

        private async Task VerifyRandoms()
        {
            var lambdaFunctionName = _lambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "Randoms"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));
            var invokeResponse = await _lambdaHelper.InvokeFunction(lambdaFunctionName, "{\"count\": 5, \"maxValue\": 1000}");
            Assert.Equal(200, invokeResponse.StatusCode);
            var responsePayload = await new StreamReader(invokeResponse.Payload).ReadToEndAsync();
            var responseArray = responsePayload.Trim(new[] {'[', ']'}).Split(',').Select(int.Parse).ToList();
            Assert.Equal(5, responseArray.Count);
            Assert.True(responseArray.All(x => x < 1000));
        }

        private async Task VerifyComplexCalculatorAdd()
        {
            var response = await _httpClient.PostAsync($"{_httpApiUrlPrefix}/ComplexCalculator/Add", new StringContent("1,2;3,4"));
            response.EnsureSuccessStatusCode();
            var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(4, responseJson["Item1"]);
            Assert.Equal(6, responseJson["Item2"]);
        }

        private async Task VerifyComplexCalculatorSubtract()
        {
            var json = JsonConvert.SerializeObject(new[,] { { 1, 2 }, { 3, 4 } });
            var data = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_httpApiUrlPrefix}/ComplexCalculator/Subtract", data);
            response.EnsureSuccessStatusCode();
            var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(-2, responseJson["Item1"]);
            Assert.Equal(-2, responseJson["Item2"]);
        }

        private string GetStackName()
        {
            var filePath = Path.Combine("..", "..", "..", "parameters.txt");
            return File.ReadAllLines(filePath)[0].Split('=')[1];
        }

        private string GetBucketName()
        {
            var filePath = Path.Combine("..", "..", "..", "parameters.txt");
            return File.ReadAllLines(filePath)[1].Split('=')[1];
        }

        private async Task CleanUp()
        {
            await _cloudFormationHelper.DeleteStack(_stackName);
            Assert.True(await _cloudFormationHelper.IsDeleted(_stackName), $"The stack '{_stackName}' still exists and will have to be manually deleted from the AWS console.");

            await _s3Helper.DeleteBucket(_bucketName);
            Assert.False(await _s3Helper.BucketExists(_bucketName), $"The bucket '{_bucketName}' still exists and will have to be manually deleted from the AWS console.");

            var parametersFilePath = Path.Combine("..", "..", "..", "parameters.txt");
            if (File.Exists(parametersFilePath))
                File.Delete(parametersFilePath);
            Assert.False(File.Exists(parametersFilePath));
        }

        public void Dispose()
        {
            CleanUp().GetAwaiter().GetResult();
        }
    }
}