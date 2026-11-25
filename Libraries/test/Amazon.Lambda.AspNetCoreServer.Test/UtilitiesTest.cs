using System;
using System.Collections.Generic;
using System.Text;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class UtilitiesTest
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("name=foo bar", "?name=foo bar")]
        [InlineData("name=foo+bar", "?name=foo+bar")]
        [InlineData("url=http://www.google.com&testDateTimeOffset=2019-03-12T16:06:06.549817+00:00", "?url=http://www.google.com&testDateTimeOffset=2019-03-12T16:06:06.549817+00:00")]
        public void TestHttpApiV2QueryStringEncoding(string starting, string expected)
        {
            var encoded = Utilities.CreateQueryStringParametersFromHttpApiV2(starting);
            Assert.Equal(expected, encoded);
        }

        [Theory]
        [InlineData(Utilities.MaxResponseBodySize, false, false)]
        [InlineData(Utilities.MaxResponseBodySize + 1, false, true)]
        [InlineData(Utilities.MaxResponseBodySize, true, false)]
        [InlineData(Utilities.MaxResponseBodySize + 1, true, true)]
        public void TestIsResponseBodyTooLarge(int size, bool isBase64Encoded, bool expected)
        {
            Assert.Equal(expected, Utilities.IsResponseBodyTooLarge(new string('a', size), isBase64Encoded));
        }

        [Fact]
        public void TestIsResponseBodyTooLargeKeepsHeadroomBelowPayloadLimit()
        {
            Assert.True(Utilities.MaxResponseBodySize < Utilities.MaxResponsePayloadSize);
            Assert.True(Utilities.IsResponseBodyTooLarge(new string('a', Utilities.MaxResponsePayloadSize), false));
        }

        [Fact]
        public void TestIsResponseBodyTooLargeCountsUtf8Bytes()
        {
            // Each character is 2 bytes in UTF-8, so this body is over the limit even though it has fewer characters.
            var body = new string('é', Utilities.MaxResponseBodySize / 2 + 1);
            Assert.True(Utilities.IsResponseBodyTooLarge(body, false));
        }

        [Theory]
        [InlineData('"')]
        [InlineData('\\')]
        [InlineData('\n')]
        public void TestIsResponseBodyTooLargeCountsJsonEscaping(char escaped)
        {
            // Each character is written as two bytes in the serialized response ("\"", "\\", "\n"),
            // so this body is over the limit although its raw size is about half of it.
            var body = new string(escaped, Utilities.MaxResponseBodySize / 2 + 1);
            Assert.True(Utilities.IsResponseBodyTooLarge(body, false));
            Assert.False(Utilities.IsResponseBodyTooLarge(new string(escaped, Utilities.MaxResponseBodySize / 2), false));
        }

        [Fact]
        public void TestIsResponseBodyTooLargeCountsUnicodeEscapes()
        {
            // Control characters without a short escape are written as "\u0001" (6 bytes).
            var body = new string('\u0001', Utilities.MaxResponseBodySize / 6 + 1);
            Assert.True(Utilities.IsResponseBodyTooLarge(body, false));
            Assert.False(Utilities.IsResponseBodyTooLarge(new string('\u0001', Utilities.MaxResponseBodySize / 6), false));
        }

        [Fact]
        public void TestLargestAllowedBodyFitsInSerializedResponse()
        {
            // The largest body that passes the check, mixed with characters that need escaping,
            // still fits in the payload limit once the whole response is serialized.
            var pattern = "a\"\\\né<\u0001";
            var builder = new StringBuilder();
            var count = Utilities.MaxResponseBodySize / Utilities.GetJsonEncodedSize(pattern);
            for (var i = 0; i < count; i++)
            {
                builder.Append(pattern);
            }
            var body = builder.ToString();
            Assert.True(Utilities.GetJsonEncodedSize(body) > Utilities.MaxResponseBodySize - Utilities.GetJsonEncodedSize(pattern));
            Assert.False(Utilities.IsResponseBodyTooLarge(body, false));

            var response = new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = body,
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain; charset=utf-8" } },
                MultiValueHeaders = new Dictionary<string, IList<string>> { { "Content-Type", new List<string> { "text/plain; charset=utf-8" } } }
            };
            using var stream = new System.IO.MemoryStream();
            new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer().Serialize(response, stream);
            Assert.True(stream.Length <= Utilities.MaxResponsePayloadSize, $"Serialized response is {stream.Length} bytes");
        }

        [Fact]
        public void TestIsResponseBodyTooLargeNullBody()
        {
            Assert.False(Utilities.IsResponseBodyTooLarge(null, false));
        }

        // This test is ensure middleware will the status code at 200.
        [Fact]
        public void EnsureStatusCodeStartsAtIs200()
        {
            var feature = new InvokeFeatures() as IHttpResponseFeature;
            Assert.Equal(200, feature.StatusCode);
        }

        // Regression test for https://github.com/aws/aws-lambda-dotnet/issues/1702.
        // ASP.NET Core's FeatureReferences cache uses Revision to detect when a
        // feature has been swapped (e.g. OutputCache/ResponseCompression replacing
        // IHttpResponseBodyFeature to wrap the response body). If Set<TFeature>
        // does not bump the revision, cached references stay stale and writes
        // bypass the wrapper.
        [Fact]
        public void SetFeatureBumpsRevision()
        {
            IFeatureCollection features = new InvokeFeatures();
            var initialRevision = features.Revision;

            features.Set<IHttpResponseBodyFeature>(new TestResponseBodyFeature());

            Assert.NotEqual(initialRevision, features.Revision);
        }

        [Fact]
        public void SetFeatureStoresAndRetrievesInstance()
        {
            IFeatureCollection features = new InvokeFeatures();
            var replacement = new TestResponseBodyFeature();

            features.Set<IHttpResponseBodyFeature>(replacement);

            Assert.Same(replacement, features.Get<IHttpResponseBodyFeature>());
        }

        [Fact]
        public void SetFeatureNullRemovesEntryAndBumpsRevision()
        {
            IFeatureCollection features = new InvokeFeatures();
            // InvokeFeatures seeds itself as the IHttpResponseBodyFeature in its constructor.
            Assert.NotNull(features.Get<IHttpResponseBodyFeature>());
            var revisionBeforeRemove = features.Revision;

            features.Set<IHttpResponseBodyFeature>(null);

            Assert.Null(features.Get<IHttpResponseBodyFeature>());
            Assert.NotEqual(revisionBeforeRemove, features.Revision);
        }

        private sealed class TestResponseBodyFeature : IHttpResponseBodyFeature
        {
            public System.IO.Stream Stream => System.IO.Stream.Null;
            public System.IO.Pipelines.PipeWriter Writer => System.IO.Pipelines.PipeWriter.Create(System.IO.Stream.Null);
            public System.Threading.Tasks.Task CompleteAsync() => System.Threading.Tasks.Task.CompletedTask;
            public void DisableBuffering() { }
            public System.Threading.Tasks.Task SendFileAsync(string path, long offset, long? count, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
