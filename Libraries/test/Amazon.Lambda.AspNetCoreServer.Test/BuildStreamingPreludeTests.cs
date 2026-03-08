// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
using System;
using System.Net;
using System.Runtime.Versioning;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    [RequiresPreviewFeatures]
    public class BuildStreamingPreludeTests
    {
        // Subclass that skips host startup entirely and
        // just exposes BuildStreamingPrelude directly without needing a running host.
        private class StandalonePreludeBuilder : APIGatewayHttpApiV2ProxyFunction
        {
            // Use the StartupMode.FirstRequest constructor so no host is started eagerly.
            public StandalonePreludeBuilder()
                : base(StartupMode.FirstRequest) { }

            public Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude
                InvokeBuildStreamingPrelude(IHttpResponseFeature responseFeature)
                => BuildStreamingPrelude(responseFeature);
        }

        private static StandalonePreludeBuilder CreateBuilder() => new StandalonePreludeBuilder();

        // Helper: create an InvokeFeatures, set StatusCode and Headers, return as IHttpResponseFeature.
        private static IHttpResponseFeature MakeResponseFeature(int statusCode, System.Collections.Generic.Dictionary<string, string[]> headers = null)
        {
            var features = new InvokeFeatures();
            var rf = (IHttpResponseFeature)features;
            rf.StatusCode = statusCode;
            if (headers != null)
            {
                foreach (var kvp in headers)
                    rf.Headers[kvp.Key] = new Microsoft.Extensions.Primitives.StringValues(kvp.Value);
            }
            return rf;
        }

        // -----------------------------------------------------------------------
        // 6.1 Status code is copied correctly for values 100–599
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(201)]
        [InlineData(204)]
        [InlineData(301)]
        [InlineData(302)]
        [InlineData(400)]
        [InlineData(401)]
        [InlineData(403)]
        [InlineData(404)]
        [InlineData(500)]
        [InlineData(503)]
        [InlineData(599)]
        public void StatusCode_IsCopiedCorrectly(int statusCode)
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(statusCode);

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal((HttpStatusCode)statusCode, prelude.StatusCode);
        }

        // -----------------------------------------------------------------------
        // 6.2 Status code defaults to 200 when IHttpResponseFeature.StatusCode is 0
        // -----------------------------------------------------------------------
        [Fact]
        public void StatusCode_DefaultsTo200_WhenFeatureStatusCodeIsZero()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(0);

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal(HttpStatusCode.OK, prelude.StatusCode);
        }

        // -----------------------------------------------------------------------
        // 6.3 Non-Set-Cookie headers appear in MultiValueHeaders with all values preserved
        // -----------------------------------------------------------------------
        [Fact]
        public void NonSetCookieHeaders_AppearInMultiValueHeaders_WithAllValuesPreserved()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["X-Custom"] = new[] { "val1", "val2" },
                ["Cache-Control"] = new[] { "no-cache", "no-store" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.True(prelude.MultiValueHeaders.ContainsKey("Content-Type"));
            Assert.Equal(new[] { "application/json" }, prelude.MultiValueHeaders["Content-Type"]);

            Assert.True(prelude.MultiValueHeaders.ContainsKey("X-Custom"));
            Assert.Equal(new[] { "val1", "val2" }, prelude.MultiValueHeaders["X-Custom"]);

            Assert.True(prelude.MultiValueHeaders.ContainsKey("Cache-Control"));
            Assert.Equal(new[] { "no-cache", "no-store" }, prelude.MultiValueHeaders["Cache-Control"]);
        }

        [Fact]
        public void NonSetCookieHeaders_MultiValueHeaders_PreservesMultipleValues()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Accept"] = new[] { "text/html", "application/xhtml+xml", "application/xml" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal(new[] { "text/html", "application/xhtml+xml", "application/xml" },
                prelude.MultiValueHeaders["Accept"]);
        }

        // -----------------------------------------------------------------------
        // 6.4 Set-Cookie header values are moved to Cookies and absent from MultiValueHeaders
        // -----------------------------------------------------------------------
        [Fact]
        public void SetCookieHeader_MovedToCookies_AbsentFromMultiValueHeaders()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Set-Cookie"] = new[] { "session=abc123; Path=/; HttpOnly" },
                ["Content-Type"] = new[] { "text/html" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            // Cookie value is in Cookies
            Assert.Contains("session=abc123; Path=/; HttpOnly", prelude.Cookies);

            // Set-Cookie is NOT in MultiValueHeaders
            Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"));
            Assert.False(prelude.MultiValueHeaders.ContainsKey("set-cookie"));

            // Other headers are still present
            Assert.True(prelude.MultiValueHeaders.ContainsKey("Content-Type"));
        }

        [Fact]
        public void SetCookieHeader_IsCaseInsensitive()
        {
            // The implementation uses StringComparison.OrdinalIgnoreCase, so
            // "set-cookie" (lowercase) should also be routed to Cookies.
            var builder = CreateBuilder();
            var features = new InvokeFeatures();
            var rf = (IHttpResponseFeature)features;
            rf.StatusCode = 200;
            // HeaderDictionary is case-insensitive, so "set-cookie" and "Set-Cookie" are the same key.
            rf.Headers["set-cookie"] = new Microsoft.Extensions.Primitives.StringValues("id=xyz; Path=/");

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Contains("id=xyz; Path=/", prelude.Cookies);
            Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"));
            Assert.False(prelude.MultiValueHeaders.ContainsKey("set-cookie"));
        }

        // -----------------------------------------------------------------------
        // 6.5 Multiple Set-Cookie values all appear in Cookies
        // -----------------------------------------------------------------------
        [Fact]
        public void MultipleSetCookieValues_AllAppearInCookies()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Set-Cookie"] = new[]
                {
                    "session=abc; Path=/; HttpOnly",
                    "theme=dark; Path=/",
                    "lang=en; Path=/; SameSite=Strict"
                }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal(3, prelude.Cookies.Count);
            Assert.Contains("session=abc; Path=/; HttpOnly", prelude.Cookies);
            Assert.Contains("theme=dark; Path=/", prelude.Cookies);
            Assert.Contains("lang=en; Path=/; SameSite=Strict", prelude.Cookies);

            // None of them should be in MultiValueHeaders
            Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public void MultipleSetCookieValues_WithOtherHeaders_CookiesAndHeadersAreSeparated()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(201, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Set-Cookie"] = new[] { "a=1", "b=2" },
                ["Location"] = new[] { "/new-resource" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal((HttpStatusCode)201, prelude.StatusCode);
            Assert.Equal(2, prelude.Cookies.Count);
            Assert.Contains("a=1", prelude.Cookies);
            Assert.Contains("b=2", prelude.Cookies);
            Assert.True(prelude.MultiValueHeaders.ContainsKey("Location"));
            Assert.False(prelude.MultiValueHeaders.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public void EmptyHeaders_ProducesEmptyMultiValueHeadersAndCookies()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(204);

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal(HttpStatusCode.NoContent, prelude.StatusCode);
            Assert.Empty(prelude.MultiValueHeaders);
            Assert.Empty(prelude.Cookies);
        }
    }
}
#endif
