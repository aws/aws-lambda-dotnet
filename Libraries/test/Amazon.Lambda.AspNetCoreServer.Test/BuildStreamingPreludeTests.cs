// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System.Net;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
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
        // 6.3 Non-Set-Cookie headers appear in Headers with all values preserved.
        // HTTP API v2 uses the single-value "headers" collection (not "multiValueHeaders"),
        // so multiple values for the same header are joined with ", ".
        // -----------------------------------------------------------------------
        [Fact]
        public void NonSetCookieHeaders_AppearInHeaders_WithAllValuesPreserved()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["X-Custom"] = new[] { "val1", "val2" },
                ["Cache-Control"] = new[] { "no-cache", "no-store" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.True(prelude.Headers.ContainsKey("Content-Type"));
            Assert.Equal("application/json", prelude.Headers["Content-Type"]);

            Assert.True(prelude.Headers.ContainsKey("X-Custom"));
            Assert.Equal("val1, val2", prelude.Headers["X-Custom"]);

            Assert.True(prelude.Headers.ContainsKey("Cache-Control"));
            Assert.Equal("no-cache, no-store", prelude.Headers["Cache-Control"]);
        }

        [Fact]
        public void NonSetCookieHeaders_Headers_JoinsMultipleValues()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Accept"] = new[] { "text/html", "application/xhtml+xml", "application/xml" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal("text/html, application/xhtml+xml, application/xml",
                prelude.Headers["Accept"]);
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

            // Set-Cookie is NOT in Headers
            Assert.False(prelude.Headers.ContainsKey("Set-Cookie"));
            Assert.False(prelude.Headers.ContainsKey("set-cookie"));

            // Other headers are still present
            Assert.True(prelude.Headers.ContainsKey("Content-Type"));
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
            Assert.False(prelude.Headers.ContainsKey("Set-Cookie"));
            Assert.False(prelude.Headers.ContainsKey("set-cookie"));
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

            // None of them should be in Headers
            Assert.False(prelude.Headers.ContainsKey("Set-Cookie"));
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
            Assert.True(prelude.Headers.ContainsKey("Location"));
            Assert.False(prelude.Headers.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public void EmptyHeaders_ProducesEmptyMultiValueHeadersAndCookies()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(204);

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.Equal(HttpStatusCode.NoContent, prelude.StatusCode);
            Assert.Empty(prelude.Headers);
            Assert.Empty(prelude.Cookies);
        }

        // -----------------------------------------------------------------------
        // Content-Length and Transfer-Encoding are excluded from the prelude
        // -----------------------------------------------------------------------
        [Fact]
        public void ContentLengthHeader_ExcludedFromPrelude()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["Content-Length"] = new[] { "42" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.True(prelude.Headers.ContainsKey("Content-Type"));
            Assert.False(prelude.Headers.ContainsKey("Content-Length"));
        }

        [Fact]
        public void TransferEncodingHeader_ExcludedFromPrelude()
        {
            var builder = CreateBuilder();
            var rf = MakeResponseFeature(200, new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "text/plain" },
                ["Transfer-Encoding"] = new[] { "chunked" }
            });

            var prelude = builder.InvokeBuildStreamingPrelude(rf);

            Assert.True(prelude.Headers.ContainsKey("Content-Type"));
            Assert.False(prelude.Headers.ContainsKey("Transfer-Encoding"));
        }
    }
}
