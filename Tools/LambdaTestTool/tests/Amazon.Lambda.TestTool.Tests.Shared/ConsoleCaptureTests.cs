using System;
using Xunit;

using Amazon.Lambda.TestTool;
using Amazon.Lambda.TestTool.Runtime;
using Amazon.Lambda.TestTool.Runtime.LambdaMocks;

namespace Amazon.Lambda.TestTool.Tests
{
    public class ConsoleCaptureTests
    {
        [Fact]
        public void CaptureStandardOut()
        {
            var logger = new LocalLambdaLogger();
            using (var captiure = new ConsoleOutWrapper(logger))
            {
                Console.WriteLine("CAPTURED");
            }
            Console.WriteLine("NOT_CAPTURED");
            
            Assert.Contains("CAPTURED", logger.Buffer);
            Assert.DoesNotContain("NOT_CAPTURED", logger.Buffer);
        }

        [Fact]
        public void CaptureStandardError()
        {
            var logger = new LocalLambdaLogger();
            using (var captiure = new ConsoleOutWrapper(logger))
            {
                Console.Error.WriteLine("CAPTURED");
            }
            Console.Error.WriteLine("NOT_CAPTURED");
            
            Assert.Contains("CAPTURED", logger.Buffer);
            Assert.DoesNotContain("NOT_CAPTURED", logger.Buffer);
        }

    }
}