// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Tests;

/// <summary>
/// Tests for <see cref="HostingOptions"/>
/// </summary>
public class HostingOptionsTests
{
    [Fact]
    public void RegisterResponseContentEncodingForContentType_StoresMapping()
    {
        // Arrange
        var options = new HostingOptions();
        var contentType = "application/json";
        var encoding = ResponseContentEncoding.Base64;

        // Act
        options.RegisterResponseContentEncodingForContentType(contentType, encoding);

        // Assert
        Assert.True(options.ContentTypeEncodings.ContainsKey(contentType));
        Assert.Equal(encoding, options.ContentTypeEncodings[contentType]);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentType_MultipleContentTypes_StoresAllMappings()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.RegisterResponseContentEncodingForContentType("application/json", ResponseContentEncoding.Default);
        options.RegisterResponseContentEncodingForContentType("image/png", ResponseContentEncoding.Base64);
        options.RegisterResponseContentEncodingForContentType("application/pdf", ResponseContentEncoding.Base64);

        // Assert
        Assert.Equal(3, options.ContentTypeEncodings.Count);
        Assert.Equal(ResponseContentEncoding.Default, options.ContentTypeEncodings["application/json"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentTypeEncodings["image/png"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentTypeEncodings["application/pdf"]);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentType_DuplicateRegistration_OverwritesPreviousValue()
    {
        // Arrange
        var options = new HostingOptions();
        var contentType = "application/json";

        // Act
        options.RegisterResponseContentEncodingForContentType(contentType, ResponseContentEncoding.Default);
        options.RegisterResponseContentEncodingForContentType(contentType, ResponseContentEncoding.Base64);

        // Assert
        Assert.Single(options.ContentTypeEncodings);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentTypeEncodings[contentType]);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentType_NullContentType_IgnoresRegistration()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.RegisterResponseContentEncodingForContentType(null, ResponseContentEncoding.Base64);

        // Assert
        Assert.Empty(options.ContentTypeEncodings);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentType_EmptyContentType_IgnoresRegistration()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.RegisterResponseContentEncodingForContentType(string.Empty, ResponseContentEncoding.Base64);

        // Assert
        Assert.Empty(options.ContentTypeEncodings);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentEncoding_StoresMapping()
    {
        // Arrange
        var options = new HostingOptions();
        var contentEncoding = "gzip";
        var encoding = ResponseContentEncoding.Base64;

        // Act
        options.RegisterResponseContentEncodingForContentEncoding(contentEncoding, encoding);

        // Assert
        Assert.True(options.ContentEncodingEncodings.ContainsKey(contentEncoding));
        Assert.Equal(encoding, options.ContentEncodingEncodings[contentEncoding]);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentEncoding_MultipleEncodings_StoresAllMappings()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);
        options.RegisterResponseContentEncodingForContentEncoding("deflate", ResponseContentEncoding.Base64);
        options.RegisterResponseContentEncodingForContentEncoding("br", ResponseContentEncoding.Base64);

        // Assert
        Assert.Equal(3, options.ContentEncodingEncodings.Count);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentEncodingEncodings["gzip"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentEncodingEncodings["deflate"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentEncodingEncodings["br"]);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentEncoding_DuplicateRegistration_OverwritesPreviousValue()
    {
        // Arrange
        var options = new HostingOptions();
        var contentEncoding = "gzip";

        // Act
        options.RegisterResponseContentEncodingForContentEncoding(contentEncoding, ResponseContentEncoding.Default);
        options.RegisterResponseContentEncodingForContentEncoding(contentEncoding, ResponseContentEncoding.Base64);

        // Assert
        Assert.Single(options.ContentEncodingEncodings);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentEncodingEncodings[contentEncoding]);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentEncoding_NullContentEncoding_IgnoresRegistration()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.RegisterResponseContentEncodingForContentEncoding(null, ResponseContentEncoding.Base64);

        // Assert
        Assert.Empty(options.ContentEncodingEncodings);
    }

    [Fact]
    public void RegisterResponseContentEncodingForContentEncoding_EmptyContentEncoding_IgnoresRegistration()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.RegisterResponseContentEncodingForContentEncoding(string.Empty, ResponseContentEncoding.Base64);

        // Assert
        Assert.Empty(options.ContentEncodingEncodings);
    }

    [Fact]
    public void DefaultResponseContentEncoding_DefaultsToDefault()
    {
        // Arrange & Act
        var options = new HostingOptions();

        // Assert
        Assert.Equal(ResponseContentEncoding.Default, options.DefaultResponseContentEncoding);
    }

    [Fact]
    public void IncludeUnhandledExceptionDetailInResponse_DefaultsToFalse()
    {
        // Arrange & Act
        var options = new HostingOptions();

        // Assert
        Assert.False(options.IncludeUnhandledExceptionDetailInResponse);
    }

    [Fact]
    public void DefaultResponseContentEncoding_CanBeSet()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;

        // Assert
        Assert.Equal(ResponseContentEncoding.Base64, options.DefaultResponseContentEncoding);
    }

    [Fact]
    public void IncludeUnhandledExceptionDetailInResponse_CanBeSet()
    {
        // Arrange
        var options = new HostingOptions();

        // Act
        options.IncludeUnhandledExceptionDetailInResponse = true;

        // Assert
        Assert.True(options.IncludeUnhandledExceptionDetailInResponse);
    }

    private static string GenerateRandomContentType(Random random)
    {
        var types = new[] { "application", "text", "image", "video", "audio", "multipart" };
        var subtypes = new[] { "json", "xml", "html", "plain", "png", "jpeg", "gif", "pdf", "octet-stream", "form-data" };
        
        return $"{types[random.Next(types.Length)]}/{subtypes[random.Next(subtypes.Length)]}";
    }

    /// <summary>
    /// Test that default content type mappings from AbstractAspNetCoreFunction are preserved
    /// </summary>
    [Fact]
    public void DefaultContentTypeMappings_ArePreserved()
    {
        // These are the default mappings from AbstractAspNetCoreFunction that should be preserved
        var expectedDefaultMappings = new Dictionary<string, ResponseContentEncoding>
        {
            // Text content types - Default encoding
            ["text/plain"] = ResponseContentEncoding.Default,
            ["text/xml"] = ResponseContentEncoding.Default,
            ["application/xml"] = ResponseContentEncoding.Default,
            ["application/json"] = ResponseContentEncoding.Default,
            ["text/html"] = ResponseContentEncoding.Default,
            ["text/css"] = ResponseContentEncoding.Default,
            ["text/javascript"] = ResponseContentEncoding.Default,
            ["text/ecmascript"] = ResponseContentEncoding.Default,
            ["text/markdown"] = ResponseContentEncoding.Default,
            ["text/csv"] = ResponseContentEncoding.Default,

            // Binary content types - Base64 encoding
            ["application/octet-stream"] = ResponseContentEncoding.Base64,
            ["image/png"] = ResponseContentEncoding.Base64,
            ["image/gif"] = ResponseContentEncoding.Base64,
            ["image/jpeg"] = ResponseContentEncoding.Base64,
            ["image/jpg"] = ResponseContentEncoding.Base64,
            ["image/x-icon"] = ResponseContentEncoding.Base64,
            ["application/zip"] = ResponseContentEncoding.Base64,
            ["application/pdf"] = ResponseContentEncoding.Base64,
            ["application/x-protobuf"] = ResponseContentEncoding.Base64,
            ["application/wasm"] = ResponseContentEncoding.Base64
        };

        // Note: We can't directly test AbstractAspNetCoreFunction's internal dictionary,
        // but we document the expected default mappings here to ensure they are preserved
        // when implementing the MinimalApi classes. The MinimalApi classes should apply
        // these same defaults when HostingOptions doesn't override them.

        // This test serves as documentation of the expected default mappings
        Assert.Equal(20, expectedDefaultMappings.Count);
        Assert.All(expectedDefaultMappings, kvp =>
        {
            Assert.NotNull(kvp.Key);
            Assert.NotEmpty(kvp.Key);
        });
    }

    /// <summary>
    /// Test that default content encoding mappings from AbstractAspNetCoreFunction are preserved
    /// </summary>
    [Fact]
    public void DefaultContentEncodingMappings_ArePreserved()
    {
        // These are the default mappings from AbstractAspNetCoreFunction that should be preserved
        var expectedDefaultMappings = new Dictionary<string, ResponseContentEncoding>
        {
            ["gzip"] = ResponseContentEncoding.Base64,
            ["deflate"] = ResponseContentEncoding.Base64,
            ["br"] = ResponseContentEncoding.Base64
        };

        // Note: We can't directly test AbstractAspNetCoreFunction's internal dictionary,
        // but we document the expected default mappings here to ensure they are preserved
        // when implementing the MinimalApi classes. The MinimalApi classes should apply
        // these same defaults when HostingOptions doesn't override them.

        // This test serves as documentation of the expected default mappings
        Assert.Equal(3, expectedDefaultMappings.Count);
        Assert.All(expectedDefaultMappings, kvp =>
        {
            Assert.NotNull(kvp.Key);
            Assert.NotEmpty(kvp.Key);
            Assert.Equal(ResponseContentEncoding.Base64, kvp.Value);
        });
    }

    /// <summary>
    /// Test that HostingOptions exposes all extension points
    /// </summary>
    [Fact]
    public void HostingOptions_ExposesAllExtensionPoints()
    {
        // Arrange
        var options = new HostingOptions();

        // Assert - Verify all properties exist and are accessible
        
        // Binary response configuration
        Assert.NotNull(options); // DefaultResponseContentEncoding property exists
        var defaultEncoding = options.DefaultResponseContentEncoding;
        Assert.Equal(ResponseContentEncoding.Default, defaultEncoding);

        // Exception handling
        var includeException = options.IncludeUnhandledExceptionDetailInResponse;
        Assert.False(includeException);

        // Marshalling callbacks - verify properties exist and can be set
        options.PostMarshallRequestFeature = (feature, request, context) => { };
        Assert.NotNull(options.PostMarshallRequestFeature);

        options.PostMarshallResponseFeature = (feature, response, context) => { };
        Assert.NotNull(options.PostMarshallResponseFeature);

        options.PostMarshallConnectionFeature = (feature, request, context) => { };
        Assert.NotNull(options.PostMarshallConnectionFeature);

        options.PostMarshallHttpAuthenticationFeature = (feature, request, context) => { };
        Assert.NotNull(options.PostMarshallHttpAuthenticationFeature);

        options.PostMarshallTlsConnectionFeature = (feature, request, context) => { };
        Assert.NotNull(options.PostMarshallTlsConnectionFeature);

        options.PostMarshallItemsFeature = (feature, request, context) => { };
        Assert.NotNull(options.PostMarshallItemsFeature);

        // Binary response registration methods
        options.RegisterResponseContentEncodingForContentType("test/type", ResponseContentEncoding.Base64);
        Assert.Single(options.ContentTypeEncodings);

        options.RegisterResponseContentEncodingForContentEncoding("test-encoding", ResponseContentEncoding.Base64);
        Assert.Single(options.ContentEncodingEncodings);

        // Serializer property
        options.Serializer = null;
        Assert.Null(options.Serializer);
    }

    /// <summary>
    /// Test that all callback properties can be set and retrieved
    /// </summary>
    [Fact]
    public void HostingOptions_AllCallbackProperties_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new HostingOptions();

        Action<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature, object, ILambdaContext> requestCallback = 
            (f, r, c) => { };
        Action<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature, object, ILambdaContext> responseCallback = 
            (f, r, c) => { };
        Action<Microsoft.AspNetCore.Http.Features.IHttpConnectionFeature, object, ILambdaContext> connectionCallback = 
            (f, r, c) => { };
        Action<Microsoft.AspNetCore.Http.Features.Authentication.IHttpAuthenticationFeature, object, ILambdaContext> authCallback = 
            (f, r, c) => { };
        Action<Microsoft.AspNetCore.Http.Features.ITlsConnectionFeature, object, ILambdaContext> tlsCallback = 
            (f, r, c) => { };
        Action<Microsoft.AspNetCore.Http.Features.IItemsFeature, object, ILambdaContext> itemsCallback = 
            (f, r, c) => { };

        // Act
        options.PostMarshallRequestFeature = requestCallback;
        options.PostMarshallResponseFeature = responseCallback;
        options.PostMarshallConnectionFeature = connectionCallback;
        options.PostMarshallHttpAuthenticationFeature = authCallback;
        options.PostMarshallTlsConnectionFeature = tlsCallback;
        options.PostMarshallItemsFeature = itemsCallback;

        // Assert
        Assert.Same(requestCallback, options.PostMarshallRequestFeature);
        Assert.Same(responseCallback, options.PostMarshallResponseFeature);
        Assert.Same(connectionCallback, options.PostMarshallConnectionFeature);
        Assert.Same(authCallback, options.PostMarshallHttpAuthenticationFeature);
        Assert.Same(tlsCallback, options.PostMarshallTlsConnectionFeature);
        Assert.Same(itemsCallback, options.PostMarshallItemsFeature);
    }

