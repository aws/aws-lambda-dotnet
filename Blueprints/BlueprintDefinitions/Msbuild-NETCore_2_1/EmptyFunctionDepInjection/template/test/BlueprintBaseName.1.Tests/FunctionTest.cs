using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

using BlueprintBaseName._1;

namespace BlueprintBaseName._1.Tests
{
    public class FunctionTest
    {
        [Fact]
        public void TestConfigFunction()
        {
            var expected = "val1";
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(p => p[It.IsAny<string>()]).Returns(expected);

            var function = new Function(mockConfig.Object);
            var result = function.FunctionHandler("env1", new TestLambdaContext());
            Assert.Equal(expected, result);
        }
    }
}
