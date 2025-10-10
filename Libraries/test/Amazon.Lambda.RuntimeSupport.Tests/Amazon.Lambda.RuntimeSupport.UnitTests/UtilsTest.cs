// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Xunit;
using Amazon.Lambda.RuntimeSupport.Bootstrap;

namespace Amazon.Lambda.RuntimeSupport.UnitTests;


public class UtilsTest
{
    [Theory]
    [InlineData("5", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsUsingMultiConcurrency(string concurrency, bool isMultiConcurrency)
    {
        var envVars = new TestEnvironmentVariables();

        if (concurrency != null)
            envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, concurrency);
        
        var result = Utils.IsUsingMultiConcurrency(envVars);
        
        Assert.Equal(isMultiConcurrency, result);
    }

    [Theory]
    [InlineData(null, 4, 1)]
    [InlineData("5", 4, 4)]
    [InlineData("5", 1, 2)]
    public void DetermineProcessingTaskCount(string concurrency, int processCount, int expected)
    {
        var envVars = new TestEnvironmentVariables();

        if (concurrency != null)
            envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, concurrency);
        
        var result = Utils.DetermineProcessingTaskCount(envVars, processCount);
        
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetermineProcessingTaskCount_WhenOverrideSet_ReturnsOverrideValue()
    {
        var envVars = new TestEnvironmentVariables();
        envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, "5");
        envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PROCESSING_TASKS, "8");
        
        var result = Utils.DetermineProcessingTaskCount(envVars, 4);
        
        Assert.Equal(8, result);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public void DetermineProcessingTaskCount_ThrowsArgumentException(string processingTasksOverride)
    {
        var envVars = new TestEnvironmentVariables();
        envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, "5");
        envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_DOTNET_PROCESSING_TASKS, processingTasksOverride);

        Assert.Throws<ArgumentException>(() => Utils.DetermineProcessingTaskCount(envVars, 4));
    }
}
