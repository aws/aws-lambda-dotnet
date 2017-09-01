using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Xunit;

using Amazon.Lambda;
using Amazon.Lambda.Model;

using Amazon.Lambda.Tools;
using Amazon.Lambda.Tools.Commands;
using Amazon.Lambda.Tools.Options;

using ThirdParty.Json.LitJson;

namespace Amazon.Lambda.Tools.Test
{
    public class LambdaToolsDefaultsReaderTest
    {
        private string GetTestProjectPath()
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;
            var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../TestFunction/");
            return fullPath;
        }

        [Fact]
        public void LoadDefaultsDirectly()
        {
            var defaults = LambdaToolsDefaultsReader.LoadDefaults(GetTestProjectPath(), LambdaToolsDefaultsReader.DEFAULT_FILE_NAME);

            Assert.Equal(defaults.Region, "us-east-2");
            Assert.Equal(defaults["region"], "us-east-2");

            Assert.Equal(defaults["disable-version-check"], true);
            Assert.Equal(defaults["function-memory-size"], 128);

        }

        [Fact]
        public void CommandInferRegionFromDefaults()
        {
            var command = new DeployFunctionCommand(new ConsoleToolLogger(), GetTestProjectPath(), new string[0]);

            Assert.Equal("us-east-2", command.GetStringValueOrDefault(command.Region, DefinedCommandOptions.ARGUMENT_AWS_REGION, true));
        }
    }
}
