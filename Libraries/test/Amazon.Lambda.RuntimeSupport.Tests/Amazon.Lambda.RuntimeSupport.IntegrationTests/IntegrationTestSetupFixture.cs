using Amazon.Lambda.RuntimeSupport.IntegrationTests.Helpers;
using NUnit.Framework;

namespace Amazon.Lambda.RuntimeSupport.IntegrationTests;

[SetUpFixture]
public class IntegrationTestSetupFixture
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        string testAppPath = null;
        string toolPath = null;
        try
        {
            testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
                "../../../../../../..",
                "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeFunctionTest");
            toolPath = LambdaToolsHelper.InstallLambdaTools();
            LambdaToolsHelper.DotnetRestore(testAppPath);
            LambdaToolsHelper.LambdaPackage(toolPath, "net6.0", testAppPath);
            LambdaToolsHelper.LambdaPackage(toolPath, "net8.0", testAppPath);
        }
        finally
        {
            LambdaToolsHelper.CleanUp(testAppPath);
            LambdaToolsHelper.CleanUp(toolPath);
        }
        
        try
        {
            testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
                "../../../../../../..",
                "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiTest");
            toolPath = LambdaToolsHelper.InstallLambdaTools();
            LambdaToolsHelper.DotnetRestore(testAppPath);
            LambdaToolsHelper.LambdaPackage(toolPath, "net6.0", testAppPath);
        }
        finally
        {
            LambdaToolsHelper.CleanUp(testAppPath);
            LambdaToolsHelper.CleanUp(toolPath);
        }
        
        try
        {
            testAppPath = LambdaToolsHelper.GetTempTestAppDirectory(
                "../../../../../../..",
                "Libraries/test/Amazon.Lambda.RuntimeSupport.Tests/CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest");
            toolPath = LambdaToolsHelper.InstallLambdaTools();
            LambdaToolsHelper.DotnetRestore(testAppPath);
            LambdaToolsHelper.LambdaPackage(toolPath, "net6.0", testAppPath);
        }
        finally
        {
            LambdaToolsHelper.CleanUp(testAppPath);
            LambdaToolsHelper.CleanUp(toolPath);
        }
    }
}