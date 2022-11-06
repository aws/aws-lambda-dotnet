# AWS Lambda Native AOT Project

This starter project consists of:
* Function.cs - contains a class with a `Main` method that starts the bootstrap and a single function handler method.
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS.
* rd.xml - Runtime directives configuration file used to tell the Native AOT compiler what code to not trim out of .NET assemblies.

You may also have a test project depending on the options selected.

The `Main` function is called once during the Lambda init phase. It initializes the .NET Lambda runtime client passing in the function 
handler to invoke for each Lambda event and the JSON serializer to use for converting Lambda JSON format to the .NET types. 

The function handler is a simple method accepting a string argument that returns the uppercase equivalent of the input string. Replace the body of this method and its parameters to suit your needs.

## Native AOT

Native AOT is a feature of .NET 7 that compiles .NET assemblies into a single native executable. By using the native executable the .NET runtime 
is not required to be installed on the target platform. Native AOT can significantly improve Lambda cold starts for .NET Lambda functions. 
This project enables Native AOT by setting the .NET `PublishAot` property in the .NET project file to `true`. The `StripSymbols` property is also
set to `true` to strip debugging symbols from the deployed executable to reduce the executable's size.

### Building Native AOT

When publishing with Native AOT the build OS and Architecture must match the target platform that the application will run. For AWS Lambda that target
platform is Amazon Linux 2. The AWS tooling for Lambda like the AWS Toolkit for Visual Studio, .NET Global Tool Amazon.Lambda.Tools and SAM CLI will 
perform a container build using a .NET 7 Amazon Linux 2 build image when `PublishAot` is set to `true`. This means **docker is a requirement**
when packaging .NET Native AOT Lambda functions on non-Amazon Linux 2 build environments. To install docker go to https://www.docker.com/.

Due to an incompatibility with Amazon Linux 2 and .NET 7 ARM support it is not possible to build and deploy ARM based Native AOT Lambda functions.
Since the architectures need to match between the build and target environments for .NET Native AOT Lambda functions it is not currently possible
to deploy Native AOT Lambda functions from an M based Mac.

### Trimming

As part of the Native AOT compilation, .NET assemblies will be trimmed removing types and methods that the compiler does not find a reference to. This is important
to keep the native executable size small. When types are used through reflection this can go undetected by the compiler causing necessary types and methods to 
be removed. The `rd.xml` file in the project is used to provide additional configuration to the compiler about what types are used to prevent them from 
being trimmed. When testing Native AOT Lambda functions in Lambda if a runtime error occurs about missing types or methods the most likely solution will
be to update the `rd.xml` to not trim the type. 

Currently the AWS SDK for .NET does not support trimming and when used should be added to the rd.xml file. For example here is a `rd.xml` file that excludes
the AWS SDK's core and AWS DynamoDB package.
```xml
<Directives xmlns="http://schemas.microsoft.com/netfx/2013/01/metadata">
	<Application>
		<Assembly Name="AWSSDK.Core" Dynamic="Required All">
		</Assembly>
		<Assembly Name="AWSSDK.DynamoDBv2" Dynamic="Required All">
		</Assembly>
		<Assembly Name="bootstrap" Dynamic="Required All">
		</Assembly>
	</Application>
</Directives>
```

For informaton about the `rd.xml` checkout the runtime directives configuration file reference: https://learn.microsoft.com/en-us/windows/uwp/dotnet-native/runtime-directives-rd-xml-configuration-file-reference

## Docker requirement

Docker is required to be installed and running when building .NET Native AOT Lambda functions on any platform besides Amazon Linux 2. Information on how acquire Docker can be found here: https://docs.docker.com/get-docker/

## Here are some steps to follow from Visual Studio:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your template and code you can deploy your application using the [Amazon.Lambda.Tools Global Tool](https://github.com/aws/aws-extensions-for-dotnet-cli#aws-lambda-amazonlambdatools) from the command line.  Version 5.6.0
or later is required to deploy this project.

Install Amazon.Lambda.Tools Global Tools if not already installed.
```
    dotnet tool install -g Amazon.Lambda.Tools
```

If already installed check if new version is available.
```
    dotnet tool update -g Amazon.Lambda.Tools
```

Execute unit tests
```
    cd "BlueprintBaseName.1/test/BlueprintBaseName.1.Tests"
    dotnet test
```

Deploy function to AWS Lambda
```
    cd "BlueprintBaseName.1/src/BlueprintBaseName.1"
    dotnet lambda deploy-function
```


## Arm64

.NET 7 ARM requires a newer version of GLIBC than is available in the `provided.al2` Lambda runtime. .NET 7 functions that are deployed using
the Arm64 architecture will fail to start with a runtime error stating the GLIBC version is below the required version for .NET 7.

