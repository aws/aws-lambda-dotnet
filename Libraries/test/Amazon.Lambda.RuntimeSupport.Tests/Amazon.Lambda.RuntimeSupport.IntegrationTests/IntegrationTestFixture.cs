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
        var testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeFunctionTest");
        var toolPath = await LambdaToolsHelper.InstallLambdaTools();
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        TestAppPaths[@"CustomRuntimeFunctionTest\bin\Release\net8.0\CustomRuntimeFunctionTest.zip"] = Path.Combine(testAppPath, @"bin\Release\net8.0\CustomRuntimeFunctionTest.zip");

        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiTest");
        toolPath = await LambdaToolsHelper.InstallLambdaTools();
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        TestAppPaths[@"CustomRuntimeAspNetCoreMinimalApiTest\bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiTest.zip"] = Path.Combine(testAppPath, @"bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiTest.zip");

        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest");
        toolPath = await LambdaToolsHelper.InstallLambdaTools();
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        TestAppPaths[@"CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest\bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest.zip"] = Path.Combine(testAppPath, @"bin\Release\net8.0\CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest.zip");
    }


    public Task DisposeAsync()
    {
        foreach (var tempPath in _tempPaths)
        {
            LambdaToolsHelper.CleanUp(tempPath);
        }
        
        return Task.CompletedTask;
    }
}
