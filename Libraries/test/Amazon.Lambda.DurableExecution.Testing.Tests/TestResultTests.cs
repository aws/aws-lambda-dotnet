// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Amazon.Lambda.Serialization.SystemTextJson;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class TestResultTests
{
    private static readonly DefaultLambdaJsonSerializer Serializer = new();

    [Fact]
    public void EnsureSucceeded_DoesNotThrow_WhenSucceeded()
    {
        var result = CreateResult(InvocationStatus.Succeeded, "hello");
        result.EnsureSucceeded();
    }

    [Fact]
    public void EnsureSucceeded_Throws_WhenFailed()
    {
        var error = new ErrorObject { ErrorType = "TestException", ErrorMessage = "something broke" };
        var result = CreateResult(InvocationStatus.Failed, default(string), error);

        var ex = Assert.Throws<TestExecutionFailedException>(() => result.EnsureSucceeded());
        Assert.Equal(InvocationStatus.Failed, ex.FinalStatus);
        Assert.Equal("TestException", ex.FailureError?.ErrorType);
        Assert.Contains("TestException", ex.Message);
        Assert.Contains("something broke", ex.Message);
    }

    [Fact]
    public void EnsureSucceeded_Throws_WhenPending()
    {
        var result = CreateResult(InvocationStatus.Pending, default(string));
        var ex = Assert.Throws<TestExecutionFailedException>(() => result.EnsureSucceeded());
        Assert.Equal(InvocationStatus.Pending, ex.FinalStatus);
    }

    [Fact]
    public void GetStep_ReturnsFirstMatch()
    {
        var steps = new[]
        {
            MakeStep("op-1", "step_a"),
            MakeStep("op-2", "step_b"),
            MakeStep("op-3", "step_a"),
        };

        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var found = result.GetStep("step_a");
        Assert.Equal("op-1", found.Id);
    }

    [Fact]
    public void GetStep_Throws_WhenNotFound()
    {
        var steps = new[] { MakeStep("op-1", "step_a") };
        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var ex = Assert.Throws<InvalidOperationException>(() => result.GetStep("missing"));
        Assert.Contains("missing", ex.Message);
        Assert.Contains("step_a", ex.Message);
    }

    [Fact]
    public void FindStep_ReturnsNull_WhenNotFound()
    {
        var steps = new[] { MakeStep("op-1", "step_a") };
        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        Assert.Null(result.FindStep("missing"));
    }

    [Fact]
    public void FindStep_ReturnsMatch()
    {
        var steps = new[] { MakeStep("op-1", "step_a") };
        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var found = result.FindStep("step_a");
        Assert.NotNull(found);
        Assert.Equal("op-1", found!.Id);
    }

    [Fact]
    public void GetSteps_ReturnsAllMatches()
    {
        var steps = new[]
        {
            MakeStep("op-1", "process_item"),
            MakeStep("op-2", "other"),
            MakeStep("op-3", "process_item"),
            MakeStep("op-4", "process_item"),
        };

        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var found = result.GetSteps("process_item");
        Assert.Equal(3, found.Count);
        Assert.Equal("op-1", found[0].Id);
        Assert.Equal("op-3", found[1].Id);
        Assert.Equal("op-4", found[2].Id);
    }

    [Fact]
    public void GetSteps_ReturnsEmpty_WhenNoMatches()
    {
        var steps = new[] { MakeStep("op-1", "step_a") };
        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        Assert.Empty(result.GetSteps("missing"));
    }

    [Fact]
    public void GetStepById_ReturnsMatch()
    {
        var steps = new[]
        {
            MakeStep("op-1", "step_a"),
            MakeStep("op-2", "step_b"),
        };

        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var found = result.GetStepById("op-2");
        Assert.Equal("step_b", found.Name);
    }

    [Fact]
    public void GetStepById_Throws_WhenNotFound()
    {
        var steps = new[] { MakeStep("op-1", "step_a") };
        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var ex = Assert.Throws<InvalidOperationException>(() => result.GetStepById("op-999"));
        Assert.Contains("op-999", ex.Message);
    }

    [Fact]
    public void Children_LinkedByParentId()
    {
        var parent = new Operation { Id = "op-parent", Type = OperationTypes.Context, Name = "batch" };
        var child1 = new Operation { Id = "op-c1", Type = OperationTypes.Step, Name = "item_1", ParentId = "op-parent" };
        var child2 = new Operation { Id = "op-c2", Type = OperationTypes.Step, Name = "item_2", ParentId = "op-parent" };
        var unrelated = new Operation { Id = "op-other", Type = OperationTypes.Step, Name = "other" };

        var steps = new[]
        {
            new TestStep(parent, Serializer),
            new TestStep(child1, Serializer),
            new TestStep(child2, Serializer),
            new TestStep(unrelated, Serializer),
        };

        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var parentStep = result.GetStep("batch");
        Assert.Equal(2, parentStep.Children.Count);
        Assert.Equal("op-c1", parentStep.Children[0].Id);
        Assert.Equal("op-c2", parentStep.Children[1].Id);

        var otherStep = result.GetStep("other");
        Assert.Empty(otherStep.Children);
    }

    [Fact]
    public void GetChildren_ReturnsSameAsProperty()
    {
        var parent = new Operation { Id = "op-parent", Type = OperationTypes.Context, Name = "batch" };
        var child = new Operation { Id = "op-c1", Type = OperationTypes.Step, Name = "item_1", ParentId = "op-parent" };

        var steps = new[]
        {
            new TestStep(parent, Serializer),
            new TestStep(child, Serializer),
        };

        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "done", null, "arn:test", 1, steps);

        var parentStep = result.GetStep("batch");
        Assert.Same(parentStep.Children, result.GetChildren(parentStep));
    }

    [Fact]
    public void Properties_ExposedCorrectly()
    {
        var result = new TestResult<int>(
            InvocationStatus.Succeeded, 42, null, "arn:aws:lambda:us-east-1:123:execution:fn:exec", 5,
            Array.Empty<TestStep>());

        Assert.Equal(InvocationStatus.Succeeded, result.Status);
        Assert.Equal(42, result.Result);
        Assert.Null(result.Error);
        Assert.Equal("arn:aws:lambda:us-east-1:123:execution:fn:exec", result.DurableExecutionArn);
        Assert.Equal(5, result.InvocationCount);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void EmptySteps_NoExceptions()
    {
        var result = new TestResult<string>(
            InvocationStatus.Succeeded, "ok", null, "arn:test", 1, Array.Empty<TestStep>());

        Assert.Empty(result.Steps);
        Assert.Empty(result.GetSteps("anything"));
    }

    private static TestResult<T> CreateResult<T>(InvocationStatus status, T? output, ErrorObject? error = null)
    {
        return new TestResult<T>(status, output, error, "arn:test", 1, Array.Empty<TestStep>());
    }

    private static TestStep MakeStep(string id, string? name, string? parentId = null)
    {
        var op = new Operation
        {
            Id = id,
            Name = name,
            Type = OperationTypes.Step,
            Status = OperationStatuses.Succeeded,
            ParentId = parentId
        };
        return new TestStep(op, Serializer);
    }
}
