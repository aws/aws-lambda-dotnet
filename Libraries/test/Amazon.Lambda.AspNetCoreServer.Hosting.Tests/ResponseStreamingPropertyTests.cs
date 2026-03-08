// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER

using System.Runtime.Versioning;

using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

using CsCheck;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Property-based tests for the hosting streaming flag routing (Property 9).
/// </summary>
[RequiresPreviewFeatures]
public class ResponseStreamingPropertyTests
{
    // -----------------------------------------------------------------------
    // Property 9: Hosting streaming flag routes to StreamingFunctionHandlerAsync
    // Feature: aspnetcore-response-streaming, Property 9: Hosting streaming flag routes to StreamingFunctionHandlerAsync
    // Validates: Requirements 10.2, 10.3
    // -----------------------------------------------------------------------

    private static IServiceProvider BuildServiceProvider(HostingOptions hostingOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton(hostingOptions);
        services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Extracts the handler method name from a HandlerWrapper by walking the closure chain.
    /// Mirrors the helper in ResponseStreamingHostingTests.
    /// </summary>
    private static string GetHandlerDelegateMethodName(HandlerWrapper wrapper)
    {
        var handler = wrapper.Handler;
        return FindDelegateMethodName(handler.Target, new HashSet<object>(ReferenceEqualityComparer.Instance));
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
                if (!name.StartsWith("<") && !name.Contains("MoveNext"))
                    return name;
                var inner = FindDelegateMethodName(d.Target, visited);
                if (!string.IsNullOrEmpty(inner)) return inner;
            }
            else if (value.GetType().IsClass && !value.GetType().IsPrimitive
                     && value.GetType().Namespace?.StartsWith("System") == false)
            {
                var inner = FindDelegateMethodName(value, visited);
                if (!string.IsNullOrEmpty(inner)) return inner;
            }
        }

        return string.Empty;
    }

    // Testable server subclasses that expose CreateHandlerWrapper publicly
    private class TestableHttpApiV2Server : APIGatewayHttpApiV2LambdaRuntimeSupportServer
    {
        public TestableHttpApiV2Server(IServiceProvider sp) : base(sp) { }
        public HandlerWrapper PublicCreateHandlerWrapper(IServiceProvider sp) => CreateHandlerWrapper(sp);
    }

    private class TestableRestApiServer : APIGatewayRestApiLambdaRuntimeSupportServer
    {
        public TestableRestApiServer(IServiceProvider sp) : base(sp) { }
        public HandlerWrapper PublicCreateHandlerWrapper(IServiceProvider sp) => CreateHandlerWrapper(sp);
    }

    private class TestableAlbServer : ApplicationLoadBalancerLambdaRuntimeSupportServer
    {
        public TestableAlbServer(IServiceProvider sp) : base(sp) { }
        public HandlerWrapper PublicCreateHandlerWrapper(IServiceProvider sp) => CreateHandlerWrapper(sp);
    }

    [Fact]
    public void Property9_HttpApiV2_StreamingFlag_RoutesCorrectly()
    {
        // Feature: aspnetcore-response-streaming, Property 9: Hosting streaming flag routes to StreamingFunctionHandlerAsync
        Gen.Bool.Sample(enableStreaming =>
        {
            var options = new HostingOptions { EnableResponseStreaming = enableStreaming };
            var sp = BuildServiceProvider(options);
            var server = new TestableHttpApiV2Server(sp);
            var wrapper = server.PublicCreateHandlerWrapper(sp);
            var methodName = GetHandlerDelegateMethodName(wrapper);

            if (enableStreaming)
                Assert.Contains("StreamingFunctionHandlerAsync", methodName);
            else
            {
                Assert.Contains("FunctionHandlerAsync", methodName);
                Assert.DoesNotContain("Streaming", methodName);
            }
        }, iter: 100);
    }

    [Fact]
    public void Property9_RestApi_StreamingFlag_RoutesCorrectly()
    {
        // Feature: aspnetcore-response-streaming, Property 9: Hosting streaming flag routes to StreamingFunctionHandlerAsync
        Gen.Bool.Sample(enableStreaming =>
        {
            var options = new HostingOptions { EnableResponseStreaming = enableStreaming };
            var sp = BuildServiceProvider(options);
            var server = new TestableRestApiServer(sp);
            var wrapper = server.PublicCreateHandlerWrapper(sp);
            var methodName = GetHandlerDelegateMethodName(wrapper);

            if (enableStreaming)
                Assert.Contains("StreamingFunctionHandlerAsync", methodName);
            else
            {
                Assert.Contains("FunctionHandlerAsync", methodName);
                Assert.DoesNotContain("Streaming", methodName);
            }
        }, iter: 100);
    }

    [Fact]
    public void Property9_Alb_StreamingFlag_RoutesCorrectly()
    {
        // Feature: aspnetcore-response-streaming, Property 9: Hosting streaming flag routes to StreamingFunctionHandlerAsync
        Gen.Bool.Sample(enableStreaming =>
        {
            var options = new HostingOptions { EnableResponseStreaming = enableStreaming };
            var sp = BuildServiceProvider(options);
            var server = new TestableAlbServer(sp);
            var wrapper = server.PublicCreateHandlerWrapper(sp);
            var methodName = GetHandlerDelegateMethodName(wrapper);

            if (enableStreaming)
                Assert.Contains("StreamingFunctionHandlerAsync", methodName);
            else
            {
                Assert.Contains("FunctionHandlerAsync", methodName);
                Assert.DoesNotContain("Streaming", methodName);
            }
        }, iter: 100);
    }
}

#endif
