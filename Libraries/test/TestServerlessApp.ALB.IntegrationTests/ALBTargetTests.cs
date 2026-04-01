using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Xunit;

namespace TestServerlessApp.ALB.IntegrationTests
{
    [Collection("ALB Integration Tests")]
    public class ALBTargetTests
    {
        private readonly ALBIntegrationTestContextFixture _fixture;

        public ALBTargetTests(ALBIntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task InvokeHelloEndpoint_ReturnsSuccessWithBody()
        {
            // ACT
            var response = await _fixture.HttpClient.GetAsync($"http://{_fixture.ALBDnsName}/hello");

            // ASSERT
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Hello from ALB Lambda!", body);
            Assert.Contains("/hello", body);
        }

        [Fact]
        public async Task InvokeHealthEndpoint_ReturnsHealthy()
        {
            // ACT
            var response = await _fixture.HttpClient.GetAsync($"http://{_fixture.ALBDnsName}/health");

            // ASSERT
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("healthy", body);
        }

        [Fact]
        public async Task InvokeUnknownPath_Returns404FromDefaultAction()
        {
            // ACT - The ALB default action returns 404 for unmatched paths
            var response = await _fixture.HttpClient.GetAsync($"http://{_fixture.ALBDnsName}/unknown-path");

            // ASSERT
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("Not Found", body);
        }

        [Fact]
        public async Task VerifyTargetGroupsExist()
        {
            // ACT - Describe all target groups and find ours
            var describeResponse = await _fixture.ELBv2Client.DescribeTargetGroupsAsync(new DescribeTargetGroupsRequest());
            var albTargetGroups = describeResponse.TargetGroups
                .Where(tg => tg.TargetType == TargetTypeEnum.Lambda)
                .ToList();

            // ASSERT - At least our Lambda target groups should exist
            Assert.True(albTargetGroups.Count >= 2, 
                $"Expected at least 2 Lambda target groups, found {albTargetGroups.Count}");
        }
    }
}