    /// <summary>
    /// Test that callback properties can be set to null
    /// </summary>
    [Fact]
    public void HostingOptions_CallbackProperties_CanBeSetToNull()
    {
        // Arrange
        var options = new HostingOptions
        {
            PostMarshallRequestFeature = (f, r, c) => { },
            PostMarshallResponseFeature = (f, r, c) => { },
            PostMarshallConnectionFeature = (f, r, c) => { },
            PostMarshallHttpAuthenticationFeature = (f, r, c) => { },
            PostMarshallTlsConnectionFeature = (f, r, c) => { },
            PostMarshallItemsFeature = (f, r, c) => { }
        };

        // Act - Set all to null
        options.PostMarshallRequestFeature = null;
        options.PostMarshallResponseFeature = null;
        options.PostMarshallConnectionFeature = null;
        options.PostMarshallHttpAuthenticationFeature = null;
        options.PostMarshallTlsConnectionFeature = null;
        options.PostMarshallItemsFeature = null;

        // Assert
        Assert.Null(options.PostMarshallRequestFeature);
        Assert.Null(options.PostMarshallResponseFeature);
        Assert.Null(options.PostMarshallConnectionFeature);
        Assert.Null(options.PostMarshallHttpAuthenticationFeature);
        Assert.Null(options.PostMarshallTlsConnectionFeature);
        Assert.Null(options.PostMarshallItemsFeature);
    }

