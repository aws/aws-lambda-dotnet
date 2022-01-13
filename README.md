# AWS Lambda for .NET Core [![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/aws/aws-lambda-dotnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Repository for the AWS NuGet packages and Blueprints to support writing AWS Lambda functions using .NET Core.

For a history of releases view the [release change log](RELEASE.CHANGELOG.md)

## Table of Contents
- [AWS Lambda for .NET Core ![Gitter](https://gitter.im/aws/aws-lambda-dotnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)](#aws-lambda-for-net-core-img-srchttpsbadgesgitterimjoin20chatsvg-altgitter)
  - [Table of Contents](#table-of-contents)
  - [NuGet Packages](#nuget-packages)
    - [Events](#events)
    - [Amazon.Lambda.Tools](#amazonlambdatools)
      - [Global Tool Migration](#global-tool-migration)
        - [Migrating from DotNetCliToolReference](#migrating-from-dotnetclitoolreference)
    - [Amazon.Lambda.AspNetCoreServer](#amazonlambdaaspnetcoreserver)
    - [Amazon.Lambda.TestUtilities](#amazonlambdatestutilities)
  - [Blueprints](#blueprints)
    - [Dotnet CLI Templates](#dotnet-cli-templates)
    - [Yeoman (Deprecated)](#yeoman-deprecated)
  - [Getting Help](#getting-help)
  - [Feedback and Contributing](#feedback-and-contributing)


## NuGet Packages
This repo contains a number of different tools and libraries to support development of Lambda functions using .NET. These packages have individual README docs outlining specific information for that particular package. These packages are cataloged here.

### Events

This packages in this folder contains classes that can be used as input types for Lambda functions that process various AWS events.

These are the packages and their README.md files:

* [Amazon.Lambda.APIGatewayEvents](Libraries/src/Amazon.Lambda.APIGatewayEvents) - [README.md](Libraries/src/Amazon.Lambda.APIGatewayEvents/README.md)
* [Amazon.Lambda.ApplicationLoadBalancerEvents](Libraries/src/Amazon.Lambda.ApplicationLoadBalancerEvents) - [README.md](Libraries/src/Amazon.Lambda.ApplicationLoadBalancerEvents/README.md)
* [Amazon.Lambda.CloudWatchLogsEvents](Libraries/src/Amazon.Lambda.CloudWatchLogsEvents) - [README.md](Libraries/src/Amazon.Lambda.CloudWatchLogsEvents/README.md)
* [Amazon.Lambda.CognitoEvents](Libraries/src/Amazon.Lambda.CognitoEvents) - [README.md](Libraries/src/Amazon.Lambda.CognitoEvents/README.md)
* [Amazon.Lambda.ConfigEvents](Libraries/src/Amazon.Lambda.ConfigEvents) - [README.md](Libraries/src/Amazon.Lambda.ConfigEvents/README.md)
* [Amazon.Lambda.DynamoDBEvents](Libraries/src/Amazon.Lambda.DynamoDBEvents) - [README.md](Libraries/src/Amazon.Lambda.DynamoDBEvents/README.md)
* [Amazon.Lambda.LexEvents](Libraries/src/Amazon.Lambda.LexEvents) - [README.md](Libraries/src/Amazon.Lambda.LexEvents/README.md)
* [Amazon.Lambda.KinesisAnalyticsEvents](Libraries/src/Amazon.Lambda.KinesisAnalyticsEvents) - [README.md](Libraries/src/Amazon.Lambda.KinesisAnalyticsEvents/README.md)
* [Amazon.Lambda.KinesisEvents](Libraries/src/Amazon.Lambda.KinesisEvents) - [README.md](Libraries/src/Amazon.Lambda.KinesisEvents/README.md)
* [Amazon.Lambda.KinesisFirehoseEvents](Libraries/src/Amazon.Lambda.KinesisFirehoseEvents) - [README.md](Libraries/src/Amazon.Lambda.KinesisFirehoseEvents/README.md)
* [Amazon.Lambda.S3Events](Libraries/src/Amazon.Lambda.S3Events) - [README.md](Libraries/src/Amazon.Lambda.S3Events/README.md)
* [Amazon.Lambda.SimpleEmailEvents](Libraries/src/Amazon.Lambda.SimpleEmailEvents) - [README.md](Libraries/src/Amazon.Lambda.SimpleEmailEvents/README.md)
* [Amazon.Lambda.SNSEvents](Libraries/src/Amazon.Lambda.SNSEvents) - [README.md](Libraries/src/Amazon.Lambda.SNSEvents/README.md)
* [Amazon.Lambda.SQSEvents](Libraries/src/Amazon.Lambda.SQSEvents) - [README.md](Libraries/src/Amazon.Lambda.SQSEvents/README.md)
* [Amazon.Lambda.KafkaEvents](Libraries/src/Amazon.Lambda.KafkaEvents) - [README.md](Libraries/src/Amazon.Lambda.KafkaEvents/README.md)

### Amazon.Lambda.Tools

Package adds commands to the dotnet cli that can be used manage Lambda functions including deploying a function from the dotnet cli. 
For more information see the [README.md](Libraries/src/Amazon.Lambda.Tools/README.md) file for Amazon.Lambda.Tools.

#### Global Tool Migration

As of September 10th, 2018 Amazon.Lambda.Tools has migrated to be .NET Core [Global Tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
As part of the migration the version number was set to 3.0.0.0

To install Amazon.Lambda.Tools use the **dotnet tool install** command.
```
dotnet tool install -g Amazon.Lambda.Tools
```

To update to the latest version of Amazon.Lambda.Tools use the **dotnet tool update** command.
```
dotnet tool update -g Amazon.Lambda.Tools
```

##### Migrating from DotNetCliToolReference

To migrate an existing project away from the older project tool, you need to edit your project file and remove the **DotNetCliToolReference** for the Amazon.Lambda.Tools package. For example, let's look at an existing Lambda project file.
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>

    <-- The new property indicating to AWS Toolkit for Visual Studio this is a Lambda project -->
    <AWSProjectType>Lambda</AWSProjectType>
  </PropertyGroup>
  
  <ItemGroup>
    <-- This line needs to be removed -->
    <DotNetCliToolReference Include="Amazon.Lambda.Tools" Version="2.2.0" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="1.0.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.Json" Version="1.3.0" />
  </ItemGroup>
</Project>
```
To migrate this project, you need to delete the **DotNetCliToolReference** element, including **Amazon.Lambda.Tools**. If you don't remove this line, the older project tool version of **Amazon.Lambda.Tools** will be used instead of an installed Global Tool.

The AWS Toolkit for Visual Studio before .NET Core 2.1 would look for the presence of **Amazon.Lambda.Tools** in the project file to determine whether to show the Lambda deployment menu item. Because we knew we were going to switch to Global Tools, and the reference to **Amazon.Lambda.Tools** in the project was going away, we added the **AWSProjectType** property to the project file. The current version of the AWS Toolkit for Visual Studio now looks for either the presence of **Amazon.Lambda.Tools** or the **AWSProjectType** set to **Lambda**. Make sure when removing the **DotNetCliToolReference** that your project file has the **AWSProjectType** property to continue deploying with the AWS Toolkit for Visual Studio.

### Amazon.Lambda.AspNetCoreServer

Package makes it easy to run ASP.NET Core Web API applications as Lambda functions.
For more information see the [README.md](Libraries/src/Amazon.Lambda.AspNetCoreServer/README.md) file for Amazon.Lambda.AspNetCoreServer.

### Amazon.Lambda.TestUtilities

Package includes test implementation of the interfaces from Amazon.Lambda.Core and helper methods to help in locally testing.
For more information see the [README.md](Libraries/src/Amazon.Lambda.TestUtilities/README.md) file for Amazon.Lambda.TestUtilities.

## Blueprints

Blueprints in this repository are .NET Core Lambda functions that can used to get started. In Visual Studio the Blueprints are available when creating a new project and selecting the AWS Lambda Project.


### Dotnet CLI Templates

New .NET Core projects can be created with the **dotnet new** command. By 
installing the **Amazon.Lambda.Templates** NuGet package the AWS Lamdba blueprints 
can be created from the **dotnet new** command. To install the template execute the following command:
```
dotnet new -i "Amazon.Lambda.Templates::*"
```

The ::* on the end of the command indicates the latest version of the NuGet package.

To see a list of the Lambda templates execute **dotnet new lambda --list**

```
> dotnet new lambda --list                                                                                             
Templates                                                 Short Name                              Language          Tags

---------------------------------------------------------------------------------------------------------------------------------------------------------
Order Flowers Chatbot Tutorial                            lambda.OrderFlowersChatbot              [C#]              AWS/Lambda/Function

Lambda Detect Image Labels                                lambda.DetectImageLabels                [C#], F#          AWS/Lambda/Function

Lambda Empty Function                                     lambda.EmptyFunction                    [C#], F#          AWS/Lambda/Function

Lex Book Trip Sample                                      lambda.LexBookTripSample                [C#]              AWS/Lambda/Function

Lambda Simple DynamoDB Function                           lambda.DynamoDB                         [C#], F#          AWS/Lambda/Function

Lambda Simple Kinesis Firehose Function                   lambda.KinesisFirehose                  [C#]              AWS/Lambda/Function

Lambda Simple Kinesis Function                            lambda.Kinesis                          [C#], F#          AWS/Lambda/Function

Lambda Simple S3 Function                                 lambda.S3                               [C#], F#          AWS/Lambda/Function

Lambda Simple SQS Function                                lambda.SQS                              [C#]              AWS/Lambda/Function

Lambda ASP.NET Core Web API                               serverless.AspNetCoreWebAPI             [C#], F#          AWS/Lambda/Serverless

Lambda ASP.NET Core Web Application with Razor Pages      serverless.AspNetCoreWebApp             [C#]              AWS/Lambda/Serverless

Serverless Detect Image Labels                            serverless.DetectImageLabels            [C#], F#          AWS/Lambda/Serverless

Lambda DynamoDB Blog API                                  serverless.DynamoDBBlogAPI              [C#]              AWS/Lambda/Serverless

Lambda Empty Serverless                                   serverless.EmptyServerless              [C#], F#          AWS/Lambda/Serverless

Lambda Giraffe Web App                                    serverless.Giraffe                      F#                AWS/Lambda/Serverless

Serverless Simple S3 Function                             serverless.S3                           [C#], F#          AWS/Lambda/Serverless

Step Functions Hello World                                serverless.StepFunctionsHelloWorld      [C#], F#          AWS/Lambda/Serverless
```

To get details about a template, you can use the help command.

**dotnet new lambda.EmptyFunction --help**

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

## Getting Help
To learn more about the various packages in this repo, please reference our [Learning Resources](./Docs/Learning_Resources.md) document. In particular, please be sure to read through the official [Lambda Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html).

If those resources are not sufficient to answer your question or resolve your issue, please feel free to open an [issue](https://github.com/aws/aws-lambda-dotnet/issues/new/choose) on this repo.

## Feedback and Contributing
We welcome community contributions to our codebase and tools! If you would like to contribute, please read through the [CONTRIBUTING.md](./CONTRIBUTING.md) document (our contribution guide) and check issues and open pull requests to ensure that the fix/feature you want to contribute is not already in development.
