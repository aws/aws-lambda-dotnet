// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Xunit;
using Xunit.Extensions.AssemblyFixture;

namespace TestServerlessApp.IntegrationTests
{
    public class S3EventNotification : IAssemblyFixture<IntegrationTestContextFixture>
    {
        private readonly IntegrationTestContextFixture _fixture;

        public S3EventNotification(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VerifyS3EventNotificationConfiguration()
        {
            // Verify the Lambda function exists in the stack
            var lambdaFunction = _fixture.LambdaFunctions
                .FirstOrDefault(x => string.Equals(x.LogicalId, "S3EventHandler"));
            Assert.NotNull(lambdaFunction);
            Assert.NotNull(lambdaFunction.Name);

            // Verify S3 bucket notification is configured correctly
            var notificationConfig = await _fixture.S3HelperInstance
                .GetBucketNotificationAsync(_fixture.TestS3BucketName);

            var lambdaConfigs = notificationConfig.LambdaFunctionConfigurations;
            Assert.Single(lambdaConfigs);

            var config = lambdaConfigs.First();

            // Verify the notification points to the correct Lambda function ARN
            Assert.Contains(lambdaFunction.Name, config.FunctionArn);

            // Verify the event type is s3:ObjectCreated:*
            Assert.Single(config.Events);
            Assert.Equal(EventType.ObjectCreatedAll, config.Events.First());

            // Verify the suffix filter is .json
            var filterRules = config.Filter.S3KeyFilter.FilterRules;
            Assert.Single(filterRules);
            var suffixRule = filterRules.First(r => string.Equals(r.Name, "suffix", System.StringComparison.OrdinalIgnoreCase));
            Assert.Equal(".json", suffixRule.Value);
        }
    }
}
