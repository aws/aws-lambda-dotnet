// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Internal;
using Amazon.Lambda.DurableExecution.Services;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class PluginInfoConverterTests
{
    private static Operation Step(string status, int? attempt) => new()
    {
        Id = "op", Type = OperationTypes.Step, Status = status,
        StepDetails = attempt == null ? null : new StepDetails { Attempt = attempt },
    };

    // StepDetails.Attempt counts finished attempts. Values below were observed from the service:
    // READY after one failure reports 1, and SUCCEEDED on the second attempt reports 2.
    [Theory]
    [InlineData(OperationStatuses.Ready, 1, 2)]
    [InlineData(OperationStatuses.Pending, 1, 2)]
    [InlineData(OperationStatuses.Started, null, 1)]
    [InlineData(OperationStatuses.Succeeded, 1, 1)]
    [InlineData(OperationStatuses.Succeeded, 2, 2)]
    [InlineData(OperationStatuses.Failed, 3, 3)]
    [InlineData(OperationStatuses.Succeeded, null, 1)]
    public void Attempt_IsTheLatestAttemptNumber(string status, int? recorded, int expected)
    {
        var info = PluginInfoConverter.ToOperationInfo(Step(status, recorded));

        Assert.Equal(expected, info.Attempt);
    }

    [Fact]
    public void Attempt_IsNullForOperationsThatDoNotRetry()
    {
        var wait = new Operation { Id = "w", Type = OperationTypes.Wait, Status = OperationStatuses.Succeeded };

        Assert.Null(PluginInfoConverter.ToOperationInfo(wait).Attempt);
    }

    [Fact]
    public void ServiceTimestamps_AreCopiedFromCheckpointResponses()
    {
        var start = new DateTime(2026, 11, 17, 4, 30, 47, 505, DateTimeKind.Utc);
        var end = start.AddSeconds(2);
        var sdkOp = new Amazon.Lambda.Model.Operation
        {
            Id = "op", Type = "STEP", Status = "SUCCEEDED", StartTimestamp = start, EndTimestamp = end,
        };

        var mapped = LambdaDurableServiceClient.MapFromSdkOperationForTest(sdkOp);

        Assert.Equal(new DateTimeOffset(start).ToUnixTimeMilliseconds(), mapped.StartTimestamp);
        Assert.Equal(new DateTimeOffset(end).ToUnixTimeMilliseconds(), mapped.EndTimestamp);
        Assert.Equal(start, PluginInfoConverter.ToOperationInfo(mapped).StartTimestamp!.Value.UtcDateTime);
    }

    [Fact]
    public void MissingServiceTimestamps_StayNull()
    {
        var mapped = LambdaDurableServiceClient.MapFromSdkOperationForTest(
            new Amazon.Lambda.Model.Operation { Id = "op", Type = "STEP", Status = "STARTED" });

        Assert.Null(mapped.StartTimestamp);
        Assert.Null(mapped.EndTimestamp);
    }
}
