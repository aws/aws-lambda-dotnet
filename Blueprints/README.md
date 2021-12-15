# AWS Lambda .NET Blueprints

Blueprints are used for setting up new .NET Core projects for AWS Lambda. They are defined in a generic form 
so that they can be exposed through Visual Studio or the **dotnet** CLI with the command `dotnet new`. 


## Definitions

The blueprints are defined in sub directories under the BlueprintDefinitions directory. There is a separate sub folder for 
each version of Visual Studio that contains Lambda blueprints. 

For each blueprint there is a blueprint-manifest.json file containing the metadata for the blueprint and a src and test directory.
It is required that each blueprint must contain a test project.

## Packaging

The .NET Core console application BlueprintPackager is used to package up the blueprints for both Visual Studio and Yeoman.
The console application can be run by executing `dotnet run` in the project directory.

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
