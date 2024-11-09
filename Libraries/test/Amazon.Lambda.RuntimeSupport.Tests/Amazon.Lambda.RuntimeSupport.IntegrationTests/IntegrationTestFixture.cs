using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly List<string> _tempPaths = new();
    
    public async Task InitializeAsync()
    {
        var testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeFunctionTest");
        var toolPath = await LambdaToolsHelper.InstallLambdaTools();
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net6.0", testAppPath);
        await LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        
        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiTest");
        toolPath = await LambdaToolsHelper.InstallLambdaTools();
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net6.0", testAppPath);
        
        testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
            "../../../../../../..",
            "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest");
        toolPath = await LambdaToolsHelper.InstallLambdaTools();
        _tempPaths.AddRange([testAppPath, toolPath] );
        await LambdaToolsHelper.LambdaPackage(toolPath, "net6.0", testAppPath);
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