using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly List<string> _tempPaths = new();

    public IDictionary<string, string> TestAppPaths { get; } = new Dictionary<string, string>();

    public async Task InitializeAsync()
    {
        var toolPath = await LambdaToolsHelper.InstallLambdaTools();

        var testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeFunctionTest");
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        TestAppPaths[@"CustomRuntimeFunctionTest\bin\Release\net8.0\CustomRuntimeFunctionTest.zip"] = Path.Combine(testAppPath, @"bin\Release\net8.0\CustomRuntimeFunctionTest.zip");

        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiTest");
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        TestAppPaths[@"CustomRuntimeAspNetCoreMinimalApiTest\bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiTest.zip"] = Path.Combine(testAppPath, @"bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiTest.zip");

        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest");
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        TestAppPaths[@"CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest\bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest.zip"] = Path.Combine(testAppPath, @"bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest.zip");

        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/ResponseStreamingFunctionHandlers");
        _tempPaths.AddRange([testAppPath, toolPath]);
        await LambdaToolsHelper.LambdaPackage(toolPath, "net10.0", testAppPath);
        TestAppPaths[@"ResponseStreamingFunctionHandlers\bin\Release\net10.0\ResponseStreamingFunctionHandlers.zip"] = Path.Combine(testAppPath, "bin", "Release", "net10.0", "ResponseStreamingFunctionHandlers.zip"); ;
    }


    public Task DisposeAsync()
    {
#if !DEBUG
        foreach (var tempPath in _tempPaths)
        {
            LambdaToolsHelper.CleanUp(tempPath);
        }
#endif
        
        return Task.CompletedTask;
    }
}
