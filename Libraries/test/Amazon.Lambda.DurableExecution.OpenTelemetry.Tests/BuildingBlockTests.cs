// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry.Tests;

public class ContextExtractorTests
{
    [Fact]
    public void XRayHeader_WithRootParentAndSampled_IsParsed()
    {
        var context = ContextExtractors.ParseXRayHeader(
            "Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1;Lineage=1:1888001e:0");

        Assert.Equal("5759e988bd862e3fe1be46a994272793", context!.TraceId);
        Assert.Equal("53995c3f42cd8ad8", context.ParentSpanId);
        Assert.Equal(UpstreamSampling.Sampled, context.Sampling);
    }

    [Theory]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=0", UpstreamSampling.NotSampled)]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8", UpstreamSampling.Undecided)]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Sampled=?", UpstreamSampling.Undecided)]
    public void Sampling_IsReadIndependentlyOfTheParent(string header, UpstreamSampling expected) =>
        Assert.Equal(expected, ContextExtractors.ParseXRayHeader(header)!.Sampling);

    [Theory]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Sampled=1")]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=0000000000000000;Sampled=1")]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=short;Sampled=1")]
    public void MissingOrInvalidParent_LeavesTheTraceIdUsable(string header)
    {
        var context = ContextExtractors.ParseXRayHeader(header)!;

        Assert.Equal("5759e988bd862e3fe1be46a994272793", context.TraceId);
        Assert.Null(context.ParentSpanId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Parent=53995c3f42cd8ad8;Sampled=1")]
    [InlineData("Root=1-00000000-000000000000000000000000;Parent=53995c3f42cd8ad8")]
    [InlineData("Root=1-xyz;Parent=53995c3f42cd8ad8")]
    public void WithoutAValidRoot_NothingIsExtracted(string? header) =>
        Assert.Null(ContextExtractors.ParseXRayHeader(header));
}

public class DeterministicIdTests
{
    private const string Arn = "arn:aws:lambda:us-east-1:123456789012:execution:test-fn:test-execution";

    // Expected values were computed with SHA-256 over the same strings the JavaScript, Python and Java SDKs hash,
    // so a trace that crosses SDKs agrees on these ids.
    [Fact]
    public void Ids_MatchTheOtherSdks()
    {
        Assert.Equal("90dc7e18c6d7080f", DeterministicIds.WorkflowSpanId(Arn));
        Assert.Equal("3dd8b3833680e6c9", DeterministicIds.ExecutionRootSpanId(Arn));
        Assert.Equal("83b376fa00e63e69", DeterministicIds.SpanIdFromOperationId("abc", Arn));
        Assert.Equal("471df46191ffb07efe6435d179443e16", DeterministicIds.TraceIdFromArn(Arn, null));
        Assert.Equal(
            "80dc8381b6cc28ec62f41397071bd59a",
            DeterministicIds.TraceIdFromArn(Arn, new DateTimeOffset(2026, 11, 17, 4, 30, 42, 814, TimeSpan.Zero)));
    }

    [Fact]
    public void Ids_AreScopedToTheExecution()
    {
        Assert.NotEqual(
            DeterministicIds.SpanIdFromOperationId("abc", Arn),
            DeterministicIds.SpanIdFromOperationId("abc", Arn + "-other"));
        Assert.NotEqual(DeterministicIds.WorkflowSpanId(Arn), DeterministicIds.ExecutionRootSpanId(Arn));
    }

    [Fact]
    public void Validation_RejectsZeroAndMalformedIds()
    {
        Assert.False(DeterministicIds.IsValidSpanId("0000000000000000"));
        Assert.False(DeterministicIds.IsValidSpanId("ABCDEF0123456789"));
        Assert.False(DeterministicIds.IsValidTraceId(new string('0', 32)));
        Assert.True(DeterministicIds.IsValidTraceId("5759e988bd862e3fe1be46a994272793"));
    }
}

public class ActivityIdOverrideTests
{
    private static ActivitySource NewSource(out ActivityListener listener, out string name)
    {
        name = "override-" + Guid.NewGuid().ToString("N");
        var captured = name;
        listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == captured,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return new ActivitySource(name);
    }

    private static readonly ActivityContext Parent = new(
        ActivityTraceId.CreateFromString("5759e988bd862e3fe1be46a994272793"),
        ActivitySpanId.CreateFromString("53995c3f42cd8ad8"), ActivityTraceFlags.Recorded, isRemote: true);

    // This test is the early warning for a runtime that renames the private fields the override writes. If it
    // fails on a new runtime, spans lose their derived ids and references across invocations break.
    [Fact]
    public void TheActivityFieldsTheOverrideWritesExist()
    {
        ActivityIdOverride.Reset();
        using var source = NewSource(out var listener, out _);
        using var _l = listener;
        using var activity = source.StartActivity("x", ActivityKind.Internal, Parent)!;

        Assert.True(ActivityIdOverride.TrySet(activity, "00f067aa0ba902b7"));
        Assert.False(ActivityIdOverride.IsDisabled);
    }

    [Fact]
    public void SpanId_IdAndChildrenSeeTheOverriddenId()
    {
        ActivityIdOverride.Reset();
        using var source = NewSource(out var listener, out _);
        using var _l = listener;
        using var activity = source.StartActivity("x", ActivityKind.Internal, Parent)!;

        ActivityIdOverride.TrySet(activity, "00f067aa0ba902b7");
        using var child = source.StartActivity("child", ActivityKind.Internal, activity.Context)!;

        Assert.Equal("00f067aa0ba902b7", activity.SpanId.ToHexString());
        Assert.Contains("00f067aa0ba902b7", activity.Id);
        Assert.Equal("00f067aa0ba902b7", activity.Context.SpanId.ToHexString());
        Assert.Equal(activity.SpanId, child.ParentSpanId);
        Assert.Equal("53995c3f42cd8ad8", activity.ParentSpanId.ToHexString());
    }

    [Fact]
    public void AsRoot_RemovesTheParent()
    {
        ActivityIdOverride.Reset();
        using var source = NewSource(out var listener, out _);
        using var _l = listener;
        using var activity = source.StartActivity("root", ActivityKind.Internal, Parent)!;

        ActivityIdOverride.TrySet(activity, "00f067aa0ba902b7", asRoot: true);

        Assert.Equal(default, activity.ParentSpanId);
        Assert.Null(activity.ParentId);
        Assert.Equal("5759e988bd862e3fe1be46a994272793", activity.TraceId.ToHexString());
    }

    [Fact]
    public void WithoutAnActivity_NothingHappens()
    {
        ActivityIdOverride.Reset();

        Assert.False(ActivityIdOverride.TrySet(null, "00f067aa0ba902b7"));
        Assert.False(ActivityIdOverride.IsDisabled);
    }
}
