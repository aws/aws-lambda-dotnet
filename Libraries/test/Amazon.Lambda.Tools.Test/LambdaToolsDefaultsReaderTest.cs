using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        [Fact]
        public void LoadDefaultsDirectly()
        {
            var defaults = LambdaToolsDefaultsReader.LoadDefaults("../TestFunction");

            Assert.Equal(defaults.Region, "us-east-2");
            Assert.Equal(defaults["region"], "us-east-2");
        }

        [Fact]
        public void CommandInferRegionFromDefaults()
        {
            var fullPath = Path.GetFullPath("../TestFunction");
            var command = new DeployFunctionCommand(new ConsoleToolLogger(), fullPath, new string[0]);

            Assert.Equal("us-east-2", command.GetStringValueOrDefault(command.Region, DefinedCommandOptions.ARGUMENT_AWS_REGION, true));
        }
    }
}
