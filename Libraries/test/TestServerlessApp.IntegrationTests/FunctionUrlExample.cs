// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class FunctionUrlExample
    {
        private readonly IntegrationTestContextFixture _fixture;

        public FunctionUrlExample(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetItems_WithCategory_ReturnsOkWithItems()
        {
            Assert.False(string.IsNullOrEmpty(_fixture.FunctionUrlPrefix), "FunctionUrlPrefix should not be empty. The Function URL was not discovered during setup.");

            var response = await GetWithRetryAsync($"{_fixture.FunctionUrlPrefix}?category=electronics");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            Assert.Equal("electronics", json["category"]?.ToString());
            Assert.NotNull(json["items"]);
            var items = json["items"].ToObject<string[]>();
            Assert.Equal(2, items.Length);
            Assert.Contains("item1", items);
            Assert.Contains("item2", items);
        }

        [Fact]
        public async Task GetItems_LogsToCloudWatch()
        {
            Assert.False(string.IsNullOrEmpty(_fixture.FunctionUrlPrefix), "FunctionUrlPrefix should not be empty. The Function URL was not discovered during setup.");

            var response = await GetWithRetryAsync($"{_fixture.FunctionUrlPrefix}?category=books");
            response.EnsureSuccessStatusCode();

            var lambdaFunctionName = _fixture.LambdaFunctions
                .FirstOrDefault(x => string.Equals(x.LogicalId, "TestServerlessAppFunctionUrlExampleGetItemsGenerated"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));

            var logGroupName = _fixture.CloudWatchHelper.GetLogGroupName(lambdaFunctionName);
            Assert.True(
                await _fixture.CloudWatchHelper.MessageExistsInRecentLogEventsAsync("Getting items for category: books", logGroupName, logGroupName),
                "Expected log message not found in CloudWatch logs");
        }

        [Fact]
        public async Task VerifyFunctionUrlConfig_HasNoneAuthType()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions
                .FirstOrDefault(x => string.Equals(x.LogicalId, "TestServerlessAppFunctionUrlExampleGetItemsGenerated"))?.Name;
            Assert.False(string.IsNullOrEmpty(lambdaFunctionName));

            var functionUrlConfig = await _fixture.LambdaHelper.GetFunctionUrlConfigAsync(lambdaFunctionName);
            Assert.NotNull(functionUrlConfig);
            Assert.Equal("NONE", functionUrlConfig.AuthType.Value);
            Assert.False(string.IsNullOrEmpty(functionUrlConfig.FunctionUrl), "Function URL should not be empty");
            Assert.Contains(".lambda-url.", functionUrlConfig.FunctionUrl);
        }

        private async Task<HttpResponseMessage> GetWithRetryAsync(string url)
        {
            const int maxAttempts = 10;
            HttpResponseMessage response = null;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(attempt * 1000);
                try
                {
                    response = await _fixture.HttpClient.GetAsync(url);

                    // If we get a 403 Forbidden, it may be an eventual consistency issue
                    // with the Function URL permissions propagating.
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        continue;

                    break;
                }
                catch
                {
                    if (attempt + 1 == maxAttempts)
                        throw;
                }
            }

            return response;
        }
    }
}
