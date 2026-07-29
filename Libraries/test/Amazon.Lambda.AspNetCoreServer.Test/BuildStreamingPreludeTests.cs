// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class BuildStreamingPreludeTests
    {
        /// <summary>
        /// Selects which proxy function implementation of BuildStreamingPrelude to exercise.
        /// The two implementations differ in how non-cookie headers are represented on the prelude:
        /// the HTTP API v2 function uses the single-value <c>Headers</c> collection while the
        /// REST API function uses the multi-value <c>MultiValueHeaders</c> collection.
        /// </summary>
        public enum ProxyFunctionType
        {
            /// <summary>APIGatewayProxyFunction (REST API) - populates MultiValueHeaders.</summary>
            RestApi,

            /// <summary>APIGatewayHttpApiV2ProxyFunction (HTTP API v2) - populates Headers.</summary>
            HttpApiV2
        }

        // Subclass that skips host startup entirely and
        // just exposes BuildStreamingPrelude directly without needing a running host.
        private class HttpApiV2PreludeBuilder : APIGatewayHttpApiV2ProxyFunction
        {
            // Use the StartupMode.FirstRequest constructor so no host is started eagerly.
            public HttpApiV2PreludeBuilder()
                : base(StartupMode.FirstRequest) { }

            public Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude
                InvokeBuildStreamingPrelude(IHttpResponseFeature responseFeature)
                => BuildStreamingPrelude(responseFeature);
        }

        // Subclass that skips host startup entirely and
        // just exposes BuildStreamingPrelude directly without needing a running host.
        private class RestApiPreludeBuilder : APIGatewayProxyFunction
        {
            // Use the StartupMode.FirstRequest constructor so no host is started eagerly.
            public RestApiPreludeBuilder()
                : base(StartupMode.FirstRequest) { }

            public Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude
                InvokeBuildStreamingPrelude(IHttpResponseFeature responseFeature)
                => BuildStreamingPrelude(responseFeature);
        }

        // Helper: invoke BuildStreamingPrelude on the requested proxy function implementation.
        private static Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude BuildPrelude(
            ProxyFunctionType functionType, IHttpResponseFeature responseFeature)
        {
            switch (functionType)
            {
                case ProxyFunctionType.RestApi:
                    return new RestApiPreludeBuilder().InvokeBuildStreamingPrelude(responseFeature);
                case ProxyFunctionType.HttpApiV2:
                    return new HttpApiV2PreludeBuilder().InvokeBuildStreamingPrelude(responseFeature);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(functionType));
            }
        }

        // Helper: create an InvokeFeatures, set StatusCode and Headers, return as IHttpResponseFeature.
        private static IHttpResponseFeature MakeResponseFeature(int statusCode, Dictionary<string, string[]> headers = null)
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
        // Assertion helpers that check the correct header collection depending on
        // the proxy function type: MultiValueHeaders for REST API, Headers for HTTP API v2.
        // -----------------------------------------------------------------------

        // Assert a non-cookie header is present with the given values.
        private static void AssertHeaderPresent(
            Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude,
            ProxyFunctionType functionType, string key, params string[] expectedValues)
        {
            if (functionType == ProxyFunctionType.RestApi)
            {
                Assert.True(prelude.MultiValueHeaders.ContainsKey(key));
                Assert.Equal(expectedValues, prelude.MultiValueHeaders[key]);

                // The single-value Headers collection is not used by the REST API implementation.
                Assert.False(prelude.Headers.ContainsKey(key));
            }
            else
            {
                Assert.True(prelude.Headers.ContainsKey(key));
                // HTTP API v2 uses single-value headers, so multiple values are joined with ", ".
                Assert.Equal(string.Join(", ", expectedValues), prelude.Headers[key]);

                // The multi-value MultiValueHeaders collection is not used by the HTTP API v2 implementation.
                Assert.False(prelude.MultiValueHeaders.ContainsKey(key));
            }
        }

        // Assert a header is absent from whichever collection the implementation uses.
        private static void AssertHeaderAbsent(
            Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude,
            ProxyFunctionType functionType, string key)
        {
            if (functionType == ProxyFunctionType.RestApi)
            {
                Assert.False(prelude.MultiValueHeaders.ContainsKey(key));
            }
            else
            {
                Assert.False(prelude.Headers.ContainsKey(key));
            }
        }

        // Assert the header collection used by the implementation is empty.
        private static void AssertHeadersEmpty(
            Amazon.Lambda.Core.ResponseStreaming.HttpResponseStreamPrelude prelude,
            ProxyFunctionType functionType)
        {
            if (functionType == ProxyFunctionType.RestApi)
            {
                Assert.Empty(prelude.MultiValueHeaders);
            }
            else
            {
                Assert.Empty(prelude.Headers);
            }
        }

        // -----------------------------------------------------------------------
        // 6.1 Status code is copied correctly for values 100–599
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(ProxyFunctionType.RestApi, 100)]
        [InlineData(ProxyFunctionType.RestApi, 200)]
        [InlineData(ProxyFunctionType.RestApi, 201)]
        [InlineData(ProxyFunctionType.RestApi, 204)]
        [InlineData(ProxyFunctionType.RestApi, 301)]
        [InlineData(ProxyFunctionType.RestApi, 302)]
        [InlineData(ProxyFunctionType.RestApi, 400)]
        [InlineData(ProxyFunctionType.RestApi, 401)]
        [InlineData(ProxyFunctionType.RestApi, 403)]
        [InlineData(ProxyFunctionType.RestApi, 404)]
        [InlineData(ProxyFunctionType.RestApi, 500)]
        [InlineData(ProxyFunctionType.RestApi, 503)]
        [InlineData(ProxyFunctionType.RestApi, 599)]
        [InlineData(ProxyFunctionType.HttpApiV2, 100)]
        [InlineData(ProxyFunctionType.HttpApiV2, 200)]
        [InlineData(ProxyFunctionType.HttpApiV2, 201)]
        [InlineData(ProxyFunctionType.HttpApiV2, 204)]
        [InlineData(ProxyFunctionType.HttpApiV2, 301)]
        [InlineData(ProxyFunctionType.HttpApiV2, 302)]
        [InlineData(ProxyFunctionType.HttpApiV2, 400)]
        [InlineData(ProxyFunctionType.HttpApiV2, 401)]
        [InlineData(ProxyFunctionType.HttpApiV2, 403)]
        [InlineData(ProxyFunctionType.HttpApiV2, 404)]
        [InlineData(ProxyFunctionType.HttpApiV2, 500)]
        [InlineData(ProxyFunctionType.HttpApiV2, 503)]
        [InlineData(ProxyFunctionType.HttpApiV2, 599)]
        public void StatusCode_IsCopiedCorrectly(ProxyFunctionType functionType, int statusCode)
        {
            var rf = MakeResponseFeature(statusCode);

            var prelude = BuildPrelude(functionType, rf);

            Assert.Equal((HttpStatusCode)statusCode, prelude.StatusCode);
        }

        // -----------------------------------------------------------------------
        // 6.2 Status code defaults to 200 when IHttpResponseFeature.StatusCode is 0
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void StatusCode_DefaultsTo200_WhenFeatureStatusCodeIsZero(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(0);

            var prelude = BuildPrelude(functionType, rf);

            Assert.Equal(HttpStatusCode.OK, prelude.StatusCode);
        }

        // -----------------------------------------------------------------------
        // 6.3 Non-Set-Cookie headers appear in the header collection with all values preserved.
        // HTTP API v2 uses the single-value "headers" collection (joining multiple values with ", "),
        // while the REST API uses the multi-value "multiValueHeaders" collection.
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void NonSetCookieHeaders_AppearInHeaders_WithAllValuesPreserved(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(200, new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["X-Custom"] = new[] { "val1", "val2" },
                ["Cache-Control"] = new[] { "no-cache", "no-store" }
            });

            var prelude = BuildPrelude(functionType, rf);

            AssertHeaderPresent(prelude, functionType, "Content-Type", "application/json");
            AssertHeaderPresent(prelude, functionType, "X-Custom", "val1", "val2");
            AssertHeaderPresent(prelude, functionType, "Cache-Control", "no-cache", "no-store");
        }

        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void NonSetCookieHeaders_JoinsMultipleValues(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(200, new Dictionary<string, string[]>
            {
                ["Accept"] = new[] { "text/html", "application/xhtml+xml", "application/xml" }
            });

            var prelude = BuildPrelude(functionType, rf);

            AssertHeaderPresent(prelude, functionType, "Accept",
                "text/html", "application/xhtml+xml", "application/xml");
        }

        // -----------------------------------------------------------------------
        // 6.4 Set-Cookie header values are moved to Cookies and absent from the header collection
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void SetCookieHeader_MovedToCookies_AbsentFromHeaders(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(200, new Dictionary<string, string[]>
            {
                ["Set-Cookie"] = new[] { "session=abc123; Path=/; HttpOnly" },
                ["Content-Type"] = new[] { "text/html" }
            });

            var prelude = BuildPrelude(functionType, rf);

            // Cookie value is in Cookies
            Assert.Contains("session=abc123; Path=/; HttpOnly", prelude.Cookies);

            // Set-Cookie is NOT in the header collection
            AssertHeaderAbsent(prelude, functionType, "Set-Cookie");
            AssertHeaderAbsent(prelude, functionType, "set-cookie");

            // Other headers are still present
            AssertHeaderPresent(prelude, functionType, "Content-Type", "text/html");
        }

        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void SetCookieHeader_IsCaseInsensitive(ProxyFunctionType functionType)
        {
            // The implementation uses StringComparison.OrdinalIgnoreCase, so
            // "set-cookie" (lowercase) should also be routed to Cookies.
            var features = new InvokeFeatures();
            var rf = (IHttpResponseFeature)features;
            rf.StatusCode = 200;
            // HeaderDictionary is case-insensitive, so "set-cookie" and "Set-Cookie" are the same key.
            rf.Headers["set-cookie"] = new Microsoft.Extensions.Primitives.StringValues("id=xyz; Path=/");

            var prelude = BuildPrelude(functionType, rf);

            Assert.Contains("id=xyz; Path=/", prelude.Cookies);
            AssertHeaderAbsent(prelude, functionType, "Set-Cookie");
            AssertHeaderAbsent(prelude, functionType, "set-cookie");
        }

        // -----------------------------------------------------------------------
        // 6.5 Multiple Set-Cookie values all appear in Cookies
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void MultipleSetCookieValues_AllAppearInCookies(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(200, new Dictionary<string, string[]>
            {
                ["Set-Cookie"] = new[]
                {
                    "session=abc; Path=/; HttpOnly",
                    "theme=dark; Path=/",
                    "lang=en; Path=/; SameSite=Strict"
                }
            });

            var prelude = BuildPrelude(functionType, rf);

            Assert.Equal(3, prelude.Cookies.Count);
            Assert.Contains("session=abc; Path=/; HttpOnly", prelude.Cookies);
            Assert.Contains("theme=dark; Path=/", prelude.Cookies);
            Assert.Contains("lang=en; Path=/; SameSite=Strict", prelude.Cookies);

            // None of them should be in the header collection
            AssertHeaderAbsent(prelude, functionType, "Set-Cookie");
        }

        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void MultipleSetCookieValues_WithOtherHeaders_CookiesAndHeadersAreSeparated(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(201, new Dictionary<string, string[]>
            {
                ["Set-Cookie"] = new[] { "a=1", "b=2" },
                ["Location"] = new[] { "/new-resource" }
            });

            var prelude = BuildPrelude(functionType, rf);

            Assert.Equal((HttpStatusCode)201, prelude.StatusCode);
            Assert.Equal(2, prelude.Cookies.Count);
            Assert.Contains("a=1", prelude.Cookies);
            Assert.Contains("b=2", prelude.Cookies);
            AssertHeaderPresent(prelude, functionType, "Location", "/new-resource");
            AssertHeaderAbsent(prelude, functionType, "Set-Cookie");
        }

        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void EmptyHeaders_ProducesEmptyHeadersAndCookies(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(204);

            var prelude = BuildPrelude(functionType, rf);

            Assert.Equal(HttpStatusCode.NoContent, prelude.StatusCode);
            AssertHeadersEmpty(prelude, functionType);
            Assert.Empty(prelude.Cookies);
        }

        // -----------------------------------------------------------------------
        // Content-Length and Transfer-Encoding are excluded from the prelude
        // -----------------------------------------------------------------------
        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void ContentLengthHeader_ExcludedFromPrelude(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(200, new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "application/json" },
                ["Content-Length"] = new[] { "42" }
            });

            var prelude = BuildPrelude(functionType, rf);

            AssertHeaderPresent(prelude, functionType, "Content-Type", "application/json");
            AssertHeaderAbsent(prelude, functionType, "Content-Length");
        }

        [Theory]
        [InlineData(ProxyFunctionType.RestApi)]
        [InlineData(ProxyFunctionType.HttpApiV2)]
        public void TransferEncodingHeader_ExcludedFromPrelude(ProxyFunctionType functionType)
        {
            var rf = MakeResponseFeature(200, new Dictionary<string, string[]>
            {
                ["Content-Type"] = new[] { "text/plain" },
                ["Transfer-Encoding"] = new[] { "chunked" }
            });

            var prelude = BuildPrelude(functionType, rf);

            AssertHeaderPresent(prelude, functionType, "Content-Type", "text/plain");
            AssertHeaderAbsent(prelude, functionType, "Transfer-Encoding");
        }
    }
}
