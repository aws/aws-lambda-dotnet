namespace Amazon.Lambda.TestTool;

public abstract class Constants
{
    public const string ToolName = "lambda-test-tool";
    public const int DefaultPort = 5050;
    public const string DefaultHost = "localhost";

    public const string ProductName = "AWS .NET Mock Lambda Test Tool";

    public const string ResponseSuccessStyle = "white-space: pre-wrap; height: min-content; font-size: 75%; color: black";
    public const string ResponseErrorStyle = "white-space: pre-wrap; height: min-content; font-size: 75%; color: red";

    public const string ResponseSuccessStyleSizeConstraint = "white-space: pre-wrap; height: 300px; font-size: 75%; color: black";
    public const string ResponseErrorStyleSizeConstraint = "white-space: pre-wrap; height: 300px; font-size: 75%; color: red";

    public const string LinkGithubRepo = "https://github.com/aws/aws-lambda-dotnet";
    public const string LinkGithubTestTool = "https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool";
    public const string LinkGithubTestToolInstallAndRun = "https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool#installing-and-running";
    public const string LinkDlqDeveloperGuide = "https://docs.aws.amazon.com/lambda/latest/dg/dlq.html";
    public const string LinkMsdnAssemblyLoadContext = "https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext";
    public const string LinkVsToolkitMarketplace = "https://marketplace.visualstudio.com/items?itemName=AmazonWebServices.AWSToolkitforVisualStudio2017";
}
