// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Xunit;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;

namespace Amazon.Lambda.RuntimeSupport.UnitTests;


public class UtilsTest
{
    [Theory]
    // .NET runtime specific variable takes precedence.
    [InlineData("Json", null, true)]
    [InlineData("json", null, true)]
    [InlineData("Text", null, false)]
    // Falls back to the Lambda platform variable when the .NET one is not set.
    [InlineData(null, "Json", true)]
    [InlineData(null, "Text", false)]
    // The .NET variable wins over the platform variable.
    [InlineData("Text", "Json", false)]
    [InlineData("Json", "Text", true)]
    [InlineData(null, null, false)]
    public void IsJsonLogFormat(string ricLogFormat, string lambdaLogFormat, bool expected)
    {
        var envVars = new TestEnvironmentVariables();
        if (ricLogFormat != null)
            envVars.SetEnvironmentVariable(Constants.NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE, ricLogFormat);
        if (lambdaLogFormat != null)
            envVars.SetEnvironmentVariable(Constants.LAMBDA_LOG_FORMAT_ENVIRONMENT_VARIABLE, lambdaLogFormat);

        Assert.Equal(expected, Utils.IsJsonLogFormat(envVars));
    }

    [Fact]
    public void EmitWorkerPoolInitializingLog_WhenMultiConcurrencyAndJson_EmitsOnce()
    {
        var envVars = new TestEnvironmentVariables();
        envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, "10");
        envVars.SetEnvironmentVariable(Constants.NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE, "Json");
        var logger = new CapturingConsoleLoggerWriter();

        // Use a worker count that differs from the max concurrency to confirm the two values are reported distinctly.
        Utils.EmitWorkerPoolInitializingLog(logger, envVars, workerCount: 3, maxConcurrency: 10);

        var write = Assert.Single(logger.Writes);
        Assert.Equal(LogLevelLoggerWriter.LogLevel.Debug.ToString(), write.Level);
        Assert.Equal(Utils.WorkerPoolInitializingLogTemplate, write.Message);
        Assert.Equal(new object[] { Utils.WorkerPoolInitializingEvent, 3, 10 }, write.Args);
    }

    [Fact]
    public void EmitWorkerPoolInitializingLog_WhenMultiConcurrencyButNotJson_DoesNotEmit()
    {
        var envVars = new TestEnvironmentVariables();
        envVars.SetEnvironmentVariable(Constants.ENVIRONMENT_VARIABLE_AWS_LAMBDA_MAX_CONCURRENCY, "10");
        var logger = new CapturingConsoleLoggerWriter();

        Utils.EmitWorkerPoolInitializingLog(logger, envVars, workerCount: 10, maxConcurrency: 10);

        Assert.Empty(logger.Writes);
    }

    [Fact]
    public void EmitWorkerPoolInitializingLog_WhenNotMultiConcurrency_DoesNotEmit()
    {
        var envVars = new TestEnvironmentVariables();
        envVars.SetEnvironmentVariable(Constants.NET_RIC_LOG_FORMAT_ENVIRONMENT_VARIABLE, "Json");
        var logger = new CapturingConsoleLoggerWriter();

        Utils.EmitWorkerPoolInitializingLog(logger, envVars, workerCount: 1, maxConcurrency: 0);

        Assert.Empty(logger.Writes);
    }
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
    [InlineData("5", 4, 5)]
    [InlineData("5", 1, 5)]
    [InlineData("10", 2, 10)]
    [InlineData("enabled", 4, 4)]
    [InlineData("enabled", 1, 2)]
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
