// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.SNS;
using Amazon.Lambda.Core;
using Amazon.Lambda.SNSEvents;

namespace TestServerlessApp
{
    public class SnsMessageProcessing
    {
        [LambdaFunction(ResourceName = "SNSMessageHandler", Policies = "AWSLambdaBasicExecutionRole", PackageType = LambdaPackageType.Image)]
        [SNSEvent("@TestTopic", ResourceName = "TestTopicEvent", FilterPolicy = "{ \"store\": [\"example_corp\"] }")]
        public void HandleMessage(SNSEvent evnt, ILambdaContext lambdaContext)
        {
            lambdaContext.Logger.Log($"Received {evnt.Records.Count} messages");
        }
    }
}
