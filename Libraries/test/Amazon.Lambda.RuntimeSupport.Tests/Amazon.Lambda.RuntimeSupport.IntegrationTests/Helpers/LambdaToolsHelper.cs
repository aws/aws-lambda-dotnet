using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;

public static class LambdaToolsHelper
{
    private static readonly string FunctionArchitecture = RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x86_64";

    public static string GetTempTestAppDirectory(string workingDirectory, string testAppPath)
    {
        var customTestAppPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(customTestAppPath);

        var currentDir = new DirectoryInfo(workingDirectory);
        CopyDirectory(currentDir, customTestAppPath);

        return Path.Combine(customTestAppPath, testAppPath);
    }

    public static async Task<string> InstallLambdaTools()
    {
        var customToolPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(customToolPath);
        await CommandLineWrapper.Run(
            "dotnet", 
            $"tool install Amazon.Lambda.Tools --tool-path {customToolPath}",
            Directory.GetCurrentDirectory());
        return customToolPath;
    }

    public static async Task LambdaPackage(string toolPath, string framework, string workingDirectory)
    {
        string lambdaToolPath = Path.Combine(toolPath, "dotnet-lambda");
        await CommandLineWrapper.Run(
            lambdaToolPath, 
            $"package -c Release --framework {framework} --function-architecture {FunctionArchitecture}", 
            workingDirectory);
    }

    public static void CleanUp(string toolPath)
    {
        if (!string.IsNullOrEmpty(toolPath) && Directory.Exists(toolPath))
        {
            Directory.Delete(toolPath, true);
        }
    }

    /// <summary>
    /// <see cref="https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories"/>
    /// </summary>
    private static void CopyDirectory(DirectoryInfo dir, string destDirName)
    {
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {dir.FullName}");
        }

        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destDirName);

        var files = dir.GetFiles();
        foreach (var file in files)
        {
            var tempPath = Path.Combine(destDirName, file.Name);
            file.CopyTo(tempPath, false);
        }

        foreach (var subdir in dirs)
        {
            if (string.Equals(subdir.Name, ".vs", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var tempPath = Path.Combine(destDirName, subdir.Name);
            var subDir = new DirectoryInfo(subdir.FullName);
            CopyDirectory(subDir, tempPath);
        }
    }
}