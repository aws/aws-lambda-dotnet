using System.Diagnostics;
using System.IO.Compression;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.UnitTests;

public class PackagingTests
{
    private readonly ITestOutputHelper _output;
    private readonly string[] _expectedFrameworks;

    public PackagingTests(ITestOutputHelper output)
    {
        _output = output;
        _expectedFrameworks = GetRuntimeSupportTargetFrameworks()
            .Split([';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(f => f != "netstandard2.0")
            .ToArray();
    }

    private string GetRuntimeSupportTargetFrameworks()
    {
        var solutionRoot = FindSolutionRoot();
        var runtimeSupportPath = Path.Combine(solutionRoot, "Libraries", "src", "Amazon.Lambda.RuntimeSupport", "Amazon.Lambda.RuntimeSupport.csproj");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"msbuild {runtimeSupportPath} --getProperty:TargetFrameworks",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Failed to get TargetFrameworks: {error}");
        }

        return output.Trim();
    }

    [Fact]
    public void VerifyPackageContentsHasRuntimeSupport()
    {
        var solutionRoot = FindSolutionRoot();
        var projectPath = Path.Combine(solutionRoot, "Tools", "LambdaTestTool-v2", "src", "Amazon.Lambda.TestTool", "Amazon.Lambda.TestTool.csproj");

        _output.WriteLine("\nPacking TestTool...");
        var packProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack {projectPath} -c Release",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        packProcess.Start();
        string packOutput = packProcess.StandardOutput.ReadToEnd();
        string packError = packProcess.StandardError.ReadToEnd();
        packProcess.WaitForExit();

        _output.WriteLine("Pack Output:");
        _output.WriteLine(packOutput);
        if (!string.IsNullOrEmpty(packError))
        {
            _output.WriteLine("Pack Errors:");
            _output.WriteLine(packError);
        }

        Assert.Equal(0, packProcess.ExitCode);

        var packageDir = Path.Combine(Path.GetDirectoryName(projectPath), "bin", "Release");
        _output.WriteLine($"Looking for package in: {packageDir}");

        var packageFiles = Directory.GetFiles(packageDir, "*.nupkg", SearchOption.AllDirectories);
        Assert.True(packageFiles.Length > 0, $"No .nupkg files found in {packageDir}");

        var packagePath = packageFiles[0];
        _output.WriteLine($"Found package: {packagePath}");

        using var archive = ZipFile.OpenRead(packagePath);
        // Verify each framework has its required files
        foreach (var framework in _expectedFrameworks)
        {
            _output.WriteLine($"\nChecking framework: {framework}");

            // Get all files for this framework
            var frameworkFiles = archive.Entries
                .Where(e => e.FullName.StartsWith($"content/Amazon.Lambda.RuntimeSupport/{framework}/"))
                .Select(e => e.FullName)
                .ToList();

            // Verify essential files exist
            var essentialFiles = new[]
            {
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.Core.dll",
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.RuntimeSupport.dll",
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.RuntimeSupport.deps.json",
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.RuntimeSupport.runtimeconfig.json"
            };

            var missingFiles = essentialFiles.Where(f => !frameworkFiles.Contains(f)).ToList();

            if (missingFiles.Any())
            {
                Assert.Fail($"The following essential files are missing for {framework}:\n" +
                            string.Join("\n", missingFiles));
            }

            _output.WriteLine($"Files found for {framework}:");
            foreach (var file in frameworkFiles)
            {
                _output.WriteLine($"  {file}");
            }
        }
    }

    private string? FindSolutionRoot()
    {
        string? currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null)
        {
            // Look for the aws-lambda-dotnet directory specifically
            if (Path.GetFileName(currentDirectory) == "aws-lambda-dotnet")
            {
                return currentDirectory;
            }
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }
        throw new Exception("Could not find the aws-lambda-dotnet root directory.");
    }
}
