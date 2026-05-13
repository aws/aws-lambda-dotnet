// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.Schedule;
using Amazon.Lambda.CloudWatchEvents.ScheduledEvents;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ScheduledProcessing
    {
        [LambdaFunction(ResourceName = "ScheduledHandler", Policies = "AWSLambdaBasicExecutionRole", PackageType = LambdaPackageType.Image)]
        [ScheduleEvent("rate(5 minutes)", ResourceName = "FiveMinuteSchedule", Description = "Runs every 5 minutes")]
        public void HandleSchedule(ScheduledEvent evnt, ILambdaContext lambdaContext)
        {
            lambdaContext.Logger.Log($"Scheduled event received at {evnt.Time}");
        }
    }
}
