# AWS Lambda for .NET Core [![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/aws/aws-lambda-dotnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Repository for the AWS NuGet packages and Blueprints to support writing AWS Lambda functions using .NET Core.

For a history of releases view the [release change log](RELEASE.CHANGELOG.md)

## NuGet Packages

### Events

This packages in this folder contains classes that can be used as input types for Lambda functions that process various AWS events.

These are the packages and their README.md files:

* [Amazon.Lambda.APIGatewayEvents](Libraries/src/Amazon.Lambda.APIGatewayEvents) - [README.md](Libraries/src/Amazon.Lambda.APIGatewayEvents/README.md)
* [Amazon.Lambda.CognitoEvents](Libraries/src/Amazon.Lambda.CognitoEvents) - [README.md](Libraries/src/Amazon.Lambda.CognitoEvents/README.md)
* [Amazon.Lambda.ConfigEvents](Libraries/src/Amazon.Lambda.ConfigEvents) - [README.md](Libraries/src/Amazon.Lambda.ConfigEvents/README.md)
* [Amazon.Lambda.DynamoDBEvents](Libraries/src/Amazon.Lambda.DynamoDBEvents) - [README.md](Libraries/src/Amazon.Lambda.DynamoDBEvents/README.md)
* [Amazon.Lambda.LexEvents](Libraries/src/Amazon.Lambda.LexEvents) - [README.md](Libraries/src/Amazon.Lambda.LexEvents/README.md)
* [Amazon.Lambda.KinesisEvents](Libraries/src/Amazon.Lambda.KinesisEvents) - [README.md](Libraries/src/Amazon.Lambda.KinesisEvents/README.md)
* [Amazon.Lambda.KinesisFirehoseEvents](Libraries/src/Amazon.Lambda.KinesisFirehoseEvents) - [README.md](Libraries/src/Amazon.Lambda.KinesisFirehoseEvents/README.md)
* [Amazon.Lambda.S3Events](Libraries/src/Amazon.Lambda.S3Events) - [README.md](Libraries/src/Amazon.Lambda.S3Events/README.md)
* [Amazon.Lambda.SimpleEmailEvents](Libraries/src/Amazon.Lambda.SimpleEmailEvents) - [README.md](Libraries/src/Amazon.Lambda.SimpleEmailEvents/README.md)
* [Amazon.Lambda.SNSEvents](Libraries/src/Amazon.Lambda.SNSEvents) - [README.md](Libraries/src/Amazon.Lambda.SNSEvents/README.md)

### Amazon.Lambda.Tools

Package adds commands to the dotnet cli that can be used manage Lambda functions including deploying a function from the dotnet cli. 
For more information see the [README.md](Libraries/src/Amazon.Lambda.Tools/README.md) file for Amazon.Lambda.Tools.

### Amazon.Lambda.AspNetCoreServer

Package makes it easy to run ASP.NET Core Web API applications as Lambda functions.
For more information see the [README.md](Libraries/src/Amazon.Lambda.AspNetCoreServer/README.md) file for Amazon.Lambda.AspNetCoreServer.

### Amazon.Lambda.TestUtilities

Package includes test implementation of the interfaces from Amazon.Lambda.Core and helper methods to help in locally testing.
For more information see the [README.md](Libraries/src/Amazon.Lambda.TestUtilities/README.md) file for Amazon.Lambda.TestUtilities.

## Blueprints

Blueprints in this repository are .NET Core Lambda functions that can used to get started. In Visual Studio the Blueprints are avalible when creating a new project and selecting the AWS Lambda Project.


### Dotnet CLI Templates

New .NET Core projects can be created with the **dotnet new** command. By 
installing the **Amazon.Lambda.Templates** NuGet package the AWS Lamdba blueprints 
can be created from the **dotnet new** command. To install the template execute the following command:
```
dotnet new -i Amazon.Lambda.Templates::*
```

The ::* on the end of the command indicates the latest version of the NuGet package.

To see a list of the Lambda templates execute **dotnet new lambda --list**

```
> dotnet new lambda --list                                                                                             
Templates                                    Short Name                      Language      Tags                    
----------------------------------------------------------------------------------------------------------------   
Lambda Detect Image Labels                   lambda.DetectImageLabels        [C#]          AWS/Lambda/Function     
Lambda Empty Function                        lambda.EmptyFunction            [C#]          AWS/Lambda/Function     
Lex Book Trip Sample                         lambda.LexBookTripSample        [C#]          AWS/Lambda/Function     
Lambda Simple DynamoDB Function              lambda.DynamoDB                 [C#]          AWS/Lambda/Function     
Lambda Simple Kinesis Firehose Function      lambda.KinesisFirehose          [C#]          AWS/Lambda/Function     
Lambda Simple Kinesis Function               lambda.Kinesis                  [C#]          AWS/Lambda/Function     
Lambda Simple S3 Function                    lambda.S3                       [C#]          AWS/Lambda/Function     
Lambda ASP.NET Core Web API                  lambda.AspNetCoreWebAPI         [C#]          AWS/Lambda/Serverless   
Lambda DynamoDB Blog API                     lambda.DynamoDBBlogAPI          [C#]          AWS/Lambda/Serverless   
Lambda Empty Serverless                      lambda.EmptyServerless          [C#]          AWS/Lambda/Serverless   
Simple Step Functions                        lambda.SimpleStepFunctions      [C#]          AWS/Lambda/Serverless   
```

To get details about a template, you can use the help command.

**dotnet new lambda.EmptyFunction –help**

```
Template Instantiation Commands for .NET Core CLI.                                                                                          
                                                                                                                                           
Lambda Empty Function (C#)                                                                                                                  
Author: AWS                                                                                                                                 
Options:                                                                                                                                    
  -p|--profile  The AWS credentials profile set in aws-lambda-tools-defaults.json and used as the default profile when interacting with AWS.
                string - Optional                                                                                                           
                                                                                                                                           
  -r|--region   The AWS region set in aws-lambda-tools-defaults.json and used as the default region when interacting with AWS.              
                string - Optional  
```

The templates take two optional parameters to set the profile and region. These values are written to the aws-lambda-tools-default.json.

To create a function, run the following command

```
dotnet new lambda.EmptyFunction --name BlogFunction --profile default --region us-east-2
```


### Yeoman (Deprecated)

The Yeoman generators have been deprecated in favor of the new **dotnet new** templates. They will not be migrated from the older project.json based project system.

For developers not using Visual Studio the Blueprints can be used with a Yeoman generator. To use Yeoman, Node.js and npm must be installed which can be obtain from [nodejs.org](https://nodejs.org/en/download/)

Once npm is installed the Yeoman generator can be installed using the following command.
```
npm install -g yo generator-aws-lambda-dotnet
```

To run the generator and select a Blueprint run the following command.
```
yo aws-lambda-dotnet
```

Assuming you are NOT using visual studio and you have installed the latest version of dotnet you will need to migrate from `project.json`-based project to MSBuild/`.csproj` based. To do this, go into the directory containing the `global.json` file and run `dotnet migrate`.  For more information see: https://docs.microsoft.com/en-us/dotnet/core/tools/#migration-from-projectjson

```
cd ProjectName
dotnet migrate
```

After that, you should be able to pull down the dependencies:

```
cd src/ProjectName
dotnet restore
```
