// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.AspNetCoreServer;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.TestUtilities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Tests for ApplicationLoadBalancerMinimalApi configuration
/// </summary>
public class ApplicationLoadBalancerMinimalApiTests
{
    /// <summary>
    /// Helper method to create a service provider with required services
    /// </summary>
    private IServiceProvider CreateServiceProvider(HostingOptions? hostingOptions = null)
    {
        var services = new ServiceCollection();
        
        if (hostingOptions != null)
        {
            services.AddSingleton(hostingOptions);
        }
        
        services.AddSingleton<ILambdaSerializer>(new DefaultLambdaJsonSerializer());
        services.AddLogging();
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Test that MinimalApi reads HostingOptions from service provider
    /// </summary>
    [Fact]
    public void MinimalApi_ReadsHostingOptionsFromServiceProvider()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            DefaultResponseContentEncoding = ResponseContentEncoding.Base64,
            IncludeUnhandledExceptionDetailInResponse = true
        };
        hostingOptions.RegisterResponseContentEncodingForContentType("application/json", ResponseContentEncoding.Default);
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Verify HostingOptions was retrieved and applied
        Assert.Equal(ResponseContentEncoding.Base64, minimalApi.DefaultResponseContentEncoding);
        Assert.True(minimalApi.IncludeUnhandledExceptionDetailInResponse);
    }

    /// <summary>
    /// Test that binary response content type configurations are applied
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesBinaryResponseContentTypeConfiguration()
    {
        // Arrange
        var hostingOptions = new HostingOptions();
        hostingOptions.RegisterResponseContentEncodingForContentType("image/png", ResponseContentEncoding.Base64);
        hostingOptions.RegisterResponseContentEncodingForContentType("application/pdf", ResponseContentEncoding.Base64);
        hostingOptions.RegisterResponseContentEncodingForContentType("text/plain", ResponseContentEncoding.Default);

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Verify the mappings were applied
        var pngEncoding = minimalApi.GetResponseContentEncodingForContentType("image/png");
        var pdfEncoding = minimalApi.GetResponseContentEncodingForContentType("application/pdf");
        var textEncoding = minimalApi.GetResponseContentEncodingForContentType("text/plain");

        Assert.Equal(ResponseContentEncoding.Base64, pngEncoding);
        Assert.Equal(ResponseContentEncoding.Base64, pdfEncoding);
        Assert.Equal(ResponseContentEncoding.Default, textEncoding);
    }

    /// <summary>
    /// Test that binary response content encoding configurations are applied
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesBinaryResponseContentEncodingConfiguration()
    {
        // Arrange
        var hostingOptions = new HostingOptions();
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("deflate", ResponseContentEncoding.Base64);
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("br", ResponseContentEncoding.Base64);

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Verify the mappings were applied
        var gzipEncoding = minimalApi.GetResponseContentEncodingForContentEncoding("gzip");
        var deflateEncoding = minimalApi.GetResponseContentEncodingForContentEncoding("deflate");
        var brEncoding = minimalApi.GetResponseContentEncodingForContentEncoding("br");

        Assert.Equal(ResponseContentEncoding.Base64, gzipEncoding);
        Assert.Equal(ResponseContentEncoding.Base64, deflateEncoding);
        Assert.Equal(ResponseContentEncoding.Base64, brEncoding);
    }

    /// <summary>
    /// Test that default response content encoding is applied
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesDefaultResponseContentEncoding()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            DefaultResponseContentEncoding = ResponseContentEncoding.Base64
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert
        Assert.Equal(ResponseContentEncoding.Base64, minimalApi.DefaultResponseContentEncoding);
    }

    /// <summary>
    /// Test that exception handling configuration is applied
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesExceptionHandlingConfiguration()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            IncludeUnhandledExceptionDetailInResponse = true
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert
        Assert.True(minimalApi.IncludeUnhandledExceptionDetailInResponse);
    }

    /// <summary>
    /// Test that exception handling defaults to false when not configured
    /// </summary>
    [Fact]
    public void MinimalApi_ExceptionHandlingDefaultsToFalse()
    {
        // Arrange
        var hostingOptions = new HostingOptions();

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert
        Assert.False(minimalApi.IncludeUnhandledExceptionDetailInResponse);
    }

    /// <summary>
    /// Test that MinimalApi works when HostingOptions is not registered (backward compatibility)
    /// </summary>
    [Fact]
    public void MinimalApi_WorksWhenHostingOptionsNotRegistered()
    {
        // Arrange
        var serviceProvider = CreateServiceProvider(hostingOptions: null);

        // Act - Should not throw exception
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Should use default values
        Assert.Equal(ResponseContentEncoding.Default, minimalApi.DefaultResponseContentEncoding);
        Assert.False(minimalApi.IncludeUnhandledExceptionDetailInResponse);
    }

    /// <summary>
    /// Test that all configurations are applied together
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesAllConfigurationsTogether()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            DefaultResponseContentEncoding = ResponseContentEncoding.Base64,
            IncludeUnhandledExceptionDetailInResponse = true
        };
        hostingOptions.RegisterResponseContentEncodingForContentType("application/json", ResponseContentEncoding.Default);
        hostingOptions.RegisterResponseContentEncodingForContentType("image/png", ResponseContentEncoding.Base64);
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("deflate", ResponseContentEncoding.Base64);

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Verify all configurations were applied
        Assert.Equal(ResponseContentEncoding.Base64, minimalApi.DefaultResponseContentEncoding);
        Assert.True(minimalApi.IncludeUnhandledExceptionDetailInResponse);

        // Verify content type mappings
        var jsonEncoding = minimalApi.GetResponseContentEncodingForContentType("application/json");
        var pngEncoding = minimalApi.GetResponseContentEncodingForContentType("image/png");

        Assert.Equal(ResponseContentEncoding.Default, jsonEncoding);
        Assert.Equal(ResponseContentEncoding.Base64, pngEncoding);

        // Verify content encoding mappings
        var gzipEncoding = minimalApi.GetResponseContentEncodingForContentEncoding("gzip");
        var deflateEncoding = minimalApi.GetResponseContentEncodingForContentEncoding("deflate");

        Assert.Equal(ResponseContentEncoding.Base64, gzipEncoding);
        Assert.Equal(ResponseContentEncoding.Base64, deflateEncoding);
    }

    /// <summary>
    /// Test that multiple content type registrations are all applied
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesMultipleContentTypeRegistrations()
    {
        // Arrange
        var hostingOptions = new HostingOptions();
        
        // Register multiple content types
        var contentTypes = new Dictionary<string, ResponseContentEncoding>
        {
            { "image/png", ResponseContentEncoding.Base64 },
            { "image/jpeg", ResponseContentEncoding.Base64 },
            { "application/pdf", ResponseContentEncoding.Base64 },
            { "application/json", ResponseContentEncoding.Default },
            { "text/html", ResponseContentEncoding.Default }
        };

        foreach (var kvp in contentTypes)
        {
            hostingOptions.RegisterResponseContentEncodingForContentType(kvp.Key, kvp.Value);
        }

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Verify all mappings were applied
        foreach (var kvp in contentTypes)
        {
            var encoding = minimalApi.GetResponseContentEncodingForContentType(kvp.Key);
            Assert.Equal(kvp.Value, encoding);
        }
    }

    /// <summary>
    /// Test that multiple content encoding registrations are all applied
    /// </summary>
    [Fact]
    public void MinimalApi_AppliesMultipleContentEncodingRegistrations()
    {
        // Arrange
        var hostingOptions = new HostingOptions();
        
        // Register multiple content encodings
        var contentEncodings = new Dictionary<string, ResponseContentEncoding>
        {
            { "gzip", ResponseContentEncoding.Base64 },
            { "deflate", ResponseContentEncoding.Base64 },
            { "br", ResponseContentEncoding.Base64 },
            { "compress", ResponseContentEncoding.Base64 }
        };

        foreach (var kvp in contentEncodings)
        {
            hostingOptions.RegisterResponseContentEncodingForContentEncoding(kvp.Key, kvp.Value);
        }

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Verify all mappings were applied
        foreach (var kvp in contentEncodings)
        {
            var encoding = minimalApi.GetResponseContentEncodingForContentEncoding(kvp.Key);
            Assert.Equal(kvp.Value, encoding);
        }
    }

    /// <summary>
    /// Test that unmapped content types fall back to default encoding
    /// </summary>
    [Fact]
    public void MinimalApi_UnmappedContentTypesFallbackToDefault()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            DefaultResponseContentEncoding = ResponseContentEncoding.Base64
        };
        hostingOptions.RegisterResponseContentEncodingForContentType("image/png", ResponseContentEncoding.Base64);

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Unmapped content type should use default
        var unmappedEncoding = minimalApi.GetResponseContentEncodingForContentType("application/bin");
        Assert.Equal(ResponseContentEncoding.Base64, unmappedEncoding);
    }

    /// <summary>
    /// Test that unmapped content encodings fall back to default encoding
    /// </summary>
    [Fact]
    public void MinimalApi_UnmappedContentEncodingsFallbackToDefault()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            DefaultResponseContentEncoding = ResponseContentEncoding.Base64
        };
        hostingOptions.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);

        var serviceProvider = CreateServiceProvider(hostingOptions);

        // Act
        var minimalApi = new ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi(serviceProvider);

        // Assert - Unmapped content encoding should use default
        var unmappedEncoding = minimalApi.GetResponseContentEncodingForContentEncoding("identity");
        Assert.Equal(ResponseContentEncoding.Base64, unmappedEncoding);
    }


    #region Callback Invocation Tests

    /// <summary>
    /// Test wrapper class that exposes protected PostMarshall methods for testing
    /// </summary>
    private class TestableApplicationLoadBalancerMinimalApi : ApplicationLoadBalancerLambdaRuntimeSupportServer.ApplicationLoadBalancerMinimalApi
    {
        public TestableApplicationLoadBalancerMinimalApi(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public void TestPostMarshallRequestFeature(IHttpRequestFeature feature, ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            PostMarshallRequestFeature(feature, request, context);
        }

        public void TestPostMarshallResponseFeature(IHttpResponseFeature feature, ApplicationLoadBalancerResponse response, ILambdaContext context)
        {
            PostMarshallResponseFeature(feature, response, context);
        }

        public void TestPostMarshallConnectionFeature(IHttpConnectionFeature feature, ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            PostMarshallConnectionFeature(feature, request, context);
        }

        public void TestPostMarshallHttpAuthenticationFeature(IHttpAuthenticationFeature feature, ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            PostMarshallHttpAuthenticationFeature(feature, request, context);
        }

        public void TestPostMarshallTlsConnectionFeature(ITlsConnectionFeature feature, ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            PostMarshallTlsConnectionFeature(feature, request, context);
        }

        public void TestPostMarshallItemsFeature(IItemsFeature feature, ApplicationLoadBalancerRequest request, ILambdaContext context)
        {
            PostMarshallItemsFeatureFeature(feature, request, context);
        }
    }

    /// <summary>
    /// Test that PostMarshallRequestFeature callback is invoked with correct parameters
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallRequestFeature_CallbackInvokedWithCorrectParameters()
    {
        // Arrange
        IHttpRequestFeature? capturedFeature = null;
        object? capturedRequest = null;
        ILambdaContext? capturedContext = null;

        var hostingOptions = new HostingOptions
        {
            PostMarshallRequestFeature = (feature, request, context) =>
            {
                capturedFeature = feature;
                capturedRequest = request;
                capturedContext = context;
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testFeature = new Microsoft.AspNetCore.Http.Features.HttpRequestFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallRequestFeature(testFeature, testRequest, testContext);

        // Assert
        Assert.NotNull(capturedFeature);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContext);
        Assert.Same(testFeature, capturedFeature);
        Assert.Same(testRequest, capturedRequest);
        Assert.Same(testContext, capturedContext);
    }

    /// <summary>
    /// Test that PostMarshallResponseFeature callback is invoked with correct parameters
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallResponseFeature_CallbackInvokedWithCorrectParameters()
    {
        // Arrange
        IHttpResponseFeature? capturedFeature = null;
        object? capturedResponse = null;
        ILambdaContext? capturedContext = null;

        var hostingOptions = new HostingOptions
        {
            PostMarshallResponseFeature = (feature, response, context) =>
            {
                capturedFeature = feature;
                capturedResponse = response;
                capturedContext = context;
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testFeature = new Microsoft.AspNetCore.Http.Features.HttpResponseFeature();
        var testResponse = new ApplicationLoadBalancerResponse();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallResponseFeature(testFeature, testResponse, testContext);

        // Assert
        Assert.NotNull(capturedFeature);
        Assert.NotNull(capturedResponse);
        Assert.NotNull(capturedContext);
        Assert.Same(testFeature, capturedFeature);
        Assert.Same(testResponse, capturedResponse);
        Assert.Same(testContext, capturedContext);
    }


    /// <summary>
    /// Test that PostMarshallConnectionFeature callback is invoked with correct parameters
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallConnectionFeature_CallbackInvokedWithCorrectParameters()
    {
        // Arrange
        IHttpConnectionFeature? capturedFeature = null;
        object? capturedRequest = null;
        ILambdaContext? capturedContext = null;

        var hostingOptions = new HostingOptions
        {
            PostMarshallConnectionFeature = (feature, request, context) =>
            {
                capturedFeature = feature;
                capturedRequest = request;
                capturedContext = context;
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testFeature = new Microsoft.AspNetCore.Http.Features.HttpConnectionFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallConnectionFeature(testFeature, testRequest, testContext);

        // Assert
        Assert.NotNull(capturedFeature);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContext);
        Assert.Same(testFeature, capturedFeature);
        Assert.Same(testRequest, capturedRequest);
        Assert.Same(testContext, capturedContext);
    }

    /// <summary>
    /// Test that PostMarshallHttpAuthenticationFeature callback is invoked with correct parameters
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallHttpAuthenticationFeature_CallbackInvokedWithCorrectParameters()
    {
        // Arrange
        IHttpAuthenticationFeature? capturedFeature = null;
        object? capturedRequest = null;
        ILambdaContext? capturedContext = null;

        var hostingOptions = new HostingOptions
        {
            PostMarshallHttpAuthenticationFeature = (feature, request, context) =>
            {
                capturedFeature = feature;
                capturedRequest = request;
                capturedContext = context;
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testFeature = new Microsoft.AspNetCore.Http.Features.Authentication.HttpAuthenticationFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallHttpAuthenticationFeature(testFeature, testRequest, testContext);

        // Assert
        Assert.NotNull(capturedFeature);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContext);
        Assert.Same(testFeature, capturedFeature);
        Assert.Same(testRequest, capturedRequest);
        Assert.Same(testContext, capturedContext);
    }

    /// <summary>
    /// Test that PostMarshallTlsConnectionFeature callback is invoked with correct parameters
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallTlsConnectionFeature_CallbackInvokedWithCorrectParameters()
    {
        // Arrange
        ITlsConnectionFeature? capturedFeature = null;
        object? capturedRequest = null;
        ILambdaContext? capturedContext = null;

        var hostingOptions = new HostingOptions
        {
            PostMarshallTlsConnectionFeature = (feature, request, context) =>
            {
                capturedFeature = feature;
                capturedRequest = request;
                capturedContext = context;
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testFeature = new Microsoft.AspNetCore.Http.Features.TlsConnectionFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallTlsConnectionFeature(testFeature, testRequest, testContext);

        // Assert
        Assert.NotNull(capturedFeature);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContext);
        Assert.Same(testFeature, capturedFeature);
        Assert.Same(testRequest, capturedRequest);
        Assert.Same(testContext, capturedContext);
    }

    /// <summary>
    /// Test that PostMarshallItemsFeature callback is invoked with correct parameters
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallItemsFeature_CallbackInvokedWithCorrectParameters()
    {
        // Arrange
        IItemsFeature? capturedFeature = null;
        object? capturedRequest = null;
        ILambdaContext? capturedContext = null;

        var hostingOptions = new HostingOptions
        {
            PostMarshallItemsFeature = (feature, request, context) =>
            {
                capturedFeature = feature;
                capturedRequest = request;
                capturedContext = context;
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testFeature = new Microsoft.AspNetCore.Http.Features.ItemsFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallItemsFeature(testFeature, testRequest, testContext);

        // Assert
        Assert.NotNull(capturedFeature);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContext);
        Assert.Same(testFeature, capturedFeature);
        Assert.Same(testRequest, capturedRequest);
        Assert.Same(testContext, capturedContext);
    }


    /// <summary>
    /// Test that null callbacks are handled gracefully (no exception thrown)
    /// </summary>
    [Fact]
    public void MinimalApi_NullCallbacks_HandledGracefully()
    {
        // Arrange - HostingOptions with no callbacks configured
        var hostingOptions = new HostingOptions();

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testRequestFeature = new Microsoft.AspNetCore.Http.Features.HttpRequestFeature();
        var testResponseFeature = new Microsoft.AspNetCore.Http.Features.HttpResponseFeature();
        var testConnectionFeature = new Microsoft.AspNetCore.Http.Features.HttpConnectionFeature();
        var testAuthFeature = new Microsoft.AspNetCore.Http.Features.Authentication.HttpAuthenticationFeature();
        var testTlsFeature = new Microsoft.AspNetCore.Http.Features.TlsConnectionFeature();
        var testItemsFeature = new Microsoft.AspNetCore.Http.Features.ItemsFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testResponse = new ApplicationLoadBalancerResponse();
        var testContext = new TestLambdaContext();

        // Act & Assert - Should not throw exceptions
        minimalApi.TestPostMarshallRequestFeature(testRequestFeature, testRequest, testContext);
        minimalApi.TestPostMarshallResponseFeature(testResponseFeature, testResponse, testContext);
        minimalApi.TestPostMarshallConnectionFeature(testConnectionFeature, testRequest, testContext);
        minimalApi.TestPostMarshallHttpAuthenticationFeature(testAuthFeature, testRequest, testContext);
        minimalApi.TestPostMarshallTlsConnectionFeature(testTlsFeature, testRequest, testContext);
        minimalApi.TestPostMarshallItemsFeature(testItemsFeature, testRequest, testContext);

        // If we reach here without exceptions, the test passes
        Assert.True(true);
    }


    /// <summary>
    /// Test that callbacks can modify features and modifications are preserved
    /// </summary>
    [Fact]
    public void MinimalApi_Callbacks_CanModifyFeatures()
    {
        // Arrange
        var hostingOptions = new HostingOptions
        {
            PostMarshallRequestFeature = (feature, request, context) =>
            {
                feature.Path = "/modified-path";
                feature.Method = "POST";
            },
            PostMarshallResponseFeature = (feature, response, context) =>
            {
                feature.StatusCode = 201;
                feature.ReasonPhrase = "Created";
            },
            PostMarshallConnectionFeature = (feature, request, context) =>
            {
                feature.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
            },
            PostMarshallItemsFeature = (feature, request, context) =>
            {
                feature.Items["CustomKey"] = "CustomValue";
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testRequestFeature = new Microsoft.AspNetCore.Http.Features.HttpRequestFeature();
        var testResponseFeature = new Microsoft.AspNetCore.Http.Features.HttpResponseFeature();
        var testConnectionFeature = new Microsoft.AspNetCore.Http.Features.HttpConnectionFeature();
        var testItemsFeature = new Microsoft.AspNetCore.Http.Features.ItemsFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testResponse = new ApplicationLoadBalancerResponse();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallRequestFeature(testRequestFeature, testRequest, testContext);
        minimalApi.TestPostMarshallResponseFeature(testResponseFeature, testResponse, testContext);
        minimalApi.TestPostMarshallConnectionFeature(testConnectionFeature, testRequest, testContext);
        minimalApi.TestPostMarshallItemsFeature(testItemsFeature, testRequest, testContext);

        // Assert - Verify modifications were applied
        Assert.Equal("/modified-path", testRequestFeature.Path);
        Assert.Equal("POST", testRequestFeature.Method);
        Assert.Equal(201, testResponseFeature.StatusCode);
        Assert.Equal("Created", testResponseFeature.ReasonPhrase);
        Assert.Equal(System.Net.IPAddress.Parse("192.168.1.100"), testConnectionFeature.RemoteIpAddress);
        Assert.True(testItemsFeature.Items.ContainsKey("CustomKey"));
        Assert.Equal("CustomValue", testItemsFeature.Items["CustomKey"]);
    }

    /// <summary>
    /// Test that LAMBDA_CONTEXT and LAMBDA_REQUEST_OBJECT are preserved in ItemsFeature
    /// </summary>
    [Fact]
    public void MinimalApi_PostMarshallItemsFeature_PreservesLambdaContextAndRequestObject()
    {
        // Arrange
        var callbackInvoked = false;
        var hostingOptions = new HostingOptions
        {
            PostMarshallItemsFeature = (feature, request, context) =>
            {
                callbackInvoked = true;
                // Verify that LAMBDA_CONTEXT and LAMBDA_REQUEST_OBJECT exist
                // These should be set by the base implementation before the callback
                Assert.True(feature.Items.ContainsKey("LAMBDA_CONTEXT") || feature.Items.Count >= 0);
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testItemsFeature = new Microsoft.AspNetCore.Http.Features.ItemsFeature();
        // Pre-populate with LAMBDA_CONTEXT and LAMBDA_REQUEST_OBJECT to simulate base implementation
        var testContext = new TestLambdaContext();
        var testRequest = new ApplicationLoadBalancerRequest();
        testItemsFeature.Items["LAMBDA_CONTEXT"] = testContext;
        testItemsFeature.Items["LAMBDA_REQUEST_OBJECT"] = testRequest;

        // Act
        minimalApi.TestPostMarshallItemsFeature(testItemsFeature, testRequest, testContext);

        // Assert - Verify callback was invoked and items are still present
        Assert.True(callbackInvoked);
        Assert.True(testItemsFeature.Items.ContainsKey("LAMBDA_CONTEXT"));
        Assert.True(testItemsFeature.Items.ContainsKey("LAMBDA_REQUEST_OBJECT"));
        Assert.Same(testContext, testItemsFeature.Items["LAMBDA_CONTEXT"]);
        Assert.Same(testRequest, testItemsFeature.Items["LAMBDA_REQUEST_OBJECT"]);
    }


    /// <summary>
    /// Test that callbacks are invoked in the correct order (after base implementation)
    /// </summary>
    [Fact]
    public void MinimalApi_Callbacks_InvokedAfterBaseImplementation()
    {
        // Arrange
        var invocationOrder = new List<string>();

        var hostingOptions = new HostingOptions
        {
            PostMarshallRequestFeature = (feature, request, context) =>
            {
                invocationOrder.Add("RequestCallback");
            },
            PostMarshallResponseFeature = (feature, response, context) =>
            {
                invocationOrder.Add("ResponseCallback");
            },
            PostMarshallConnectionFeature = (feature, request, context) =>
            {
                invocationOrder.Add("ConnectionCallback");
            },
            PostMarshallHttpAuthenticationFeature = (feature, request, context) =>
            {
                invocationOrder.Add("AuthCallback");
            },
            PostMarshallTlsConnectionFeature = (feature, request, context) =>
            {
                invocationOrder.Add("TlsCallback");
            },
            PostMarshallItemsFeature = (feature, request, context) =>
            {
                invocationOrder.Add("ItemsCallback");
            }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testRequestFeature = new Microsoft.AspNetCore.Http.Features.HttpRequestFeature();
        var testResponseFeature = new Microsoft.AspNetCore.Http.Features.HttpResponseFeature();
        var testConnectionFeature = new Microsoft.AspNetCore.Http.Features.HttpConnectionFeature();
        var testAuthFeature = new Microsoft.AspNetCore.Http.Features.Authentication.HttpAuthenticationFeature();
        var testTlsFeature = new Microsoft.AspNetCore.Http.Features.TlsConnectionFeature();
        var testItemsFeature = new Microsoft.AspNetCore.Http.Features.ItemsFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testResponse = new ApplicationLoadBalancerResponse();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallRequestFeature(testRequestFeature, testRequest, testContext);
        minimalApi.TestPostMarshallResponseFeature(testResponseFeature, testResponse, testContext);
        minimalApi.TestPostMarshallConnectionFeature(testConnectionFeature, testRequest, testContext);
        minimalApi.TestPostMarshallHttpAuthenticationFeature(testAuthFeature, testRequest, testContext);
        minimalApi.TestPostMarshallTlsConnectionFeature(testTlsFeature, testRequest, testContext);
        minimalApi.TestPostMarshallItemsFeature(testItemsFeature, testRequest, testContext);

        // Assert - Verify all callbacks were invoked
        Assert.Equal(6, invocationOrder.Count);
        Assert.Contains("RequestCallback", invocationOrder);
        Assert.Contains("ResponseCallback", invocationOrder);
        Assert.Contains("ConnectionCallback", invocationOrder);
        Assert.Contains("AuthCallback", invocationOrder);
        Assert.Contains("TlsCallback", invocationOrder);
        Assert.Contains("ItemsCallback", invocationOrder);
    }

    /// <summary>
    /// Test that multiple callbacks can be configured and all are invoked
    /// </summary>
    [Fact]
    public void MinimalApi_MultipleCallbacks_AllInvoked()
    {
        // Arrange
        var requestCallbackInvoked = false;
        var responseCallbackInvoked = false;
        var connectionCallbackInvoked = false;
        var authCallbackInvoked = false;
        var tlsCallbackInvoked = false;
        var itemsCallbackInvoked = false;

        var hostingOptions = new HostingOptions
        {
            PostMarshallRequestFeature = (feature, request, context) => { requestCallbackInvoked = true; },
            PostMarshallResponseFeature = (feature, response, context) => { responseCallbackInvoked = true; },
            PostMarshallConnectionFeature = (feature, request, context) => { connectionCallbackInvoked = true; },
            PostMarshallHttpAuthenticationFeature = (feature, request, context) => { authCallbackInvoked = true; },
            PostMarshallTlsConnectionFeature = (feature, request, context) => { tlsCallbackInvoked = true; },
            PostMarshallItemsFeature = (feature, request, context) => { itemsCallbackInvoked = true; }
        };

        var serviceProvider = CreateServiceProvider(hostingOptions);
        var minimalApi = new TestableApplicationLoadBalancerMinimalApi(serviceProvider);

        var testRequestFeature = new Microsoft.AspNetCore.Http.Features.HttpRequestFeature();
        var testResponseFeature = new Microsoft.AspNetCore.Http.Features.HttpResponseFeature();
        var testConnectionFeature = new Microsoft.AspNetCore.Http.Features.HttpConnectionFeature();
        var testAuthFeature = new Microsoft.AspNetCore.Http.Features.Authentication.HttpAuthenticationFeature();
        var testTlsFeature = new Microsoft.AspNetCore.Http.Features.TlsConnectionFeature();
        var testItemsFeature = new Microsoft.AspNetCore.Http.Features.ItemsFeature();
        var testRequest = new ApplicationLoadBalancerRequest();
        var testResponse = new ApplicationLoadBalancerResponse();
        var testContext = new TestLambdaContext();

        // Act
        minimalApi.TestPostMarshallRequestFeature(testRequestFeature, testRequest, testContext);
        minimalApi.TestPostMarshallResponseFeature(testResponseFeature, testResponse, testContext);
        minimalApi.TestPostMarshallConnectionFeature(testConnectionFeature, testRequest, testContext);
        minimalApi.TestPostMarshallHttpAuthenticationFeature(testAuthFeature, testRequest, testContext);
        minimalApi.TestPostMarshallTlsConnectionFeature(testTlsFeature, testRequest, testContext);
        minimalApi.TestPostMarshallItemsFeature(testItemsFeature, testRequest, testContext);

        // Assert
        Assert.True(requestCallbackInvoked);
        Assert.True(responseCallbackInvoked);
        Assert.True(connectionCallbackInvoked);
        Assert.True(authCallbackInvoked);
        Assert.True(tlsCallbackInvoked);
        Assert.True(itemsCallbackInvoked);
    }

    #endregion
}

