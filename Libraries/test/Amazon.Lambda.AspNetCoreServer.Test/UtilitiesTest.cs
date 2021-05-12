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
    }
}
