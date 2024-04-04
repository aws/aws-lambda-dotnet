namespace Amazon.Lambda.TestTool.BlazorTester
{
    public class Constants
    {
        public const int DEFAULT_PORT = 5050;
        public const string DEFAULT_HOST = "localhost";

#if NET6_0
        public const string PRODUCT_NAME = "AWS .NET 6.0 Mock Lambda Test Tool";
#elif NET7_0
        public const string PRODUCT_NAME = "AWS .NET 7.0 Mock Lambda Test Tool";
#elif NET8_0
        public const string PRODUCT_NAME = "AWS .NET 8.0 Mock Lambda Test Tool";
#else
        Update for new target framework!!!
#endif

        public const string ResponseSuccessStyle = "white-space: pre-wrap; height: min-content; font-size: 75%; color: black";
        public const string ResponseErrorStyle = "white-space: pre-wrap; height: min-content; font-size: 75%; color: red";

        public const string ResponseSuccessStyleSizeConstraint = "white-space: pre-wrap; height: 300px; font-size: 75%; color: black";
        public const string ResponseErrorStyleSizeConstraint = "white-space: pre-wrap; height: 300px; font-size: 75%; color: red";


        public const string LINK_GITHUB_TEST_TOOL = "https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool";
        public const string LINK_GITHUB_TEST_TOOL_INSTALL_AND_RUN = "https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool#installing-and-running";
        public const string LINK_DLQ_DEVELOEPR_GUIDE = "https://docs.aws.amazon.com/lambda/latest/dg/dlq.html";
        public const string LINK_MSDN_ASSEMBLY_LOAD_CONTEXT = "https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext";
        public const string LINK_VS_TOOLKIT_MARKETPLACE = "https://marketplace.visualstudio.com/items?itemName=AmazonWebServices.AWSToolkitforVisualStudio2017";
    }
}
