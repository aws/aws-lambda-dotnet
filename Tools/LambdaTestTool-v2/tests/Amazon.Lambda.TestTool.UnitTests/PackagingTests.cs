using System.Diagnostics;
using System.IO.Compression;
using Amazon.Lambda.TestTool.UnitTests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.TestTool.UnitTests;

public class PackagingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _workingDirectory;

    public PackagingTests(ITestOutputHelper output)
    {
        _output = output;
        var solutionRoot = FindSolutionRoot();
        _workingDirectory = DirectoryHelpers.GetTempTestAppDirectory(solutionRoot);
    }

    [Fact]
    public void VerifyPackageContentsHasStaticAssets()
    {
        var projectPath = Path.Combine(_workingDirectory, "Tools", "LambdaTestTool-v2", "src", "Amazon.Lambda.TestTool", "Amazon.Lambda.TestTool.csproj");
        _output.WriteLine("Packing TestTool...");
        var packProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack -c Release --no-build --no-restore {projectPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        packProcess.Start();
        string packOutput = packProcess.StandardOutput.ReadToEnd();
        string packError = packProcess.StandardError.ReadToEnd();
        packProcess.WaitForExit(int.MaxValue);

        _output.WriteLine("Pack Output:");
        _output.WriteLine(packOutput);
        if (!string.IsNullOrEmpty(packError))
        {
            _output.WriteLine("Pack Errors:");
            _output.WriteLine(packError);
        }

        Assert.Equal(0, packProcess.ExitCode);

        var packageDir = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Release");
        _output.WriteLine($"Looking for package in: {packageDir}");

        var packageFiles = Directory.GetFiles(packageDir, "*.nupkg", SearchOption.AllDirectories);
        Assert.True(packageFiles.Length > 0, $"No .nupkg files found in {packageDir}");

        var packagePath = packageFiles[0];
        _output.WriteLine($"Found package: {packagePath}");

        using var archive = ZipFile.OpenRead(packagePath);

        // Get all files for this framework
        var frameworkFiles = archive.Entries
            .Where(e => e.FullName.StartsWith($"tools/net8.0/any/wwwroot"))
            .Select(e => e.FullName)
            .ToList();

        // Verify essential files exist
        var essentialFiles = new[]
        {
            $"tools/net8.0/any/wwwroot/bootstrap-icons/",
            $"tools/net8.0/any/wwwroot/bootstrap/",
            $"tools/net8.0/any/wwwroot/_content/BlazorMonaco/"
        };

        var missingFiles = essentialFiles.Where(f => !frameworkFiles.Any(x => x.StartsWith(f))).ToList();

        if (missingFiles.Any())
        {
            Assert.Fail($"The following static assets are missing:\n" +
                        string.Join("\n", missingFiles));
        }
    }

    [Fact]
    public void VerifyPackageContentsHasRuntimeSupport()
    {
        var projectPath = Path.Combine(_workingDirectory, "Tools", "LambdaTestTool-v2", "src", "Amazon.Lambda.TestTool", "Amazon.Lambda.TestTool.csproj");
        var expectedFrameworks = new string[] { "net6.0", "net8.0", "net9.0", "net10.0" };
        _output.WriteLine("Packing TestTool...");
        var packProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack -c Release --no-build --no-restore {projectPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        packProcess.Start();
        string packOutput = packProcess.StandardOutput.ReadToEnd();
        string packError = packProcess.StandardError.ReadToEnd();
        packProcess.WaitForExit(int.MaxValue);

        _output.WriteLine("Pack Output:");
        _output.WriteLine(packOutput);
        if (!string.IsNullOrEmpty(packError))
        {
            _output.WriteLine("Pack Errors:");
            _output.WriteLine(packError);
        }

        Assert.Equal(0, packProcess.ExitCode);

        var packageDir = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Release");
        _output.WriteLine($"Looking for package in: {packageDir}");

        var packageFiles = Directory.GetFiles(packageDir, "*.nupkg", SearchOption.AllDirectories);
        Assert.True(packageFiles.Length > 0, $"No .nupkg files found in {packageDir}");

        var packagePath = packageFiles[0];
        _output.WriteLine($"Found package: {packagePath}");

        using var archive = ZipFile.OpenRead(packagePath);
        // Verify each framework has its required files
        foreach (var framework in expectedFrameworks)
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
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.RuntimeSupport.TestTool.dll",
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.RuntimeSupport.TestTool.deps.json",
                $"content/Amazon.Lambda.RuntimeSupport/{framework}/Amazon.Lambda.RuntimeSupport.TestTool.runtimeconfig.json"
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

    private string FindSolutionRoot()
    {
        Console.WriteLine("Looking for solution root...");
        string? currentDirectory = Directory.GetCurrentDirectory();
        while (currentDirectory != null)
        {
            // Look for the "Tools" directory specifically and then go up one level to the root of the repository.
            // The reason we do this is because the source directory "aws-lambda-dotnet" does not always exist in the CI.
            // In CodeBuild, the contents of "aws-lambda-dotnet" get copied to a temp location,
            // so the path does not contain the name "aws-lambda-dotnet".
            if (Path.GetFileName(currentDirectory) == "Tools")
            {
                return Path.Combine(currentDirectory, "..");
            }
            currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        }
        throw new Exception("Could not find the 'Tools' root directory.");
    }

    public void Dispose()
    {
        DirectoryHelpers.CleanUp(_workingDirectory);
    }
}
