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
