// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Amazon.Lambda.Model;
using Xunit;
using Xunit.Abstractions;

namespace TestDurableServerlessApp.IntegrationTests
{
    /// <summary>
    /// Verifies the durable Annotations path deploys the way a customer deploys it:
    /// <c>dotnet lambda deploy-serverless</c> -> CloudFormation, using the source-generator-produced
    /// serverless.template (the <c>DurableConfig</c> block plus the checkpoint-API IAM policy). This is the
    /// coverage the durable unit tests and the CreateFunction-based durable integ suite lack: it pushes the
    /// generated template through CloudFormation, where an invalid/empty <c>DurableConfig</c> is rejected at
    /// deploy time. The durable execution behavior itself (invoke, checkpoint, replay, history) is already
    /// covered by the Amazon.Lambda.DurableExecution integration tests, so this test only asserts that the
    /// function deploys and is configured as durable.
    /// </summary>
    public class DurableAnnotationsTest : IClassFixture<DurableServerlessFixture>
    {
        private readonly DurableServerlessFixture _fixture;
        private readonly ITestOutputHelper _output;

        public DurableAnnotationsTest(DurableServerlessFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task DurableServerless_DeploysWithDurableConfig()
        {
            // The fixture already asserted the CloudFormation stack reached CREATE_COMPLETE and discovered a
            // single Lambda function, so reaching here proves the generated DurableConfig template deployed.
            Assert.False(string.IsNullOrEmpty(_fixture.DurableFunctionName), "A durable function should have been deployed.");

            // Confirm the deployed function actually carries the durable configuration the generator emitted.
            // ExecutionTimeout is required — an empty DurableConfig would have failed the deploy above.
            var config = await _fixture.LambdaClient.GetFunctionConfigurationAsync(
                new GetFunctionConfigurationRequest { FunctionName = _fixture.DurableFunctionName });

            _output.WriteLine($"Deployed durable function '{_fixture.DurableFunctionName}' with ExecutionTimeout={config.DurableConfig?.ExecutionTimeout}");

            Assert.NotNull(config.DurableConfig);
            Assert.Equal(300, config.DurableConfig.ExecutionTimeout);
        }
    }
}
