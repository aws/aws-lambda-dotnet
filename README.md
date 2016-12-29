# AWS Lambda for .NET Core

Repository for the AWS NuGet packages and Blueprints to support writing AWS Lambda functions using .NET Core.

For a history of releases view the [release change log](RELEASE.CHANGELOG.md)

## NuGet Packages

### Events

This packages in this folder contains classes that can be used as input types for Lambda functions that process various AWS events.

These are the packages and their README.md files:

* [Amazon.Lambda.APIGatewayEvents](Libraries/Amazon.Lambda.APIGatewayEvents) - [README.md](Libraries/Amazon.Lambda.APIGatewayEvents/README.md)
* [Amazon.Lambda.CognitoEvents](Libraries/Amazon.Lambda.CognitoEvents) - [README.md](Libraries/Amazon.Lambda.CognitoEvents/README.md)
* [Amazon.Lambda.ConfigEvents](Libraries/Amazon.Lambda.ConfigEvents) - [README.md](Libraries/Amazon.Lambda.ConfigEvents/README.md)
* [Amazon.Lambda.DynamoDBEvents](Libraries/Amazon.Lambda.DynamoDBEvents) - [README.md](Libraries/Amazon.Lambda.DynamoDBEvents/README.md)
* [Amazon.Lambda.KinesisEvents](Libraries/Amazon.Lambda.KinesisEvents) - [README.md](Libraries/Amazon.Lambda.KinesisEvents/README.md)
* [Amazon.Lambda.S3Events](Libraries/Amazon.Lambda.S3Events) - [README.md](Libraries/Amazon.Lambda.S3Events/README.md)
* [Amazon.Lambda.SNSEvents](Libraries/Amazon.Lambda.SNSEvents) - [README.md](Libraries/Amazon.Lambda.SNSEvents/README.md)

### Amazon.Lambda.Tools

Package adds commands to the dotnet cli that can be used manage Lambda functions including deploying a function from the dotnet cli. 
For more information see the [README.md](Libraries/src/Amazon.Lambda.Tools/README.md) file for Amazon.Lambda.Tools.

### Amazon.Lambda.AspNetCoreServer

Packages makes it easy to run ASP.NET Core Web API applications as Lambda functions.
For more information see the [README.md](Libraries/Amazon.Lambda.AspNetCoreServer/README.md) file for Amazon.Lambda.AspNetCoreServer.

### Amazon.Lambda.TestUtilities

Package includes test implementation of the interfaces from Amazon.Lambda.Core and helper methods to help in locally testing.
For more information see the [README.md](Libraries/Amazon.Lambda.TestUtilities/README.md) file for Amazon.Lambda.TestUtilities.

## Blueprints

Blueprints in this repository are .NET Core Lambda functions that can used to get started. In Visual Studio the Blueprints are avalible when creating a new project and selecting the AWS Lambda Project.

### Yeoman
For developers not using Visual Studio the Blueprints can be used with a Yeoman generator. To use Yeoman, Node.js and npm must be installed which can be obtain from [nodejs.org](https://nodejs.org/en/download/)

Once npm is installed the Yeoman generator can be installed using the following command.
```
npm install -g yo generator-aws-lambda-dotnet
```

To run the generator and select a Blueprint run the following command.
```
yo aws-lambda-dotnet
```
