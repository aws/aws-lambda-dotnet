// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatchEvents;
using Amazon.CloudWatchEvents.Model;
using Xunit;

namespace TestServerlessApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class ScheduleEventRule
    {
        private readonly IntegrationTestContextFixture _fixture;

        public ScheduleEventRule(IntegrationTestContextFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task VerifyScheduleEventRuleConfiguration()
        {
            var lambdaFunctionName = _fixture.LambdaFunctions.FirstOrDefault(x => string.Equals(x.LogicalId, "ScheduledHandler"))?.Name;
            Assert.NotNull(lambdaFunctionName);

            var eventsClient = new AmazonCloudWatchEventsClient(Amazon.RegionEndpoint.USWest2);

            // Paginate through all rules and verify the rule targets the correct Lambda function
            Rule matchingRule = null;
            string nextToken = null;

            do
            {
                var rulesResponse = await eventsClient.ListRulesAsync(new ListRulesRequest
                {
                    NextToken = nextToken
                });

                foreach (var rule in rulesResponse.Rules.Where(r =>
                    string.Equals(r.ScheduleExpression, "rate(5 minutes)") &&
                    string.Equals(r.Description, "Runs every 5 minutes")))
                {
                    var targetsResponse = await eventsClient.ListTargetsByRuleAsync(new ListTargetsByRuleRequest
                    {
                        Rule = rule.Name
                    });

                    if (targetsResponse.Targets.Any(t => t.Arn != null && t.Arn.Contains($":function:{lambdaFunctionName}")))
                    {
                        matchingRule = rule;
                        break;
                    }
                }

                if (matchingRule != null)
                {
                    break;
                }

                nextToken = rulesResponse.NextToken;
            }
            while (!string.IsNullOrEmpty(nextToken));

            Assert.NotNull(matchingRule);
            Assert.Equal("rate(5 minutes)", matchingRule.ScheduleExpression);
            Assert.Equal("Runs every 5 minutes", matchingRule.Description);
        }
    }
}
