using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;
    using Xunit;
    using Moq;

    namespace Amazon.Lambda.TestTool.UnitTests
    {
        public class HttpRequestUtilityTests
        {
            private readonly HttpRequestUtility _utility;

            public HttpRequestUtilityTests()
            {
                _utility = new HttpRequestUtility();
            }

            [Theory]
            [InlineData("image/jpeg", true)]
            [InlineData("audio/mpeg", true)]
            [InlineData("video/mp4", true)]
            [InlineData("application/octet-stream", true)]
            [InlineData("text/plain", false)]
            [InlineData("application/json", false)]
            [InlineData(null, false)]
            [InlineData("", false)]
            public void IsBinaryContent_ReturnsExpectedResult(string contentType, bool expected)
            {
                var result = _utility.IsBinaryContent(contentType);
                Assert.Equal(expected, result);
            }

            [Fact]
            public void ReadRequestBody_ReturnsCorrectContent()
            {
                var content = "Test body content";
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                var request = new Mock<HttpRequest>();
                request.Setup(r => r.Body).Returns(stream);

                var result = _utility.ReadRequestBody(request.Object);

                Assert.Equal(content, result);
            }

            [Fact]
            public void ExtractHeaders_ReturnsCorrectDictionaries()
            {
                var headers = new HeaderDictionary
            {
                { "Single", new StringValues("Value") },
                { "Multi", new StringValues(new[] { "Value1", "Value2" }) }
            };

                var (singleValueHeaders, multiValueHeaders) = _utility.ExtractHeaders(headers);

                Assert.Equal(2, singleValueHeaders.Count);
                Assert.Equal(2, multiValueHeaders.Count);
                Assert.Equal("Value", singleValueHeaders["Single"]);
                Assert.Equal("Value2", singleValueHeaders["Multi"]);
                Assert.Equal(new List<string> { "Value" }, multiValueHeaders["Single"]);
                Assert.Equal(new List<string> { "Value1", "Value2" }, multiValueHeaders["Multi"]);
            }

            [Fact]
            public void ExtractQueryStringParameters_ReturnsCorrectDictionaries()
            {
                var query = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "Single", new StringValues("Value") },
                { "Multi", new StringValues(new[] { "Value1", "Value2" }) }
            });

                var (singleValueParams, multiValueParams) = _utility.ExtractQueryStringParameters(query);

                Assert.Equal(2, singleValueParams.Count);
                Assert.Equal(2, multiValueParams.Count);
                Assert.Equal("Value", singleValueParams["Single"]);
                Assert.Equal("Value2", singleValueParams["Multi"]);
                Assert.Equal(new List<string> { "Value" }, multiValueParams["Single"]);
                Assert.Equal(new List<string> { "Value1", "Value2" }, multiValueParams["Multi"]);
            }
        }
    }

}
