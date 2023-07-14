using Xunit;
using Amazon.Lambda.TestUtilities;
using Moq;


namespace BlueprintBaseName._1.Tests
{
    public class FunctionTest
    {
        ICalculatorService _mockCalculatorService;

        public FunctionTest()
        {
            // This isn't an accurate calculator, but rather shows how the business logic
            // for our Lambda functions can be moved to a class that can be replaced by a
            // mocked implementation for testing.
            var mock = new Mock<ICalculatorService>();
            mock.Setup(m => m.Add(It.IsAny<int>(), It.IsAny<int>())).Returns(12);

            _mockCalculatorService = mock.Object;
        }

        [Fact]
        public void TestAdd()
        {
            TestLambdaContext context = new TestLambdaContext();
            
            var functions = new Functions(_mockCalculatorService);
            Assert.Equal(12, functions.Add(3, 9, context));
        }
    }
}
