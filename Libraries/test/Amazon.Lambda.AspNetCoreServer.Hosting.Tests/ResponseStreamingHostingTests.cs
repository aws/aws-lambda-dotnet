// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Runtime.Versioning;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.AspNetCoreServer.Test;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Tests for response streaming integration in hosting (Requirement 10).
/// </summary>
[RequiresPreviewFeatures]
public class ResponseStreamingHostingTests
{
    [Fact]
    public void EnableResponseStreaming_DefaultsToFalse()
    {
        var options = new HostingOptions();
        Assert.False(options.EnableResponseStreaming);
    }

    [Fact]
    public void EnableResponseStreaming_CanBeSetToTrue()
    {
        var options = new HostingOptions { EnableResponseStreaming = true };
        Assert.True(options.EnableResponseStreaming);
    }

    [Fact]
    public void AddAWSLambdaHosting_ConfigureCallback_CanSetEnableResponseStreamingTrue()
    {
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

#pragma warning disable CA2252
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
        {
            options.EnableResponseStreaming = true;
        });
#pragma warning restore CA2252

        var sp = services.BuildServiceProvider();
        var hostingOptions = sp.GetService<HostingOptions>();

        Assert.NotNull(hostingOptions);
        Assert.True(hostingOptions.EnableResponseStreaming);
    }

    [Fact]
    public void AddAWSLambdaHosting_WithoutCallback_EnableResponseStreamingRemainsDefault()
    {
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

        var sp = services.BuildServiceProvider();
        var hostingOptions = sp.GetService<HostingOptions>();

        Assert.NotNull(hostingOptions);
        Assert.False(hostingOptions.EnableResponseStreaming);
    }


    // Helper: build a minimal IServiceProvider with the given HostingOptions
    private static IServiceProvider BuildServiceProvider(HostingOptions hostingOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton(hostingOptions);
        services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    // ---- APIGatewayHttpApiV2 ----

    [Fact]
    public void HttpApiV2_CreateHandlerWrapper_StreamingFalse_TargetsFunctionHandlerAsync()
    {
        var options = new HostingOptions { EnableResponseStreaming = false };
        var sp = BuildServiceProvider(options);

        var server = new TestableHttpApiV2Server(sp);
        var wrapper = server.PublicCreateHandlerWrapper(sp);

        // The handler delegate target method should be FunctionHandlerAsync (not streaming)
        var methodName = GetHandlerDelegateMethodName(wrapper);
        Assert.Contains("FunctionHandlerAsync", methodName);
        Assert.DoesNotContain("Streaming", methodName);
    }

    [Fact]
    public void HttpApiV2_CreateHandlerWrapper_StreamingTrue_TargetsFunctionHandlerAsync()
    {
        var options = new HostingOptions { EnableResponseStreaming = true };
        var sp = BuildServiceProvider(options);

        var server = new TestableHttpApiV2Server(sp);
        var wrapper = server.PublicCreateHandlerWrapper(sp);

        var methodName = GetHandlerDelegateMethodName(wrapper);
        Assert.Contains("FunctionHandlerAsync", methodName);
    }

    // ---- APIGatewayRestApi ----

    [Fact]
    public void RestApi_CreateHandlerWrapper_StreamingFalse_TargetsFunctionHandlerAsync()
    {
        var options = new HostingOptions { EnableResponseStreaming = false };
        var sp = BuildServiceProvider(options);

        var server = new TestableRestApiServer(sp);
        var wrapper = server.PublicCreateHandlerWrapper(sp);

        var methodName = GetHandlerDelegateMethodName(wrapper);
        Assert.Contains("FunctionHandlerAsync", methodName);
        Assert.DoesNotContain("Streaming", methodName);
    }

    [Fact]
    public void RestApi_CreateHandlerWrapper_StreamingTrue_TargetsFunctionHandlerAsync()
    {
        var options = new HostingOptions { EnableResponseStreaming = true };
        var sp = BuildServiceProvider(options);

        var server = new TestableRestApiServer(sp);
        var wrapper = server.PublicCreateHandlerWrapper(sp);

        var methodName = GetHandlerDelegateMethodName(wrapper);
        Assert.Contains("FunctionHandlerAsync", methodName);
    }

    // ---- ApplicationLoadBalancer ----

    [Fact]
    public void Alb_CreateHandlerWrapper_StreamingFalse_TargetsFunctionHandlerAsync()
    {
        var options = new HostingOptions { EnableResponseStreaming = false };
        var sp = BuildServiceProvider(options);

        var server = new TestableAlbServer(sp);
        var wrapper = server.PublicCreateHandlerWrapper(sp);

        var methodName = GetHandlerDelegateMethodName(wrapper);
        Assert.Contains("FunctionHandlerAsync", methodName);
        Assert.DoesNotContain("Streaming", methodName);
    }

    [Fact]
    public void Alb_CreateHandlerWrapper_StreamingTrue_TargetsFunctionHandlerAsync()
    {
        var options = new HostingOptions { EnableResponseStreaming = true };
        var sp = BuildServiceProvider(options);

        var server = new TestableAlbServer(sp);
        var wrapper = server.PublicCreateHandlerWrapper(sp);

        var methodName = GetHandlerDelegateMethodName(wrapper);
        Assert.Contains("FunctionHandlerAsync", methodName);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Extracts the method name from the delegate stored inside a HandlerWrapper.
    /// HandlerWrapper.Handler is a LambdaBootstrapHandler (a delegate). The actual
    /// user-supplied delegate is captured in a closure, so we walk the closure's
    /// fields to find the innermost Func/delegate and read its Method.Name.
    /// </summary>
    private static string GetHandlerDelegateMethodName(HandlerWrapper wrapper)
    {
        // HandlerWrapper.Handler is the LambdaBootstrapHandler delegate.
        // It is an async lambda that closes over the user-supplied handler delegate.
        // We use reflection to dig through the closure chain until we find a field
        // whose type is a delegate with a Method.Name we can inspect.
        var handler = wrapper.Handler;
        return FindDelegateMethodName(handler.Target, visited: new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static string FindDelegateMethodName(object? target, HashSet<object> visited)
    {
        if (target == null || !visited.Add(target))
            return string.Empty;

        foreach (var field in target.GetType().GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public))
        {
            var value = field.GetValue(target);
            if (value == null) continue;

            if (value is Delegate d)
            {
                var name = d.Method.Name;
                // Skip compiler-generated method names (lambdas / state machines)
                if (!name.StartsWith("<") && !name.Contains("MoveNext"))
                    return name;

                // Recurse into the delegate's own closure
                var inner = FindDelegateMethodName(d.Target, visited);
                if (!string.IsNullOrEmpty(inner))
                    return inner;
            }
            else if (value.GetType().IsClass && !value.GetType().IsPrimitive
                     && value.GetType().Namespace?.StartsWith("System") == false)
            {
                var inner = FindDelegateMethodName(value, visited);
                if (!string.IsNullOrEmpty(inner))
                    return inner;
            }
        }

        return string.Empty;
    }

    // -------------------------------------------------------------------------
    // Testable server subclasses that expose CreateHandlerWrapper publicly
    // -------------------------------------------------------------------------

    private class TestableHttpApiV2Server : APIGatewayHttpApiV2LambdaRuntimeSupportServer
    {
        public TestableHttpApiV2Server(IServiceProvider sp) : base(sp) { }

        public HandlerWrapper PublicCreateHandlerWrapper(IServiceProvider sp)
            => CreateHandlerWrapper(sp);
    }

    private class TestableRestApiServer : APIGatewayRestApiLambdaRuntimeSupportServer
    {
        public TestableRestApiServer(IServiceProvider sp) : base(sp) { }

        public HandlerWrapper PublicCreateHandlerWrapper(IServiceProvider sp)
            => CreateHandlerWrapper(sp);
    }

    private class TestableAlbServer : ApplicationLoadBalancerLambdaRuntimeSupportServer
    {
        public TestableAlbServer(IServiceProvider sp) : base(sp) { }

        public HandlerWrapper PublicCreateHandlerWrapper(IServiceProvider sp)
            => CreateHandlerWrapper(sp);
    }
}
