
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
    #if NET8_0_OR_GREATER
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
        builder.Services.AddAWSLambdaBeforeSnapshotRequest(async httpClient =>
        {
            await httpClient.GetAsync($"/test");
        });
            
        var app = builder.Build();
            
        app.MapGet($"/test",
            () =>
            {
                callbackDidTheCallback = true;
                return "Success";
            });
            
        var serverTask = app.RunAsync();

        // let the server run for a max of 500 ms
        await Task.WhenAny(
            serverTask,
            Task.Delay(TimeSpan.FromMilliseconds(500)));

        // shut down server
        await app.StopAsync();

        Assert.True(callbackDidTheCallback);
    }
    #endif
}
