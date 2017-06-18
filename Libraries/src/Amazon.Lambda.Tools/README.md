# Amazon.Lambda.Tools

This package adds commands to the .NET Core CLI that can be used to manage AWS Lambda functions including deploying a function from the CLI. The
[AWS Toolkit for Visual Studio](https://aws.amazon.com/visualstudio/) also uses Amazon.Lambda.Tools to deploy Lambda functions.

### Adding the Package

To add Amazon.Lambda.Tools to a msbuild based project edit the csproj file by adding the following section. Update the version filled to the latest released version.

```xml
<ItemGroup>
	<DotNetCliToolReference Include="Amazon.Lambda.Tools" Version="1.6.0" />
</ItemGroup>
```

#### Project.json based projects (Visual Studio 2015)
To add Amazon.Lambda.Tools to a project.json based project it must be set in both the dependencies and tools section of the project.json file. For example:
```json
{
  "version": "1.0.0-*",

  "dependencies": {
    "Microsoft.NETCore.App": {
      "type": "platform",
      "version": "1.0.0"
    },

 
    "Amazon.Lambda.Tools" : {
      "type" :"build",
      "version":"1.5.0"
    }
  },

  "tools": {
    "Amazon.Lambda.Tools" : "1.6.0"
  },

  "frameworks": {
    "netcoreapp1.0": {
      "imports": "dnxcore50"
    }
  }
}
```
Notice that Amazon.Lambda.Tools is added as a dependency of type *build* which means it will not be added to the project's deployment artifacts when running the **dotnet publish** command.

To use Amazon.Lambda.Tools to deploy a function run the following command from the directory containing the project.json file:
```
dotnet lambda deploy-function
```
The following commands are supported for working with AWS Lambda functions:
* deploy-function - Runs **dotnet publish** and deploys the packaged output zip file
* invoke-function - Invokes the Lambda function running in the service and displays the results
* delete-function - Deletes the Lambda function
* list-functions - Lists the created Lambda functions
* get-function-config - Gets the current configuration for a Lambda function
* update-function-config - Updates the configuration for a Lambda function

To use Amazon.Lambda.Tools to deploy an AWS Serverless Application run the following command from the directory containing the project.json file:
```
dotnet lambda deploy-serverless
```

The following commands are supported for working with AWS Serverless Applications:
* deploy-serverless - Runs **dotnet publish** and deploys the packaged output zip file to Amazon S3 and the serverless template file to AWS CloudFormation
* list-serverless - Lists the created Serverless applications
* delete-serverless - Deletes the Serverless application

To use Amazon.Lambda.Tools to create a packaged zip file for use with continuous integration (the content is packaged but not deployed) run the following command from the directory containing the project.json file:
```
dotnet lambda package
```

The following commands are supported for working with continuous integration:
* package - Runs **dotnet publish** to create a packaged output zip file but does not deploy the package

To get more help on how to use Amazon.Lambda.Tools use the help command.
```
dotnet help lambda

or

dotnet lambda help deploy-function
```

## Manage Command Arguments with aws-lambda-tools-defaults.json

The aws-lambda-tools-defaults.json file can be used to simplify the use of Amazon.Lambda.Tools. Amazon.Lambda.Tools will
search for this file in the project root and use the settings present in the file as default values for the
command line arguments. For example with the following file
```json
{
  "profile":"default",
  "region" : "us-west-2",
  "configuration" : "Release",
  "framework" : "netcoreapp1.0",
  "function-runtime":"dotnetcore1.0",
  "function-memory-size" : 256,
  "function-timeout" : 30,
  "function-handler" : "EmptyFunction::EmptyFunction.Function::FunctionHandler"
}
```
the only argument left to be set when executing **dotnet lambda deploy-function** for a new function is the function name and IAM Role.

When redeploying to an existing function or executing **dotnet lambda update-function-config** only profile, region, framework and configuration are 
used from this file to avoid accidentally changing any configuration settings on the function.

When using the [AWS Toolkit for Visual Studio](https://aws.amazon.com/visualstudio/) this file is also used to
prefill the deployment dialog. Blueprints used to create Lambda projects will create this file automatically and populate it with some default values.
