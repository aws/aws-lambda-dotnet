using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using System.Linq;

using Amazon.Lambda;

namespace Amazon.Lambda.Tests
{
    public class CoreTest
    {
        [Fact]
        public void TestLambdaLogger()
        {
            // verify that LambdaLogger logs to Console

            var message = "This is a message that should appear in console! ?_?";
            var oldWriter = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);

                    Lambda.Core.LambdaLogger.Log(message);

                    var consoleText = writer.ToString();
                    Assert.Contains(message, consoleText);
                }
            }
            finally
            {
                Console.SetOut(oldWriter);
            }
        }
    }
}
