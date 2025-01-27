// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.TestTool.Models;

namespace Amazon.Lambda.TestTool;

/// <summary>
/// Provides constant values used across the application.
/// </summary>
public abstract class Constants
{
    /// <summary>
    /// The name of the dotnet CLI tool
    /// </summary>
    public const string ToolName = "dotnet-lambda-test-tool";

    /// <summary>
    /// The default port used by the Lambda Test Tool for the Lambda Runtime API and the Web Interface.
    /// </summary>
    public const int DefaultLambdaEmulatorPort = 5050;

    /// <summary>
    /// The default port used by the API Gateway Emulator.
    /// </summary>
    public const int DefaultApiGatewayEmulatorPort = 5051;

    /// <summary>
    /// The default hostname used for the Lambda Test Tool.
    /// </summary>
    public const string DefaultLambdaEmulatorHost = "localhost";

    /// <summary>
    /// The default mode for the API Gateway Emulator.
    /// </summary>
    public const ApiGatewayEmulatorMode DefaultApiGatewayEmulatorMode = ApiGatewayEmulatorMode.HttpV2;

    /// <summary>
    /// The prefix for environment variables used to configure the Lambda functions.
    /// </summary>
    public const string LambdaConfigEnvironmentVariablePrefix = "APIGATEWAY_EMULATOR_ROUTE_CONFIG";

    /// <summary>
    /// The product name displayed for the Lambda Test Tool.
    /// </summary>
    public const string ProductName = "AWS .NET Mock Lambda Test Tool";

    /// <summary>
    /// The CSS style used for successful responses in the tool's UI.
    /// </summary>
    public const string ResponseSuccessStyle = "white-space: pre-wrap; height: min-content; font-size: 75%; color: black";

    /// <summary>
    /// The CSS style used for error responses in the tool's UI.
    /// </summary>
    public const string ResponseErrorStyle = "white-space: pre-wrap; height: min-content; font-size: 75%; color: red";

    /// <summary>
    /// The CSS style used for successful responses in the tool's UI when a size constraint is applied.
    /// </summary>
    public const string ResponseSuccessStyleSizeConstraint = "white-space: pre-wrap; height: 300px; font-size: 75%; color: black";

    /// <summary>
    /// The CSS style used for error responses in the tool's UI when a size constraint is applied.
    /// </summary>
    public const string ResponseErrorStyleSizeConstraint = "white-space: pre-wrap; height: 300px; font-size: 75%; color: red";

    /// <summary>
    /// The GitHub repository link for the AWS Lambda .NET repository.
    /// </summary>
    public const string LinkGithubRepo = "https://github.com/aws/aws-lambda-dotnet";

    /// <summary>
    /// The GitHub link for the Lambda Test Tool.
    /// </summary>
    public const string LinkGithubTestTool = "https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool-v2";

    /// <summary>
    /// The GitHub link for the Lambda Test Tool's installation and running instructions.
    /// </summary>
    public const string LinkGithubTestToolInstallAndRun = "https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool#installing-and-running";

    /// <summary>
    /// The AWS Developer Guide link for Dead Letter Queues in AWS Lambda.
    /// </summary>
    public const string LinkDlqDeveloperGuide = "https://docs.aws.amazon.com/lambda/latest/dg/dlq.html";

    /// <summary>
    /// The Microsoft documentation link for the <see cref="System.Runtime.Loader.AssemblyLoadContext"/> class.
    /// </summary>
    public const string LinkMsdnAssemblyLoadContext = "https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext";

    /// <summary>
    /// The Visual Studio Marketplace link for the AWS Toolkit for Visual Studio.
    /// </summary>
    public const string LinkVsToolkitMarketplace = "https://marketplace.visualstudio.com/items?itemName=AmazonWebServices.AWSToolkitforVisualStudio2022";
}
