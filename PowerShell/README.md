# The AWS Lambda Tools for Powershell

The AWS Lambda Tools for Powershell allows PowerShell developers to publish PowerShell scripts 
with their dependent modules and have the scripts be invoked by Lambda.

### Setting up a development environment

Before we get started developing PowerShell based Lambda functions, let's set up our 
development environment. .

First, we need to set up the correct version of PowerShell. AWS Lambda support 
for PowerShell is based on the cross-platform PowerShell Core 6.0 release. This means 
you can develop your Lambda functions for PowerShell on Windows, Linux, or Mac. If you don’t 
have this version of PowerShell installed, you can find instructions [here](https://docs.microsoft.com/en-us/powershell/scripting/setup/installing-powershell-core-on-windows?view=powershell-6).

If you are using Visual Studio Code on Windows as your IDE, you need to ensure it's 
configured for PowerShell Core 6.0. To learn how to configure Visual Studio Code for 
PowerShell Core, see the following: https://docs.microsoft.com/en-us/powershell/scripting/core-powershell/vscode/using-vscode?view=powershell-6

Next, we need to install the .NET Core 2.1 SDK. Because PowerShell Core is built on top of 
.NET Core, the Lambda support for PowerShell uses the same .NET Core 2.1 Lambda runtime for 
both .NET Core and PowerShell based Lambda functions. The .NET Core 2.1 SDK is used by 
the new PowerShell publishing cmdlets for Lambda to create the Lambda deployment 
package. You can find the .NET Core 2.1 SDK here: https://www.microsoft.com/net/download Be 
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