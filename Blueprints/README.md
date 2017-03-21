# AWS Lambda .NET Blueprints

Blueprints are used for setting up new .NET Core projects for AWS Lambda. They are defined in a generic form 
so that they can be exposed through Visual Studio or the **dotnet** CLI with the command `dotnet new`. 


## Definitions

The blueprints are defined in sub directories under the BlueprintDefinitions/Msbuild directory for new msbuild project system. 
The older project.json based blueprints are in sub directories under BlueprintDefinitions/ProjectJson.

For each blueprint there is a blueprint-manifest.json file containing the metadata for the blueprint and a src and test directory.
It is required that each blueprint must contain a test project.

## Packaging

The .NET Core console application BlueprintPackager is used to package up the blueprints for both Visual Studio and Yeoman.
The console application can be run by executing `dotnet run` in the project directory.

## Visual Studio

The BlueprintPackager will write the blueprints to the ../Deployment/Blueprints/VisualStudioBlueprintsMsbuild for VS 2017 
and  ../Deployment/Blueprints/VisualStudioBlueprintsProjectJson for VS 2015.
You can test how your blueprints work in Visual Studio by copying the 
../Deployment/Blueprints/VisualStudioBlueprintsMsbuild to C:\Program Files (x86)\AWS Tools\HostedFiles\LambdaSampleFunctions\NETCore\msbuild-v1
and 
../Deployment/Blueprints/VisualStudioBlueprintsProjectJson to C:\Program Files (x86)\AWS Tools\HostedFiles\LambdaSampleFunctions\NETCore\v1.
After copying the directories point AWS Toolkit for Visual Studio to C:\Program Files (x86)\AWS Tools\HostedFiles
to get its metadata. To update the toolkit open Visual Studio's Options dialog from the Tools menu, select AWS Toolkit and select 
"Use local file system location". Note: Currently the C:\Program Files (x86)\AWS Tools\HostedFiles is only created when you
install the toolkit from the installer for Visual Studio 2015 Toolkit.

## Creating Projects from "dotnet new"

The blueprints are also packaged up into the NuGet package [Amazon.Lambda.Templates](https://www.nuget.org/packages/Amazon.Lambda.Templates/)
and can be installed into the dotnet CLI by running the following command:

`dotnet new -i Amazon.Lambda.Templates::*`

Once that is complete the list of available projects types can be seen by running the following command.

`dotnet new -all`

```
Templates                            Short Name                    Language      Tags
------------------------------------------------------------------------------------------------------
Lambda Detect Image Labels           lambda.DetectImageLabels      [C#]          AWS/Lambda/Function
Lambda Empty Function                lambda.EmptyFunction          [C#]          AWS/Lambda/Function
Lambda Simple DynamoDB Function      lambda.DynamoDB               [C#]          AWS/Lambda/Function
Lambda Simple Kinesis Function       lambda.Kinesis                [C#]          AWS/Lambda/Function
Lambda Simple S3 Function            lambda.S3                     [C#]          AWS/Lambda/Function
Lambda ASP.NET Core Web API          lambda.AspNetCoreWebAPI       [C#]          AWS/Lambda/Serverless
Lambda DynamoDB Blog API             lambda.DynamoDBBlogAPI        [C#]          AWS/Lambda/Serverless
Lambda Empty Serverless              lambda.EmptyServerless        [C#]          AWS/Lambda/Serverless
Console Application                  console                       [C#], F#      Common/Console
Class library                        classlib                      [C#], F#      Common/Library
Unit Test Project                    mstest                        [C#], F#      Test/MSTest
xUnit Test Project                   xunit                         [C#], F#      Test/xUnit
ASP.NET Core Empty                   web                           [C#]          Web/Empty
ASP.NET Core Web App                 mvc                           [C#], F#      Web/MVC
ASP.NET Core Web API                 webapi                        [C#]          Web/WebAPI
Nuget Config                         nugetconfig                                 Config
Web Config                           webconfig                                   Config
Solution File                        sln                                         Solution
```

To get help on a project template with `dotnet new` run the following:

`dotnet new lambda.EmptyFunction --help`

```
Template Instantiation Commands for .NET Core CLI.

Usage: dotnet new [arguments] [options]

Arguments:
  template  The template to instantiate.

Options:
  -l|--list         List templates containing the specified name.
  -lang|--language  Specifies the language of the template to create
  -n|--name         The name for the output being created. If no name is specified, the name of the current directory is used.
  -o|--output       Location to place the generated output.
  -h|--help         Displays help for this command.
  -all|--show-all   Shows all templates


Lambda Empty Function (C#)
Author: AWS
Options:
  -p|--profile  The AWS credentials profile set in aws-lambda-tools-defaults.json and used as the default profile when interacting with AWS.
                string - Optional

  -r|--region   The AWS region set in aws-lambda-tools-defaults.json and used as the default region when interacting with AWS.
                string - Optional
```

To create a project run the following command:

```
dotnet new lambda.EmptyFunction --name ExampleFunction --profile default --region us-east-2
```


## Yeoman (Deprecated)

When the blueprints were initially released they were also accessible through a [Yeoman](http://yeoman.io/). 
When the .NET Core tools reached their GA status `dotnet new` had extensibility added to which made for a 
better experience for creating the blueprints outside of Visual Studio. The Yeoman generator still exists and 
can be used for users that are still developing with the older project.json formatted projects.


To use the blueprints with Yeoman you must first install [npm](https://nodejs.org/en/) which is part of the Node.js 
install. Once npm is installed you can install Yeoman by running the following command.

```
npm install -g yo
```

To install the current distributed version of the AWS Lambda .NET Core blueprints run the following command.

```
npm install -g generator-aws-lambda-dotnet
```

When the BlueprintPackager runs it will copy the Yeoman generator to ../Deployment/Blueprints/generator-aws-lambda-dotnet.
To use these blueprints instead of the distributed version execute the command `npm link` in the directory. To switch 
back to the distributed version execute the command `npm unlink`.

To run the Yeoman generator which will allow you to pick a blueprint run the following command.
```
yo aws-lambda-dotnet
```