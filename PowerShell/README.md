# The AWS Lambda Tools for Powershell

The AWS Lambda Tools for Powershell allows PowerShell developers to publish PowerShell scripts 
with their dependent modules and have the scripts be invoked by Lambda.

## Setting up a development environment

Before we get started developing PowerShell based Lambda functions, let's set up our 
development environment.

First, we need to set up the correct version of PowerShell. AWS Lambda support 
for PowerShell is based on the cross-platform PowerShell release. This means 
you can develop your Lambda functions for PowerShell on Windows, Linux, or Mac. If you don't 
have this version of PowerShell installed, you can find instructions [here](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.4).

If you are using Visual Studio Code on Windows as your IDE, you need to ensure it's 
configured for PowerShell Core. To learn how to configure Visual Studio Code for 
PowerShell Core, see the following: https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/vscode/using-vscode?view=powershell-7.4

Next, we need to install the .NET 8 SDK. Because PowerShell Core is built on top of 
.NET Core, the Lambda support for PowerShell uses the same .NET 8 Lambda runtime for 
both .NET Core and PowerShell based Lambda functions. The .NET 8 SDK is used by 
the new PowerShell publishing cmdlets for Lambda to create the Lambda deployment 
package. You can find the .NET 8 SDK [here]( https://www.microsoft.com/net/download). Be 
sure to install the SDK, not the runtime installation.

The last component we need for the development environment is the 
new **AWSLambdaPSCore** module that you can install from the PowerShell Gallery. The 
following is an example of installing the module from a PowerShell Core shell.

```
Install-Module AWSLambdaPSCore -Scope CurrentUser
```

This new module has the following new cmdlets to help you author and publish PowerShell based Lambda functions.

Cmdlet name | Description
------------ | -------------
Get-AWSPowerShellLambdaTemplate|Returns a list of getting started templates.
New-AWSPowerShellLambda|Used to create an initial PowerShell script that is based on a template.
Publish-AWSPowerShellLambda|Publishes a given PowerShell script to Lambda.
New-AWSPowerShellLambdaPackage|Creates the Lambda deployment package that can be used in a CI/CD system for deployment.

## Troubleshooting
### Enabling Debug output
In PowerShell, [Write-Debug](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/write-debug?view=powershell-7.4) CmdLet could be used to write debug message to the console. However, by default, debug messages are not displayed in the console, but you can display them by using the **Debug** parameter or the **$DebugPreference** variable.

The default value of [DebugPreference](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables?view=powershell-7.4#debugpreference) variable is `SilentlyContinue`, which means the debug message isn't displayed and execution continues without interruption. The `-Debug` parameter could be used to override the `$DebugPreference` value.

Enabling output of `Write-Debug` to CloudWatch logs is a 2 step process:
- In PowerShell Lambda script, 
  - Either need to set `$DebugPreference = "Continue"` at the beginning of script. Thereafter use `Write-Debug` to output debug messages ; **OR**
  - Include `-Debug` parameter while executing `Write-Debug` (e.g.` Write-Debug "Testing Lambda PowerShell Write-Debug" -Debug`).
- At Lambda function level, set the value of `AWS_LAMBDA_HANDLER_LOG_LEVEL` environment variable with value `DEBUG`. This would enable debug logs at Lambda level. This environment variable could be set:
  - Either manually in Lambda function configuration in AWS console; **OR**
  - While executing CmdLet `Publish-AWSPowerShellLambda`, passing parameter `-EnvironmentVariable @{'AWS_LAMBDA_HANDLER_LOG_LEVEL'='DEBUG'}`.
  
  The value of the `AWS_LAMBDA_HANDLER_LOG_LEVEL` environment variable is set to the values of the [LogLevel](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.Core/ILambdaLogger.cs#L7) enum.

The role assigned to Lambda function should have permissions to write to CloudWatch logs. 


# Learning Resources

[Lambda Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html)
  * [Programming Model for Authoring Lambda Functions in PowerShell](https://docs.aws.amazon.com/lambda/latest/dg/powershell-programming-model.html)
  * [Creating a Deployment Package (PowerShell)](https://docs.aws.amazon.com/lambda/latest/dg/lambda-powershell-how-to-create-deployment-package.html)
  
### AWS Blog Posts

* [Announcing Lambda Support for PowerShell Core](https://aws.amazon.com/blogs/developer/announcing-lambda-support-for-powershell-core/)
* [Deploying PowerShell-based Lambda with AWS CloudFormation](https://aws.amazon.com/blogs/developer/deploying-powershell-based-lambda-with-aws-cloudformation/)
* [Creating a PowerShell REST API](https://aws.amazon.com/blogs/developer/creating-a-powershell-rest-api/)

### AWS Recorded Talks
* [Unleash your PowerShell with AWS Lambda and Serverless Computing](https://www.youtube.com/watch?v=-CmIrrEYtLA) - PowerShell and DevOps Global Summit 2019 by Andrew Pearce
  * Introduces PowerShell language support in AWS Lambda, introduces event driven design patterns and demonstrates a PowerShell Serverless application using Amazon Simple Notification Service (SNS), Amazon Simple Queue Service (SQS) and Amazon API Gateway.
