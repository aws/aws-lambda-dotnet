using System;
using System.Collections.Generic;
using System.Text;

using Amazon.Lambda.AspNetCoreServer.Internal;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{
    public class UtilitiesTest
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("name=foo bar", "?name=foo+bar")]
        [InlineData("name=foo+bar", "?name=foo%2Bbar")]
        [InlineData("param1", "?param1")]
        [InlineData("param=value1&param=value2", "?param=value1&param=value2")]
        [InlineData("url=http://www.google.com&testDateTimeOffset=2019-03-12T16:06:06.549817+00:00", "?url=http%3A%2F%2Fwww.google.com&testDateTimeOffset=2019-03-12T16%3A06%3A06.549817%2B00%3A00")]
        public void TestHttpApiV2QueryStringEncoding(string starting, string expected)
        {
            var encoded = Utilities.CreateQueryStringParametersFromHttpApiV2(starting);
            Assert.Equal(expected, encoded);
        }
    }
}
