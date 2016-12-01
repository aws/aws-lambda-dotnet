using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

using Amazon.Lambda.Tools.Commands;
using Amazon.Lambda.Tools.Options;


namespace Amazon.Lambda.Tools.Test
{
    public class CommandLineParserTest
    {

        [Fact]
        public void SingleStringArgument()
        {
            Action<CommandOptions> validation = x =>
            {
                Assert.Equal(1, x.Count);

                var option = x.FindCommandOption("-c");
                Assert.Equal("Release", option.Item2.StringValue);
            };

            var values = CommandLineParser.ParseArguments(DeployFunctionCommand.DeployCommandOptions, new string[] {"-c", "Release" });
            validation(values);

            values = CommandLineParser.ParseArguments(DeployFunctionCommand.DeployCommandOptions, new string[] { "--configuration", "Release" });
            validation(values);
        }

        [Fact]
        public void SingleIntArgument()
        {
            Action<CommandOptions> validation = x =>
            {
                Assert.Equal(1, x.Count);

                var option = x.FindCommandOption("-ft");
                Assert.Equal(100, option.Item2.IntValue);
            };

            var values = CommandLineParser.ParseArguments(DeployFunctionCommand.DeployCommandOptions, new string[] { "-ft", "100" });
            validation(values);

            values = CommandLineParser.ParseArguments(DeployFunctionCommand.DeployCommandOptions, new string[] { "--function-timeout", "100" });
            validation(values);
        }

        [Fact]
        public void SingleBoolArgument()
        {
            Action<CommandOptions> validation = x =>
            {
                Assert.Equal(1, x.Count);

                var option = x.FindCommandOption("-fp");
                Assert.Equal(true, option.Item2.BoolValue);
            };

            var values = CommandLineParser.ParseArguments(DeployFunctionCommand.DeployCommandOptions, new string[] { "-fp", "true" });
            validation(values);

            values = CommandLineParser.ParseArguments(DeployFunctionCommand.DeployCommandOptions, new string[] { "--function-publish", "true" });
            validation(values);
        }

        [Fact]
        public void BuildLambdaDeployCommandWithAllArguments()
        {
            var arguments = new List<string>();
            arguments.AddRange(new string[] { "-c", "CrazyRelease" });
            arguments.AddRange(new string[] { "-f", "netfake" });
            arguments.AddRange(new string[] { "--function-name", "MyName" });
            arguments.AddRange(new string[] { "--function-description", "MyDescription" });
            arguments.AddRange(new string[] { "--function-publish", "true" });
            arguments.AddRange(new string[] { "--function-handler", "TheHandler" });
            arguments.AddRange(new string[] { "--function-memory-size", "33" });
            arguments.AddRange(new string[] { "--function-role", "MyRole" });
            arguments.AddRange(new string[] { "--function-timeout", "55" });
            arguments.AddRange(new string[] { "--function-runtime", "netcore9.9" });

            var command = new DeployFunctionCommand(new ConsoleToolLogger(), ".", arguments.ToArray());

            Assert.Equal("CrazyRelease", command.Configuration);
            Assert.Equal("netfake", command.TargetFramework);
            Assert.Equal("MyName", command.FunctionName);
            Assert.Equal("MyDescription", command.Description);
            Assert.Equal(true, command.Publish);
            Assert.Equal(33, command.MemorySize);
            Assert.Equal("MyRole", command.Role);
            Assert.Equal(55, command.Timeout);
            Assert.Equal("netcore9.9", command.Runtime);
        }
    }
}
