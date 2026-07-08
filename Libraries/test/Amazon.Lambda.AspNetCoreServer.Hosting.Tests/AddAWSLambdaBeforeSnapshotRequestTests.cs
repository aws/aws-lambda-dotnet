
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.AspNetCoreServer.Test;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions.AddAWSLambdaBeforeSnapshotRequest"/>
/// </summary>
public class AddAWSLambdaBeforeSnapshotRequestTests
{
    [Theory]
    [InlineData(LambdaEventSource.HttpApi)]
    [InlineData(LambdaEventSource.RestApi)]
    [InlineData(LambdaEventSource.ApplicationLoadBalancer)]
    public async Task VerifyCallbackIsInvoked(LambdaEventSource hostingType)
    {
        using var e1 = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", nameof(VerifyCallbackIsInvoked));
        using var e2 = new EnvironmentVariableHelper("AWS_LAMBDA_INITIALIZATION_TYPE", "snap-start");

        var callbackDidTheCallback = false;

        var builder = WebApplication.CreateSlimBuilder(new string[0]);

        builder.Services.AddAWSLambdaHosting(hostingType);
        // Initialize asp.net pipeline before Snapshot
        builder.Services.AddAWSLambdaBeforeSnapshotRequest( 
             new HttpRequestMessage(HttpMethod.Get, "/test")
        );
            
        var app = builder.Build();
            
        app.MapGet($"/test",
            () =>
            {
                callbackDidTheCallback = true;
                return "Success";
            });
            
        var serverTask = app.RunAsync();

        // Poll for the before-snapshot callback to fire rather than racing a single fixed delay.
        // A fixed 500 ms window is flaky under load (observed intermittently in CI): the
        // before-snapshot request may not have completed yet, leaving the callback unset. Wait up
        // to 10 seconds, checking frequently, and stop as soon as the callback has run.
        var timeout = TimeSpan.FromSeconds(10);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!callbackDidTheCallback && sw.Elapsed < timeout && !serverTask.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        // shut down server
        await app.StopAsync();

        Assert.True(callbackDidTheCallback);
    }
}
