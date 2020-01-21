# AWS Lambda for .NET Core [![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/aws/aws-lambda-dotnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Repository for the AWS NuGet packages and Blueprints to support writing AWS Lambda functions using .NET Core.

For a history of releases view the [release change log](RELEASE.CHANGELOG.md)

## Version Status
Our intention is to keep a regular patch and update cycle to ensure Lambda developers have access to the currently supported patch of each major version of .NET Core.  Given the development and deployment effort, our target is to have most rollouts complete in less than a month.  We do not expect it to be less than one week.  This enables us to ensure high quality deployments.  We will give special attention to any versions that contain security updates critical to .NET Core in AWS Lambda.

The table below shows the currently supported patch of each major version of .NET Core in AWS Lambda, the next version(s) we plan on deploying, and the latest version released by Microsoft.  These are subject to change and we'll keep this table as up-to-date as possible.

|Version|Currently Supported Patch|In Development Queue|Latest Microsoft Release|
|-------|-------------------------|--------------------|------------------------|
|1.0|1.0.13|1.0.16|1.0.16|
|2.0|2.0.9||2.0.9|
|2.1|2.1.13|2.1.15|2.1.15|
|3.1||3.1.1|3.1.1|

## Learning Resources

[Lambda Developer Guide](https://docs.aws.amazon.com/lambda/latest/dg/welcome.html)
  * [Programming Model for Authoring Lambda Functions in C#](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-programming-model.html)
  * [Creating a Deployment Package (C#)](https://docs.aws.amazon.com/lambda/latest/dg/lambda-dotnet-how-to-create-deployment-package.html)

### AWS Blog Posts
* [Developing .NET Core AWS Lambda functions](https://aws.amazon.com/blogs/compute/developing-net-core-aws-lambda-functions/)
* [.NET Core Global Tools for AWS](https://aws.amazon.com/blogs/developer/net-core-global-tools-for-aws/)
  * Important read of users of the dotnet Lambda CLI tool.
* [AWS Lambda .NET Core 2.1 Support Released](https://aws.amazon.com/blogs/developer/aws-lambda-net-core-2-1-support-released/)
  * Contains useful information for migrating .NET Core 2.0 Lambda projects to .NET Core 2.1.
* [F# Tooling Support for AWS Lambda](https://aws.amazon.com/blogs/developer/f-tooling-support-for-aws-lambda/)
* [New AWS X-Ray .NET Core Support](https://aws.amazon.com/blogs/developer/new-aws-x-ray-net-core-support/)
  * Contains information on setting up X-Ray with .NET Core Lambda functions.
* [Serverless ASP.NET Core 2.0 Applications](https://aws.amazon.com/blogs/developer/serverless-asp-net-core-2-0-applications/)

### Community Posts
* [My First AWS Lambda Using .NET Core](http://solutionsbyraymond.com/2018/09/20/my-first-aws-lambda-using-net-core/) By Raymond Sanchez, September 2018
* [Developing .NET Core AWS Lambda functions](https://awscentral.blogspot.com/2018/09/developing-net-core-aws-lambda-functions.html?utm_source=dlvr.it&utm_medium=twitter) By Walker Cabay, June 2018
  * Focuses on debugging and diagnostics as well as using the SAM, serverless application model, cli.
* [Going serverless with .NET Core, AWS Lambda and the Serverless framework](http://josephwoodward.co.uk/2017/11/going-serverless-net-core-aws-lambda-serverless-framework) By Joseph Woodward, November 2017
  * Shows how to use the Serverless framework with .NET Core Lambda.
* [Creating a Serverless Application with .NET Core, AWS Lambda and AWS API Gateway](https://www.jerriepelser.com/blog/dotnet-core-aws-lambda-serverless-application/) By Jerrie Pelser, April 2017
  * Tutorial for building a Web API and **not** using ASP.NET Core.
* [Modular Powershell in AWS Lambda Functions](https://rollingwebsphere.home.blog/2020/01/18/aws-lambda-functions-with-modular-powershell/) By Brian Olson, January 2020
  * Tutorial for using modular powershell functions in AWS Lambda.

### AWS Recorded Talks
* [Building a .NET Serverless Application on AWS](https://www.youtube.com/watch?v=TZUtB1xXduo) By Abby Fuller, Tara Walker and Nicki Klien 2018
  * Demo of a serverless application using the AWS .NET SDK, AWS Lambda, AWS CodeBuild, AWS X-Ray, Amazon Dynamo DB Accelorator (DAX), and the AWS Toolkit for Visual Studio.
* [Serverless Applications with AWS](https://www.youtube.com/watch?v=sgXq5-UGRt8&list=PL03Lrmd9CiGei7clxJEyIIbVTm5NWJPm7) - From NDC Minnesota 2018 by Norm Johanson
  * Description of how .NET Core Lambda works
  * Explain how AWS Lambda scales
  * How to use AWS Step Functions
  * A brief section on using the .NET Lambda tools for CI/CD
* [.NET Serverless Development on AWS](https://www.youtube.com/watch?v=IBeqDaMDjf0) - AWS Online Tech Talks by Norm Johanson 2018
  * Shows how to use both Visual Studio and dotnet CLI tools
  * Create an F# Lambda function
  * How to use X-Ray with Lambda
  * Demonstrate using the `dotnet lambda package-ci` command for CI/CD with AWS Code services. 
* [Containers and Serverless with AWS](https://www.youtube.com/watch?v=TYb-vw6knQ0&list=PL03Lrmd9CiGfprrIjzbjdA2RRShJMzYIM) - From NDC Oslo 2018 By Norm Johanson
  * Compares the serverless and container platforms to help inform deciding which platform to use.
* [How to Deploy .NET Code to AWS from Within Visual Studio](https://www.youtube.com/watch?v=pgRzdZeNxD8) - AWS Online Tech Talks, August 2017

### Community Recorded Talks
* [Create a Serverless .NET Core 2.1 Web API with AWS Lambda](https://www.youtube.com/watch?v=OhEANj3Y6ZQ) By Daniel Donbavand, August 2018
  * Tutorial for building a .NET Lambda Web API.
* [AWS for .NET Developers - AWS Lambda, S3, Rekognition - .NET Concept of the Week - Episode 15](https://www.youtube.com/watch?v=yFbLCqToEYc) By Greg Kalapos, July 2018
  * In this episode we create a "Not Hotdog" clone from Silicon Valley (HBO) called "SchnitzelOrNot" with .NET and AWS. For this we use AWS Lambda with .NET Core, S3, and Amazon Rekognition.

 
## NuGet Packages

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
