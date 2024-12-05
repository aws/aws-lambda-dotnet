using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests;

public class PackagingTests
{
    [Fact]
    public void VerifyPackageContentsHasRuntimeSupport()
    {
        string projectPath = Path.Combine(FindSolutionRoot(), "src", "Amazon.Lambda.TestTool", "Amazon.Lambda.TestTool.csproj");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack {projectPath} -c Release",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);

        string packagePath = Directory.GetFiles(Path.GetDirectoryName(projectPath), "*.nupkg", SearchOption.AllDirectories)[0];

        using (var archive = ZipFile.OpenRead(packagePath))
        {
            var runtimeSupportDllEntry = archive.GetEntry("content/Amazon.Lambda.RuntimeSupport.dll");
            Assert.NotNull(runtimeSupportDllEntry);
        }
    }

    private string FindSolutionRoot()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null)
        {
            string[] solutionFiles = Directory.GetFiles(currentDirectory, "*.sln");
            if (solutionFiles.Length > 0)
            {
                return currentDirectory;
            }
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }
        throw new Exception("Could not find the solution root directory.");
    }


}
