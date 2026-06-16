// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Testing;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Testing.Tests;

public class OptionsValidationTests
{
    [Fact]
    public void TestRunnerOptions_Defaults()
    {
        var options = new TestRunnerOptions();

        Assert.True(options.SkipTime);
        Assert.Equal(100, options.MaxInvocations);
        Assert.Equal(TimeSpan.FromSeconds(30), options.DefaultTimeout);
        Assert.Null(options.Serializer);
        Assert.Null(options.LoggerFactory);
        Assert.Equal("arn:aws:lambda:us-east-1:123456789012:execution:test-fn:test-execution", options.DurableExecutionArn);
    }

    [Fact]
    public void TestRunnerOptions_MaxInvocations_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestRunnerOptions { MaxInvocations = 0 });
    }

    [Fact]
    public void TestRunnerOptions_MaxInvocations_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestRunnerOptions { MaxInvocations = -1 });
    }

    [Fact]
    public void TestRunnerOptions_MaxInvocations_PositiveValue_Accepted()
    {
        var options = new TestRunnerOptions { MaxInvocations = 500 };
        Assert.Equal(500, options.MaxInvocations);
    }

    [Fact]
    public void TestRunnerOptions_CustomArn()
    {
        var options = new TestRunnerOptions
        {
            DurableExecutionArn = "arn:aws:lambda:eu-west-1:999:execution:my-fn:custom"
        };
        Assert.Equal("arn:aws:lambda:eu-west-1:999:execution:my-fn:custom", options.DurableExecutionArn);
    }

    [Fact]
    public void CloudTestRunnerOptions_Defaults()
    {
        var options = new CloudTestRunnerOptions();

        Assert.Equal(TimeSpan.FromSeconds(2), options.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(5), options.DefaultTimeout);
        Assert.Null(options.Serializer);
    }

    [Fact]
    public void CloudTestRunnerOptions_CustomValues()
    {
        var options = new CloudTestRunnerOptions
        {
            PollInterval = TimeSpan.FromSeconds(5),
            DefaultTimeout = TimeSpan.FromMinutes(10)
        };

        Assert.Equal(TimeSpan.FromSeconds(5), options.PollInterval);
        Assert.Equal(TimeSpan.FromMinutes(10), options.DefaultTimeout);
    }
}
