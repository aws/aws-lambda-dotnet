// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.Lambda.AspNetCoreServer.Test;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Tests for service registration in <see cref="ServiceCollectionExtensions"/>
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAWSLambdaHosting_WithConfiguration_RegistersHostingOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
        {
            options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.NotNull(hostingOptions);
        Assert.Equal(ResponseContentEncoding.Base64, hostingOptions.DefaultResponseContentEncoding);
    }

    [Fact]
    public void AddAWSLambdaHosting_WithoutConfiguration_RegistersHostingOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.NotNull(hostingOptions);
        Assert.Equal(ResponseContentEncoding.Default, hostingOptions.DefaultResponseContentEncoding);
    }

    [Fact]
    public void AddAWSLambdaHosting_WithNullConfiguration_RegistersHostingOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, configure: null);

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.NotNull(hostingOptions);
    }

    [Fact]
    public void AddAWSLambdaHosting_RegistersHostingOptionsAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
        {
            options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert - Get the service twice and verify it's the same instance
        var hostingOptions1 = serviceProvider.GetService<HostingOptions>();
        var hostingOptions2 = serviceProvider.GetService<HostingOptions>();
        
        Assert.NotNull(hostingOptions1);
        Assert.NotNull(hostingOptions2);
        Assert.Same(hostingOptions1, hostingOptions2);
    }

    [Fact]
    public void AddAWSLambdaHosting_RestApi_RegistersHostingOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.RestApi, options =>
        {
            options.IncludeUnhandledExceptionDetailInResponse = true;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.NotNull(hostingOptions);
        Assert.True(hostingOptions.IncludeUnhandledExceptionDetailInResponse);
    }

    [Fact]
    public void AddAWSLambdaHosting_ApplicationLoadBalancer_RegistersHostingOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.ApplicationLoadBalancer, options =>
        {
            options.RegisterResponseContentEncodingForContentType("image/png", ResponseContentEncoding.Base64);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.NotNull(hostingOptions);
        Assert.True(hostingOptions.ContentTypeEncodings.ContainsKey("image/png"));
    }

    [Fact]
    public void AddAWSLambdaHosting_NotInLambda_DoesNotRegisterHostingOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        // No AWS_LAMBDA_FUNCTION_NAME environment variable set

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
        {
            options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.Null(hostingOptions);
    }

    [Fact]
    public void AddAWSLambdaHosting_ConfigurationIsApplied()
    {
        // Arrange
        var services = new ServiceCollection();
        using var envHelper = new EnvironmentVariableHelper("AWS_LAMBDA_FUNCTION_NAME", "test-function");

        // Act
        services.AddAWSLambdaHosting(LambdaEventSource.HttpApi, options =>
        {
            options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;
            options.IncludeUnhandledExceptionDetailInResponse = true;
            options.RegisterResponseContentEncodingForContentType("application/json", ResponseContentEncoding.Default);
            options.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostingOptions = serviceProvider.GetService<HostingOptions>();
        Assert.NotNull(hostingOptions);
        Assert.Equal(ResponseContentEncoding.Base64, hostingOptions.DefaultResponseContentEncoding);
        Assert.True(hostingOptions.IncludeUnhandledExceptionDetailInResponse);
        Assert.True(hostingOptions.ContentTypeEncodings.ContainsKey("application/json"));
        Assert.True(hostingOptions.ContentEncodingEncodings.ContainsKey("gzip"));
    }
}
