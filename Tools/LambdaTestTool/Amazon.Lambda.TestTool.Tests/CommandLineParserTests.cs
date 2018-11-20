using System;
using Xunit;

namespace Amazon.Lambda.TestTool.Tests
{
    public class CommandLineParserTests
    {
        [Fact]
        public void NoArguments()
        {
            var options = CommandLineOptions.Parse(new string[0]);
            Assert.NotNull(options);
        }
        
        [Fact]
        public void ParseIntValueForSwitch()
        {
            var options = CommandLineOptions.Parse(new string[]{"--port", "8080"});
            Assert.Equal(8080, options.Port);            
        }

        [Fact]
        public void MissingValue()
        {
            Assert.Throws<CommandLineParseException>(() =>  CommandLineOptions.Parse(new string[]{"--port"}));
        }

        [Fact]
        public void ValueNotAnInt()
        {
            Assert.Throws<CommandLineParseException>(() =>  CommandLineOptions.Parse(new string[]{"--port", "aaa"}));
        }
    }
}