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
