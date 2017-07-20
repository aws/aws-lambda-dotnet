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


namespace Amazon.Lambda.Tools.Test
{
    public class CheckIfDependencyBringsInNETCore11
    {
        private string GetTestProjectPath(string project)
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;
            var fullPath = Path.GetFullPath(Path.GetDirectoryName(assembly.Location) + "../../../../../DependencyCheckTestProjects/" + project);
            return fullPath;
        }

        [Fact]
        public async Task UseNewtonsoft10()
        {
            var fullPath = GetTestProjectPath("UseNewtonsoft10");
            var logger = new TestToolLogger();
            var command = new PackageCommand(logger, fullPath, new string[0]);
            command.DisableInteractive = true;
            command.Configuration = "Release";
            command.TargetFramework = "netcoreapp1.0";

            command.OutputPackageFileName = Path.GetTempFileName() + ".zip";

            var created = await command.ExecuteAsync();
            Assert.False(created);

            Assert.True(logger.Buffer.ToLower().Contains("error: netstandard.library 1.6.1 is used for target framework netcoreapp1.1"));
            Assert.True(logger.Buffer.ToLower().Contains("error: 	newtonsoft.json : 10.0.2"));
        }

        [Fact]
        public async Task Use11ASPNetCoreDependencies()
        {
            var fullPath = GetTestProjectPath("Use11ASPNetCoreDependencies");
            var logger = new TestToolLogger();
            var command = new PackageCommand(logger, fullPath, new string[0]);
            command.DisableInteractive = true;
            command.Configuration = "Release";
            command.TargetFramework = "netcoreapp1.0";

            command.OutputPackageFileName = Path.GetTempFileName() + ".zip";

            var created = await command.ExecuteAsync();
            Assert.False(created);

            Assert.True(logger.Buffer.ToLower().Contains("error: netstandard.library 1.6.1 is used for target framework netcoreapp1.1"));
            Assert.True(logger.Buffer.ToLower().Contains("error: 	microsoft.aspnetcore.diagnostics : 1.1.1"));
            Assert.True(logger.Buffer.ToLower().Contains("error: 	microsoft.extensions.fileproviders.embedded : 1.1.0"));
        }
    }
}
