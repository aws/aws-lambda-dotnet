using System;
using Xunit;

namespace Amazon.Lambda.TestTool.Tests
{
    public class CommandLineParserTests
    {
        [Fact]
        public void AllValuesGetSet()
        {
            var options = CommandLineOptions.Parse(new string[] {"--help", "--port", "1111", "--no-launch-window", 
                                                                "--path", "./foo", "--profile", "test", "--region", "special-region",
                                                                "--no-ui", "--config-file", "test-config.json", "--payload", "myfile.json", "--pause-exit", "false" });


            Assert.True(options.ShowHelp);
            Assert.Equal(1111, options.Port);
            Assert.True(options.NoLaunchWindow);
            Assert.Equal("./foo", options.Path);
            Assert.Equal("test", options.AWSProfile);
            Assert.Equal("special-region", options.AWSRegion);
            Assert.True(options.NoUI);
            Assert.Equal("test-config.json", options.ConfigFile);
            Assert.Equal("myfile.json", options.Payload);
            Assert.False(options.PauseExit);
        }


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


        [Fact]
        public void BoolSwitchWithoutValueLast()
        {
            var options = CommandLineOptions.Parse(new string[] { "--no-ui" });
            Assert.True(options.NoUI);
        }

        [Fact]
        public void BoolSwitchWithoutValue()
        {
            var options = CommandLineOptions.Parse(new string[] { "--no-ui", "--profile", "test" });
            Assert.True(options.NoUI);
            Assert.Equal("test", options.AWSProfile);
        }

        [Fact]
        public void BoolSwitchValueLast()
        {
            var options = CommandLineOptions.Parse(new string[0]);
            Assert.True(options.PauseExit);

            options = CommandLineOptions.Parse(new string[] { "--pause-exit", "false"});
            Assert.False(options.PauseExit);
        }

        [Fact]
        public void BoolSwitchValue()
        {
            var options = CommandLineOptions.Parse(new string[0]);
            Assert.True(options.PauseExit);

            options = CommandLineOptions.Parse(new string[] { "--pause-exit", "false", "--profile", "test" });
            Assert.False(options.PauseExit);
            Assert.Equal("test", options.AWSProfile);
        }
    }
}