// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using Amazon.Lambda.DurableExecution;
using Amazon.Lambda.Serialization.SystemTextJson;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

// The [DurableExecution] attribute itself now lives in Amazon.Lambda.Annotations (its unit tests
// live with that package). These tests cover the durable invocation envelopes that the generated
// typed-envelope wrapper relies on: the runtime HandlerWrapper deserializes
// DurableExecutionInvocationInput and serializes DurableExecutionInvocationOutput using the
// ILambdaSerializer attached to the context. This verifies the default serializer round-trips both
// envelopes (including the UpperSnakeCaseEnumConverter on Status and a nested InitialExecutionState)
// without loss.
public class DurableEnvelopeSerializationTests
{
    [Fact]
    public void DefaultSerializer_RoundTripsInvocationInput()
    {
        var serializer = new DefaultLambdaJsonSerializer();
        var input = new DurableExecutionInvocationInput
        {
            DurableExecutionArn = "arn:aws:lambda:us-west-2:123456789012:durable-execution:abc",
            CheckpointToken = "token-1",
            InitialExecutionState = new InitialExecutionState
            {
                NextMarker = "marker-2"
            }
        };

        var roundTripped = RoundTrip(serializer, input);

        Assert.Equal(input.DurableExecutionArn, roundTripped.DurableExecutionArn);
        Assert.Equal(input.CheckpointToken, roundTripped.CheckpointToken);
        Assert.NotNull(roundTripped.InitialExecutionState);
        Assert.Equal("marker-2", roundTripped.InitialExecutionState!.NextMarker);
    }

    [Fact]
    public void DefaultSerializer_RoundTripsInvocationOutput()
    {
        var serializer = new DefaultLambdaJsonSerializer();
        var output = new DurableExecutionInvocationOutput
        {
            Status = InvocationStatus.Pending,
            Result = "\"some-result\""
        };

        var roundTripped = RoundTrip(serializer, output);

        Assert.Equal(InvocationStatus.Pending, roundTripped.Status);
        Assert.Equal("\"some-result\"", roundTripped.Result);
        Assert.Null(roundTripped.Error);
    }

    [Fact]
    public void DefaultSerializer_SerializesStatusAsUpperSnakeCase()
    {
        var serializer = new DefaultLambdaJsonSerializer();
        var output = new DurableExecutionInvocationOutput { Status = InvocationStatus.Succeeded };

        using var stream = new MemoryStream();
        serializer.Serialize(output, stream);
        var json = Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"SUCCEEDED\"", json);
    }

    private static T RoundTrip<T>(DefaultLambdaJsonSerializer serializer, T value)
    {
        using var stream = new MemoryStream();
        serializer.Serialize(value, stream);
        stream.Position = 0;
        return serializer.Deserialize<T>(stream);
    }
}