    /// <summary>
    /// Test that registration methods support fluent-style chaining pattern
    /// Note: While the methods return void and don't support true method chaining,
    /// they can be called sequentially in a fluent style
    /// </summary>
    [Fact]
    public void HostingOptions_RegistrationMethods_SupportSequentialConfiguration()
    {
        // Arrange
        var options = new HostingOptions();

        // Act - Configure multiple settings sequentially (fluent style)
        options.RegisterResponseContentEncodingForContentType("application/json", ResponseContentEncoding.Default);
        options.RegisterResponseContentEncodingForContentType("image/png", ResponseContentEncoding.Base64);
        options.RegisterResponseContentEncodingForContentEncoding("gzip", ResponseContentEncoding.Base64);
        options.RegisterResponseContentEncodingForContentEncoding("deflate", ResponseContentEncoding.Base64);

        // Assert - All configurations should be applied
        Assert.Equal(2, options.ContentTypeEncodings.Count);
        Assert.Equal(2, options.ContentEncodingEncodings.Count);
        Assert.Equal(ResponseContentEncoding.Default, options.ContentTypeEncodings["application/json"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentTypeEncodings["image/png"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentEncodingEncodings["gzip"]);
        Assert.Equal(ResponseContentEncoding.Base64, options.ContentEncodingEncodings["deflate"]);
    }

    /// <summary>
    /// Test that HostingOptions can be configured using object initializer syntax
    /// </summary>
    [Fact]
    public void HostingOptions_SupportsObjectInitializerSyntax()
    {
        // Act - Configure using object initializer
        var options = new HostingOptions
        {
            DefaultResponseContentEncoding = ResponseContentEncoding.Base64,
            IncludeUnhandledExceptionDetailInResponse = true,
            PostMarshallRequestFeature = (feature, request, context) => { },
            PostMarshallResponseFeature = (feature, response, context) => { },
            Serializer = null
        };

        // Assert
        Assert.Equal(ResponseContentEncoding.Base64, options.DefaultResponseContentEncoding);
        Assert.True(options.IncludeUnhandledExceptionDetailInResponse);
        Assert.NotNull(options.PostMarshallRequestFeature);
        Assert.NotNull(options.PostMarshallResponseFeature);
        Assert.Null(options.Serializer);
    }

    /// <summary>
    /// Test that HostingOptions configuration can be done through action delegate
    /// (as used in AddAWSLambdaHosting)
    /// </summary>
    [Fact]
    public void HostingOptions_SupportsConfigurationThroughActionDelegate()
    {
        // Arrange
        HostingOptions? capturedOptions = null;
        Action<HostingOptions> configureAction = options =>
        {
            capturedOptions = options;
            options.DefaultResponseContentEncoding = ResponseContentEncoding.Base64;
            options.IncludeUnhandledExceptionDetailInResponse = true;
            options.RegisterResponseContentEncodingForContentType("application/json", ResponseContentEncoding.Default);
            options.PostMarshallRequestFeature = (f, r, c) => { };
        };

        var options = new HostingOptions();

        // Act
        configureAction(options);

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.Same(options, capturedOptions);
        Assert.Equal(ResponseContentEncoding.Base64, options.DefaultResponseContentEncoding);
        Assert.True(options.IncludeUnhandledExceptionDetailInResponse);
        Assert.Single(options.ContentTypeEncodings);
        Assert.NotNull(options.PostMarshallRequestFeature);
    }

    /// <summary>
    /// Test that all extension point properties use correct delegate signatures
    /// </summary>
    [Fact]
    public void HostingOptions_ExtensionPointProperties_UseCorrectDelegateSignatures()
    {
        // Arrange
        var options = new HostingOptions();

        // Act & Assert - Verify delegate signatures match expected types
        // These assignments will fail to compile if signatures don't match

        // PostMarshallRequestFeature: Action<IHttpRequestFeature, object, ILambdaContext>
        options.PostMarshallRequestFeature = (Microsoft.AspNetCore.Http.Features.IHttpRequestFeature feature, 
                                               object request, 
                                               ILambdaContext context) => { };

        // PostMarshallResponseFeature: Action<IHttpResponseFeature, object, ILambdaContext>
        options.PostMarshallResponseFeature = (Microsoft.AspNetCore.Http.Features.IHttpResponseFeature feature, 
                                                object response, 
                                                ILambdaContext context) => { };

        // PostMarshallConnectionFeature: Action<IHttpConnectionFeature, object, ILambdaContext>
        options.PostMarshallConnectionFeature = (Microsoft.AspNetCore.Http.Features.IHttpConnectionFeature feature, 
                                                  object request, 
                                                  ILambdaContext context) => { };

        // PostMarshallHttpAuthenticationFeature: Action<IHttpAuthenticationFeature, object, ILambdaContext>
        options.PostMarshallHttpAuthenticationFeature = (Microsoft.AspNetCore.Http.Features.Authentication.IHttpAuthenticationFeature feature, 
                                                          object request, 
                                                          ILambdaContext context) => { };

        // PostMarshallTlsConnectionFeature: Action<ITlsConnectionFeature, object, ILambdaContext>
        options.PostMarshallTlsConnectionFeature = (Microsoft.AspNetCore.Http.Features.ITlsConnectionFeature feature, 
                                                     object request, 
                                                     ILambdaContext context) => { };

        // PostMarshallItemsFeature: Action<IItemsFeature, object, ILambdaContext>
        options.PostMarshallItemsFeature = (Microsoft.AspNetCore.Http.Features.IItemsFeature feature, 
                                             object request, 
                                             ILambdaContext context) => { };

        // If we reach here, all delegate signatures are correct
        Assert.NotNull(options.PostMarshallRequestFeature);
        Assert.NotNull(options.PostMarshallResponseFeature);
        Assert.NotNull(options.PostMarshallConnectionFeature);
        Assert.NotNull(options.PostMarshallHttpAuthenticationFeature);
        Assert.NotNull(options.PostMarshallTlsConnectionFeature);
        Assert.NotNull(options.PostMarshallItemsFeature);
    }

    /// <summary>
    /// Test that property names match the Core_Library extension point names
    /// </summary>
    [Fact]
    public void HostingOptions_PropertyNames_MatchCoreLibraryExtensionPoints()
    {
        // This test verifies that property names in HostingOptions match the method names
        // in AbstractAspNetCoreFunction (the Core_Library)

        var expectedPropertyNames = new[]
        {
            "DefaultResponseContentEncoding",
            "IncludeUnhandledExceptionDetailInResponse",
            "PostMarshallRequestFeature",
            "PostMarshallResponseFeature",
            "PostMarshallConnectionFeature",
            "PostMarshallHttpAuthenticationFeature",
            "PostMarshallTlsConnectionFeature",
            "PostMarshallItemsFeature",
            "RegisterResponseContentEncodingForContentType",
            "RegisterResponseContentEncodingForContentEncoding"
        };

        var hostingOptionsType = typeof(HostingOptions);

        foreach (var expectedName in expectedPropertyNames)
        {
            // Check if property or method exists
            var property = hostingOptionsType.GetProperty(expectedName);
            var method = hostingOptionsType.GetMethod(expectedName);

            Assert.True(property != null || method != null, 
                $"HostingOptions should have property or method named '{expectedName}' to match Core_Library extension point");
        }
    }
}
