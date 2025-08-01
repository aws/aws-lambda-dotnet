## Release 2025-08-01

### Amazon.Lambda.RuntimeSupport (1.13.2)
* Fix issue making HTTP header comparisons be case insensitive

## Release 2025-07-15

### Amazon.Lambda.TestTool.BlazorTester (0.16.3)
* Update to version 2.7.0 of Amazon.Lambda.Core
### Amazon.Lambda.Templates (7.5.0)
* Update package references including AWS SDK for .NET version to V4

## Release 2025-07-14 #2

### Amazon.Lambda.TestTool (0.11.1)
* Fix issue causing sample requests to not be applied if a config path is not supplied
### Amazon.Lambda.AspNetCoreServer (9.2.0)
* Update Amazon.Lambda.Logging.AspNetCore dependency
### Amazon.Lambda.AspNetCoreServer.Hosting (1.9.0)
* Update Amazon.Lambda.Logging.AspNetCore dependency
### Amazon.Lambda.Core (2.7.0)
* Added log level with exception version of the static logging functions on Amazon.Lambda.Core.LambdaLogger
### Amazon.Lambda.Logging.AspNetCore (4.1.0)
* Convert logging parameters into JSON properties when Lambda log format is configured as JSON

## Release 2025-06-25

### Amazon.Lambda.TestTool (0.11.0)
* Add the ability to save requests and hide UI elements

## Release 2025-06-03

### Amazon.Lambda.RuntimeSupport (1.13.1)
* Add support for parameterized logging method with exception to global logger LambdaLogger in Amazon.Lambda.Core
### Amazon.Lambda.Core (2.6.0)
* Added log level version of the static logging functions on Amazon.Lambda.Core.LambdaLogger
### Amazon.Lambda.AspNetCoreServer (9.1.2)
* Update Amazon.Lambda.Logging.AspNetCore dependency
### Amazon.Lambda.AspNetCoreServer.Hosting (1.8.2)
* Update Amazon.Lambda.Logging.AspNetCore dependency
### Amazon.Lambda.Logging.AspNetCore (4.0.0)
* Add support Lambda log levels
* Change build target from .NET Standard 2.0 to .NET 6 and NET 8 to match Amazon.Lambda.AspNetCoreServer
### Amazon.Lambda.TestUtilities (3.0.0)
* Update Amazon.Lambda.TestUtitlies to have implementation of the newer logging methods
* Change build target from .NET Standard 2.0 to .NET 6 and NET 8 to match Amazon.Lambda.AspNetCoreServer

## Release 2025-05-15

### Amazon.Lambda.TestTool (0.10.3)
* Fix issue with long debugging sessions causing events to be retried
### Amazon.Lambda.AspNetCoreServer (9.1.1)
* Fixed issue when executing AddRegisterBeforeSnapshot callbacks to ensure each invocation has a new ASP.NET Core feature collection and HttpContext.
### Amazon.Lambda.AspNetCoreServer.Hosting (1.8.1)
* Update Amazon.Lambda.AspNetCoreServer dependency for latest patch fix.

## Release 2025-04-29

### Amazon.Lambda.TestTool (0.10.2)
* Add SSO dependencies to support credential profiles that are using SSO
* Update version of AWS SDK for .NET to V4
### Amazon.Lambda.DynamoDBEvents.SDK.Convertor (2.0.0)
* Update to AWS SDK for .NET V4
### Amazon.Lambda.KinesisEvents (3.0.0)
* Update to AWS SDK for .NET V4

## Release 2025-04-25

### Amazon.Lambda.AspNetCoreServer (9.1.0)
* Add overrideable method GetBeforeSnapshotRequests() and AddAWSLambdaBeforeSnapshotRequest() extension method to support warming up the asp.net/lambda pipelines automatically during BeforeSnapshot callback.
### Amazon.Lambda.AspNetCoreServer.Hosting (1.8.0)
* Add overrideable method GetBeforeSnapshotRequests() and AddAWSLambdaBeforeSnapshotRequest() extension method to support warming up the asp.net/lambda pipelines automatically during BeforeSnapshot callback.
### Amazon.Lambda.AppSyncEvents (1.0.0)
* Added AppSyncResolverEvent to support direct lambda resolver

## Release 2025-04-10 #2

### Amazon.Lambda.TestTool (0.10.1)
* Support optional SQS client region in event source test tool
### Amazon.Lambda.DynamoDBEvents.SDK.Convertor (1.0.0)
* Implement DynamoDBEvents SDK Convertor package for DynamoDBEvent images to DynamoDBv2.Document

## Release 2025-04-04

### Amazon.Lambda.Templates (7.4.1)
* Update the AWS Message Processing Framework for .NET project template.
### Amazon.Lambda.TestTool (0.10.0)
* Add SQS event source support

## Release 2025-03-13

### Amazon.Lambda.Annotations (1.7.0)
* Add ConfigureHostBuilder function to `Startup` class for allowing the Lambda function to configure an `IHostApplicationBuilder`.
### Amazon.Lambda.RuntimeSupport (1.13.0)
* Add support for parameterized logging method to global logger LambdaLogger in Amazon.Lambda.Core
### Amazon.Lambda.Core (2.5.1)
* Add support for parameterized logging method to global logger LambdaLogger. Method is marked as preview till new version of Amazon.Lambda.RuntimeSupport is deployed to managed runtime.

## Release 2025-03-05

### Amazon.Lambda.TestTool (0.9.1)
* Add Lambda-Runtime-Deadline-Ms response header

## Release 2025-02-26 #3

### Amazon.Lambda.Annotations (1.6.3)
* fix: implemented change to always use __ as prefix and suffix for parameter names generated for a LambdaFunction.

## Release 2025-02-26 #2

### Amazon.Lambda.TestTool (0.9.0)
* Add README
* Add 6MB request and response size validation.

## Release 2025-02-25

### Amazon.Lambda.AspNetCoreServer (9.0.4)
* Fixed issue with marking the response body started. This is required for writing trailing headers
### Amazon.Lambda.AspNetCoreServer.Hosting (1.7.4)
* Update Amazon.Lambda.AspNetCoreServer dependency to latest.
### Amazon.Lambda.RuntimeSupport (1.12.3)
* Add ability to specify configuration using command line args

## Release 2025-02-20

### Amazon.Lambda.TestTool.BlazorTester (0.16.2)
* Fixed yaml function-based serverless.template Environment Variable parsing and adding to functioninfo
### Amazon.Lambda.TestTool (0.0.3)
* Add missing icon for NuGet package

## Release 2025-02-07

### Amazon.Lambda.TestTool (0.0.2-preview)
* Update default log level to be ERROR in production and INFORMATION in debug mode.
* Fix exception method when not setting --api-gateway-emulator-mode
* Breaking change: Switch to use commands to invoke the tool. For example to run the Lambda emulator use the command 'dotnet lambda-test-tool start --lambda-emulator-port 5050'
* Add new info command to get metadata about the tool. For example getting the version number of the tool.

## Release 2025-01-31 #2

### Amazon.Lambda.TestTool.BlazorTester (0.16.1)
* Update AWS dependencies. This address issues when attempting to use SnapStart APIs from Amazon.Lambda.Core in Lambda functions.

## Release 2025-01-31

### Amazon.Lambda.TestTool (0.0.1-preview)
* Initial preview release

## Release 2024-12-09

### SnapshotRestore.Registry (1.0.1)
* Added License URL to project
* Updated project to support building the .NET 9 Lambda custom runtime image
### Amazon.Lambda.RuntimeSupport (1.12.2)
* Updated project to support building the .NET 9 Lambda custom runtime image
### Amazon.Lambda.Annotations (1.6.2)
* Added License URL to project

## Release 2024-11-26

### Amazon.Lambda.AspNetCoreServer (9.0.3)
* Fixed issue '(EmptyBodyBehavior = EmptyBodyBehavior.Allow)' not being honored when request body was empty
### Amazon.Lambda.RuntimeSupport (1.12.1)
* Revert behavior for non SnapShot scenario to throw an exception when initialization fails
### Amazon.Lambda.AspNetCoreServer.Hosting (1.7.3)
* Fixed issue '(EmptyBodyBehavior = EmptyBodyBehavior.Allow)' not being honored when request body was empty

## Release 2024-11-25

### Amazon.Lambda.Templates (7.4.0)
* Update package dependencies. The significant dependency update was for Amazon.Lambda.Core and Amazon.Lambda.RuntimeSupport with added support for Lambda SnapStart.

## Release 2024-11-20

### Amazon.Lambda.PowerShellHost (3.0.2)
* Update to latest version of Amazon.Lambda.Core
### Amazon.Lambda.Logging.AspNetCore (3.1.1)
* Update to latest version of Amazon.Lambda.Core
### Amazon.Lambda.Annotations (1.6.1)
* Update to latest version of Amazon.Lambda.Core
### Amazon.Lambda.AspNetCoreServer.Hosting (1.7.2)
* Update to latest version of Amazon.Lambda.Core and Amazon.Lambda.RuntimeSupport
### Amazon.Lambda.AspNetCoreServer (9.0.2)
* Update to latest version of Amazon.Lambda.Core

## Release 2024-11-18

### Amazon.Lambda.Core (2.5.0)
* Added the new `SnapshotRestore` static class for registering SnapStart hooks for before snapshot and after restore.

### Amazon.Lambda.RuntimeSupport (1.12.0)
* Added support for handling Lambda SnapStart events.

### SnapshotRestore.Registry (1.0.0)
* New package used by Amazon.Lambda.RuntimeSupport for registering and executing SnapStart hooks.

## Release 2024-11-14

### Amazon.Lambda.TestTool.BlazorTester (0.16.0)
* Update Lambda Test Tool to add a .NET9 target
* Fixed issue supporting parameterized logging APIs added to Amazon.Lambda.Core in version 2.4.0

## Release 2024-11-13

### Amazon.Lambda.Templates (7.3.0)
* Update package Dependencies.
* Custom Runtime templates target .NET 9.
* Rework the `EntryPoint` for F# test projects.

## Release 2024-11-12

### Amazon.Lambda.Annotations (1.6.0)
* Eagerly build Function Handler inside the Generated class's constructor

## Release 2024-11-06

### Amazon.Lambda.Core (2.4.0)
* Removed RequiresPreviewFeatures attribute from parameterized logging APIs

## Release 2024-11-01

### Amazon.Lambda.TestTool.BlazorTester (0.15.3)
* Fixed an issue where delimited string and primitive values in payload were not parsed properly by Lambda Test Tool.

## Release 2024-10-30

### Amazon.Lambda.Annotations (1.5.3)
* Added a .NET 8 target with support for Trimming
* Updated package description to remove "preview" text

## Release 2024-10-22

### Amazon.Lambda.Serialization.Json (2.2.4)
* Fixed null pointer exception when enabling debug mode with LAMBDA_NET_SERIALIZER_DEBUG environment variable and function response is null.
### Amazon.Lambda.Serialization.SystemTextJson (2.4.4)
* Fixed null pointer exception when enabling debug mode with LAMBDA_NET_SERIALIZER_DEBUG environment variable and function response is null.
### AWSLambdaPSCore PowerShell Module (4.0.4.0)
* Removed reference to global variable $Name when packaging PowerShell script as Lambda function.

## Release 2024-10-15

### AWSLambdaPSCore PowerShell Module (4.0.3.0)
* Update default version of Microsoft.PowerShell.SDK to 7.4.5

## Release 2024-09-30

### AWSLambdaPSCore PowerShell Module (4.0.2.0)
* Update PowerShell Lambda blueprints to reference the latest Amazon.Lambda.PowerShellHost and Amazon.Lambda.Core packages.

## Release 2024-09-29

### Amazon.Lambda.Annotations (1.5.2)
* Fixed issue with handling unicode characters when converting FromBody parameters from JSON to .NET type.

## Release 2024-09-25

### Amazon.Lambda.Annotations (1.5.1)
* Added a 127 character limit on the Lambda function handler when the package type is set to zip.
### Amazon.Lambda.PowerShellHost (3.0.1)
* Fixed an issue where Write-Debug doesn't log debug messages to CloudWatch for a Lambda PowerShell function.

## Release 2024-09-05

### Amazon.Lambda.RuntimeSupport (1.11.0)
* Add support for structured logging. Access to structured logging is pending a deployment of Amazon.Lambda.RuntimeSupport to the managed runtime. Follow GitHub issue https://github.com/aws/aws-lambda-dotnet/issues/1747 for status updates on deployment.
### Amazon.Lambda.Core (2.3.0)
* Add preview parameterized logging APIs to the ILogger interface. The API will be in preview till the completion of Amazon.Lambda.RuntimeSupport being deployed to the managed runtime.
### Amazon.Lambda.AspNetCoreServer (9.0.1)
* Add application/wasm to list of content types that should be base 64 encoded.
### Amazon.Lambda.AspNetCoreServer.Hosting (1.7.1)
* Updated reference of Amazon.Lambda.AspNetCoreServer to 9.0.1
### Amazon.Lambda.CognitoEvents (4.0.0)
* **Breaking Change** Corrected the data type for ClaimsToAddOrOverride property in IdTokenGeneration and AccessTokenGeneration classes for CognitoPreTokenGenerationV2Event.

## Release 2024-08-14

### Amazon.Lambda.Serialization.Json (2.2.3)
* Fixed an issue in `JsonSerializer` where `JsonSerializerSettings.NullValueHandling` was being set to `NullValueHandling.Ignore` after custom settings were applied.
* Added new `JsonIncludeNullValueSerializer` derived from `JsonSerializer` that includes null values in serialized JSON.

## Release 2024-08-01

### Amazon.Lambda.Serialization.Json (2.2.2)
* Fixed `AwsResolver.CreateProperties()` to use logical `OR` condition when checking for `Amazon.Lambda.CloudWatchEvents.CloudWatchEvent` type.
* Initialized `JsonSerializerSettings.NullValueHandling` to `NullValueHandling.Ignore` for `JsonSerializer`.
### Amazon.Lambda.APIGatewayEvents (2.7.1)
* Added support for `Condition` element in `APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement`.

## Release 2024-06-21

### Amazon.Lambda.Templates (7.2.0)
* Updated Amazon.Lambda.Annotations dependency to 1.5.0 in the Message Processing Framework template. Added the `SQSEvent` attribute to set up SQS event source mapping for the message handler Lambda function.

## Release 2024-06-17

### Amazon.Lambda.Annotations (1.5.0)
* Added new .NET attribute to create SQS event source mapping for Lambda functions.

## Release 2024-05-16

### Amazon.Lambda.Annotations (1.4.0)
* Updated the source generator to add documentation on public members in the generated files.
* Added the "\<auto-generated/\>" top-level comment on the generated files to skip them from being processed by analyzers.
* Use the `ILambdaSerializer` to serialize the response body when returning an `IHttpResult`.
  * **BREAKING CHANGE**: Removed the `JsonContext` property in the `HttpResultSerializationOptions` class and replaced it with an `ILambdaSerializer`

## Release 2024-04-25

### Amazon.Lambda.Annotations (1.3.1)
* Update User-Agent string
### Amazon.Lambda.TestTool (0.15.2)
* Update User-Agent string
### AWSLambdaPSCore PowerShell Module (4.0.1.0)
* Update User-Agent string
### Amazon.Lambda.Serialization.SystemTextJson (2.4.3)
* Pull Request [#1736](https://github.com/aws/aws-lambda-dotnet/pull/1736) Update ByteArrayConverter to support base64 deserialization of byte[]. Thanks [brendonparker](https://github.com/brendonparker)

## Release 2024-04-11

### Amazon.Lambda.Templates (7.1.1)
* Update templates to use Amazon.Lambda.Annotations v1.3.0

## Release 2024-04-05

### Amazon.Lambda.Annotations (1.3.0)
* Update default memory size from 256 MB to 512 MB.
* Use JSON serializer extracted from the DI container to serialize error responses.
### Amazon.Lambda.Serialization.Json (2.2.1)
* Correctly handle Lambda events with dates set as Unix epoch in milliseconds.
### Amazon.Lambda.Serialization.SystemTextJson (2.4.2)
* Correctly handle Lambda events with dates set as Unix epoch in milliseconds.

## Release 2024-03-27

### Amazon.Lambda.Templates (7.1.0)
* Add new template using the AWS Message Processing Framework for .NET
* Update versions of AWS.Lambda.Powertools dependencies in relevant templates

## Release 2024-03-12

### Amazon.Lambda.DynamoDBEvents (3.1.1)
* Better handle empty `AttributeValue` objects when serializing `DynamoDBEvent` objects to JSON.

## Release 2024-03-04

### Amazon.Lambda.DynamoDBEvents (3.1.0)
* Added `ToJson` which can be used to convert `DynamoDBEvent` objects to the higher-level document and object persistence classes in the AWS SDK.

## Release 2024-02-22

### Amazon.Lambda.Templates (7.0.0)
* Updated templates to target .NET 8
* Updated the serverless Native AOT template to use Amazon.Lambda.Anotations
* Updated the DynamoDB blueprint for changes with the new major version bump of Amazon.Lambda.DynamoDBEvents
* Updated all AWS dependencies in templates
### AWSLambdaPSCore PowerShell Module (4.0.0)
* Updated to target the .NET 8 Lambda runtime
### Amazon.Lambda.PowerShellHost (3.0.0)
* Updated to target .NET 8
  
## Release 2024-02-15

### Amazon.Lambda.Annotations (1.2.0)
* Added support for detecting TargetFramework is .NET 8 and configuring the runtime to dotnet8.
### Amazon.Lambda.AspNetCoreServer (9.0.0)
* Breaking Change: Removed support for .NET Core 3.1.
* Breaking Change: Removed AbstractAspNetCoreFunction.CreateWebHostBuilder method that was overloaded for .NET Core 3.1 and earlier functions for configuring IWebHostBuilder.
* Added .NET 8 target.
* Addressed trim warnings and marked assembly as trimmable for Native AOT Lambda functions;
### Amazon.Lambda.AspNetCoreServer.Hosting (1.7.0)  
* Update version of Amazon.Lambda.AspNetCoreServer to 9.0.0.
* Added new AddAWSLambdaHosting overload that has a parameter for SourceGeneratorLambdaJsonSerializer<T> to force source generator serialization.
* Addressed trim warnings and marked assembly as trimmable for Native AOT Lambda functions.
### Amazon.Lambda.Serialization.SystemTextJson (2.4.1)  
* Marked SourceGeneratorLambdaJsonSerializer<T> constructor that took in an instance of JsonSerializerContext as Obsolete. This was due to the JsonSerializerContext instance unlikely to have the correct JsonSerializerOptions for Lambda serialization.

## Release 2024-01-18

### Amazon.Lambda.CognitoEvents (3.0.0)
* Pull Request [#1656](https://github.com/aws/aws-lambda-dotnet/pull/1656) Add contracts for cognito pre token generation v2. Thanks [Ernest Folch](https://github.com/ernest-folch-fleksy)
* Pull Request [#1646](https://github.com/aws/aws-lambda-dotnet/pull/1646) Fixed the JSON deserialization error in Cognito triggered. Thanks [Ankush Jain](https://github.com/ankushjain358)

## Release 2024-01-12

### Amazon.Lambda.DynamoDBEvents (3.0.0)
* Removed `AWSSDK.DynamoDBv2` dependency from `DynamoDBEvent` and related classes.
### Amazon.Lambda.Serialization.Json (2.2.0)
* Updated contract resolvers to be compatible with the latest version of the `Amazon.Lambda.DynamoDBEvents` package. 

## Release 2023-12-15

### Amazon.Lambda.Templates (6.15.1)
* Update Powertools dependencies in relevant Lambda templates.

## Release 2023-11-20

### Amazon.Lambda.TestTool (0.15.1)
* Fixed issue with getting a 404 for "_framework/blazor.server.js" when the UI started up for .NET 6 and 7.
  
## Release 2023-11-17

### Amazon.Lambda.TestTool (0.15.0)
* Released .NET 8 version as NuGet package Amazon.Lambda.TestTool-8.0
* Pull Request [#1598](https://github.com/aws/aws-lambda-dotnet/pull/1598) Add width to .main to prevent window overflow. Thanks [mleziva](https://github.com/mleziva)
* Deprecated the .NET Core 3.1 and .NET 5 version. No new versions targeting those frameworks will be released.

## Release 2023-11-15

### Amazon.Lambda.Annotations (1.1.0)
* Added support for deploying as an executable assembly and targeting provided.al2 and provided.al2023. This allows support for deploying .NET 8 AOT Lambda functions.
* Addressed AOT trim warnings.

## Release 2023-11-13

### Amazon.Lambda.AspNetCoreServer (8.1.1)
* Pull Request [#1599](https://github.com/aws/aws-lambda-dotnet/pull/1599) adding `application/x-protobuf` to list of content types that should be base 64 encoded. Thanks [yuriygavriluk](https://github.com/yuriygavriluk)
### Amazon.Lambda.AspNetCoreServer.Hosting (1.6.1)
* Updated dependency Amazon.Lambda.AspNetCoreServer to 8.1.1
### Amazon.Lambda.Templates (6.15.0)
* Update custom runtime templates to use provided.al2023 and .NET 8

## Release 2023-10-26

### Amazon.Lambda.RuntimeSupport (1.10.0)
* Marked as trimmable for .NET 8
* Applied the `RequiresUnreferencedCode` attribute to areas that caused trim warnings. These code paths are used by the managed runtime when running .NET Lambda functions from a class library. Code paths for an executable assembly Lambda function, used for Native AOT, will not trigger trim warnings with this release.
### Amazon.Lambda.Serialization.SystemTextJson (2.4.0)
* Marked as trimmable for .NET 8
* For trimmed Lambda functions the `SourceGeneratorLambdaJsonSerializer` must be used. The other serializers have been marked with the `RequiresUnreferencedCode` attribute.
* Added new constructor for `SourceGeneratorLambdaJsonSerializer` that takes in the `JsonSerializerContext` as a parameter to remove the reflection call to create the isntance.  
### Amazon.Lambda.APIGatewayEvents (2.7.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.ApplicationLoadBalancerEvents (2.2.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.CloudWatchEvents (4.4.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.CloudWatchLogsEvents (2.2.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.CognitoEvents (2.2.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.ConfigEvents (2.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.ConnectEvents (1.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.Core (2.2.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.DynamoDBEvents (2.3.0)
* Due to a dependency on the AWSSDK.DynamoDBv2 package the `DynamoDBEvent` event class is marked with the RequiresUnreferencedCode making it not safe for trimming.
### Amazon.Lambda.KafkaEvents (2.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.KinesisAnalyticsEvents (2.3.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.KinesisEvents (2.2.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.KinesisFirehoseEvents (2.3.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.LexEvents (3.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.LexV2Events (1.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.MQEvents (2.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.S3Events (3.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.SimpleEmailEvents (3.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.SNSEvents (2.1.0)
* Marked as trimmable for .NET 8
### Amazon.Lambda.SQSEvents (2.2.0)
* Marked as trimmable for .NET 8

## Release 2023-10-13

### Amazon.Lambda.RuntimeSupport (1.9.1)
* Added `AWS_LAMBDA_DOTNET_DISABLE_MEMORY_LIMIT_CHECK` environment variable that if set to `true` disables the logic to configure the max heap memory size for the .NET runtime.

## Release 2023-10-13

### Amazon.Lambda.RuntimeSupport (1.9.0)
* Added .NET 8 target for the package.
* Pull Request [#1578](https://github.com/aws/aws-lambda-dotnet/pull/1578) using new .NET 8 APIs to inform the .NET runtime how much memory the Lambda environment is configured for. This helps the GC understand when nearing the memory limit and when it should be more aggressive collecting memory.

## Release 2023-09-05

### Amazon.Lambda.DynamoDBEvents (2.2.0)
* Added support for DynamoDBTimeWindowEvent.
### Amazon.Lambda.KinesisEvents (2.1.0)
* Added support for KinesisTimeWindowEvent.
* Added new StreamsEventResponse class for reporting batch item failures when processing streams for KinesisEvent.

## Release 2023-08-24

### Amazon.Lambda.TestTool (0.14.1)
* Fixes an issue where using SSO profile was giving error while monitoring DLQ in Lambda Test Tool.
### Amazon.Lambda.MQEvents (2.0.0)
* **Breaking Change:** Corrected the date type of RabbitMQMessage.BasicProperties.Priority to Nullable int in RabbitMQEvent class.

## Release 2023-08-02

### Amazon.Lambda.TestTool (0.14.0)
* Add new --disable-logs switch used with --no-ui to ensure the console does not include logs just the function's response.
* Set default values on the ILambdaContext for `FunctionName`, `InvokedFunctionArn` and `AwsRequestId`.
* Fixed issue when a Lambda function build directory contains multiple `*.runtimeconfig.json` file causing the tool to pick the wrong file.
* Pull Request [#1556](https://github.com/aws/aws-lambda-dotnet/pull/1556) add null check when traversing parent folders. Thanks [joshuA Seither](https://github.com/joshuaseither).
* Pull Request [#1558](https://github.com/aws/aws-lambda-dotnet/pull/1558) fix casing issue with integ test. Thanks [joshuA Seither](https://github.com/joshuaseither).

## Release 2023-07-17

### Amazon.Lambda.Templates (6.14.0)
* Update Annotations template to use 1.0.0 of Amazon.Lambda.Annotations, and convert the Lambda Empty Serverless and Lambda Empty Serverless (.NET 7 Container Image) templates to use Annotations.

## Release 2023-07-14

### Amazon.Lambda.Annotations (1.0.0)
* Update to version 1.0.0
* Diagnostic errors are now thrown when invalid attribute names are specified for `FromQuery`, `FromRoute`, and `FromHeader`

## Release 2023-06-23

### Amazon.Lambda.Annotations (0.13.5)
* Fixed error thrown when template includes a CloudFormation short-hand intrinsic function

## Release 2023-06-18

### Amazon.Lambda.Serialization.Json (2.1.1)
* Fixed issue null pointer exception when parsing events for types with MemoryStream.
### Amazon.Lambda.Annotations (0.13.4)
* Add diagnostic error message when using complex types mapped to query string parameters.

## Release 2023-06-08

### Amazon.Lambda.RuntimeSupport (1.8.8)
* Fixed issue with failing to report uncaught exceptions that have non-ascii characters from Lambda functions.
### Amazon.Lambda.CognitoEvents (2.1.1)
* Pull Request [#1523](https://github.com/aws/aws-lambda-dotnet/pull/1523) fixed incorrect modeling of the `ChallengeAnswer` property. Thanks [JP Grusling](https://github.com/jp-grusling)

## Release 2023-06-07

### Amazon.Lambda.TestTool (0.13.1)
* Fixed issues parsing environment variables from `aws-lambda-tools-defaults.json` that were quoted.
### Amazon.Lambda.Templates (6.13.1)
* Renamed Powertools for AWS Lambda blueprint and updated dependencies to Powertools for AWS Lambda.

## Release 2023-05-08

### Amazon.Lambda.Annotations (0.13.3)
* Pull Request [#1504](https://github.com/aws/aws-lambda-dotnet/pull/1504) Add Http Status Code property on IHttpResult. Thanks [Paulo Serra](https://github.com/kabaluk)
* Fixed issue mapping to HTTP headers using different casing
* Fixed issue using nullable parameter types for mapped HTTP elements
* Added dependency to Amazon.Lambda.Core to make sure core interfaces are always available when adding Amazon.Lambda.Annotations to a project
* Added compile error if no `LambdaSerailizerAttribute` is set for the assembly
* Added compile error if missing a reference to `Amazon.Lambda.APIGatewayEvents` when using the API Gateway attributes
### Amazon.Lambda.S3Events (3.0.1)
* Fixed typo in documentation for S3ObjectLambdaEvent


## Release 2023-04-24

### Amazon.Lambda.Annotations (0.13.2)
* Fixed issue with diagnostics message not being correctly formatted.
### Amazon.Lambda.Templates (6.13.0)
* Update blueprints to latest versions of AWS dependencies.

## Release 2023-04-23

### Amazon.Lambda.RuntimeSupport (1.8.7)
* Pull request [#1491](https://github.com/aws/aws-lambda-dotnet/pull/1491) Fix issue with running native aot executables in root folder. Thanks [Richard Davison](https://github.com/richarddd)

## Release 2023-04-06

### Amazon.Lambda.Templates (6.11.0)
* Update blueprints to latest versions of AWS dependencies.  
* Update Lambda Annotations blueprint to use `FrameworkReference` to take advantage of the ASP.NET Core assemblies already available in the Lambda environment.

## Release 2023-04-06

### Amazon.Lambda.KafkaEvents (2.0.0)
* **Breaking Change:** Corrected the data type for Headers to use signed bytes.
### Amazon.Lambda.Annotations (0.13.1)
* Fixed issue with code generation errors not being reported correctly as diagnostic errors. 
### Amazon.Lambda.TestTool (0.13.0)
* Added support for setting environment variables configured in CloudFormation template or config file before invoking function.

## Release 2023-03-23

### Amazon.Lambda.RuntimeSupport (1.8.6)
* Fixed issue getting ObjectDisposedException writing to Console.Out after Console.Out had been unintendedly disposed.
### Amazon.Lambda.AspNetCoreServer (8.1.0)
* Pull request [#1463](https://github.com/aws/aws-lambda-dotnet/pull/1463) add support for IHttpRequestFeature.RawTarget. Thanks [Kevin Stapleton](https://github.com/kevinstapleton)
### Amazon.Lambda.AspNetCoreServer.Hosting (1.6.0)
* Update to Amazon.Lambda.AspNetCoreServer dependency version to 8.1.0
### Amazon.Lambda.Templates (6.11.0)
* Update blueprints to latest versions of AWS dependencies.  

## Release 2023-03-22

### Amazon.Lambda.RuntimeSupport (1.8.5)
* Update user agent string when running as Native AOT

## Release 2023-03-20

### Amazon.Lambda.MQEvents (1.1.0)
* Pull request [#1452](https://github.com/aws/aws-lambda-dotnet/pull/1452) Adds custom properties to ActiveMQ event. Thanks [Alex Issa](https://github.com/alexaiss)

## Release 2023-03-06

### Amazon.Lambda.CloudWatchEvents (4.3.0)
* Pull request [#1447](https://github.com/aws/aws-lambda-dotnet/pull/1447) adds events for Translate Parallel Data State Change and Translate Text Translation Job State Change. Thanks [Bryan Hogan](https://github.com/bjhogan)

## Release 2023-03-01

### Amazon.Lambda.Annotations (0.13.0)
* **Breaking Change:** Renamed `LambdaFunctionAttribute` property `Name` to `ResourceName` to clarify this property is used for setting the CloudFormation resource name.
* Add diagnostic error message if the value for `ResourceName` is invalid for a CloudFormation resource name.
### Amazon.Lambda.RuntimeSupport (1.8.4)
* Fixed `FUNCTION_ERROR_INIT_FAILURE` error when using provisioned concurrency with Native AOT
### Amazon.Lambda.CloudWatchEvents (4.2.0)
* Pull request [#1445](https://github.com/aws/aws-lambda-dotnet/pull/1445) add Transcribe event object. Thanks [Bryan Hogan](https://github.com/bjhogan)
### Amazon.Lambda.AspNetCoreServer.Hosting (1.5.1)
* Update dependency of Amazon.Lambda.Runtime to version 1.8.4
* Update dependency of Amazon.Lambda.AspNetCoreServer to version 8.0.0

## Release 2023-02-27

### Amazon.Lambda.Templates (6.10.0)
* Add new blueprints for [Amazon Lambda Powertools](https://github.com/awslabs/aws-lambda-powertools-dotnet)
* Update Annotations blueprint to use version 0.12.0 of Amazon.Lambda.Annotations.

## Release 2023-02-13

### Amazon.Lambda.Annotations (0.12.0)
* Pull request [#1432](https://github.com/aws/aws-lambda-dotnet/pull/1432) add common 5xx status codes to HttpResults. Thanks [Ryan Cormack](https://github.com/ryancormack)
### Amazon.Lambda.APIGatewayEvents (2.6.0)
* Pull request [#1066](https://github.com/aws/aws-lambda-dotnet/pull/1066) add V2 API Gateway customer authrorizer. Thanks [Oleksandr Liakhevych](https://github.com/Dreamescaper)
### Amazon.Lambda.AspNetCoreServer (8.0.0)
*  **Breaking change** Pull request [#899](https://github.com/aws/aws-lambda-dotnet/pull/899) make adding exception detail to http response opt-in using new `IncludeUnhandledExceptionDetailInResponse` property. Thanks [duncanbrown](https://github.com/duncanbrown)
* Pull request [#1403](https://github.com/aws/aws-lambda-dotnet/pull/1403) add support for resource path variable substitution. Thanks [christostatitzikidis](https://github.com/christostatitzikidis)
### Amazon.Lambda.CloudWatchEvents (4.1.0)
* Pull request [#1073](https://github.com/aws/aws-lambda-dotnet/pull/1073) add CloudWatch Events/EventBridge event types for S3 object events. Thanks [Richard P. Field III](https://github.com/rpf3)
### Amazon.Lambda.CognitoEvents (2.1.0)
* Pull request [#1051](https://github.com/aws/aws-lambda-dotnet/pull/1051) add contract types for Cognito Trigger events. Thanks [Jon Armen](https://github.com/jon-armen)

## Release 2023-02-08

### Amazon.Lambda.Annotations (0.11.0)
* Support customizng HTTP responses including status code and headers using the new IHttpResult and HttpResults types. Read more information [here](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations#customizing-responses-for-api-gateway-lambda-functions).
* Fix issue with incorrect code being generated from methods that return a `Task` but does not use the `async` keyword.

## Release 2023-02-02

### Amazon.Lambda.Serialization.SystemTextJson (2.3.1)
* Pull request [#1382](https://github.com/aws/aws-lambda-dotnet/pull/1382) Make Amazon.Lambda.Serialization.SystemTextJson compatible with trimming. Thanks [Beau Gosse](https://github.com/Beau-Gosse-dev)

## Release 2023-01-23

### Amazon.Lambda.KinesisFirehoseEvents (2.2.0)
* Pull request [#1346](https://github.com/aws/aws-lambda-dotnet/pull/1346) adding partition key metadata for response object. Thanks [Chris Smith](https://github.com/chris-smith-zocdoc)

## Release 2023-01-15

### Amazon.Lambda.RuntimeSupport (1.8.2)
* Add timestamp when making API calls to the Lambda service runtime's logging API. This timestamp is not visible to end users but is instead used by the Lambda service to make sure the log statements are properly orderd.
### Amazon.Lambda.CloudWatchEvents (4.0.1)
* Corrected the type of the ContainerOverride.Environment property.

## Release 2022-12-09

### Amazon.Lambda.AspNetCoreServer.Hosting (1.5.0)
* Pull request [#1311](https://github.com/aws/aws-lambda-dotnet/pull/1311) add ability to configure the ILambdaSerializer when calling AddAWSLambdaHosting. Thanks [James Eastham](https://github.com/jeastham1993).

## Release 2022-12-08

### Amazon.Lambda.Annotations (0.10.0-preview)
* Added support for `dynamic` return types and parameters
* Added a message to the CloudFormation template description when managed by Lambda Annotations with an opt-out flag
### Amazon.Lambda.Templates (6.9.0)
* Update AWS package dependencies in blueprints.

## Release 2022-12-07

### Amazon.Lambda.AspNetCoreServer (7.3.0)
* Pull request [#1350](https://github.com/aws/aws-lambda-dotnet/pull/1350), implement IHttpActivityFeature for plumbing diagnostic activities. Thanks [Brendon Parker](https://github.com/brendonparker)
### Amazon.Lambda.AspNetCoreServer.Hosting (1.4.0)
* Pull request [#1350](https://github.com/aws/aws-lambda-dotnet/pull/1350), implement IHttpActivityFeature for plumbing diagnostic activities. Thanks [Brendon Parker](https://github.com/brendonparker)

## Release 2022-12-06

### Amazon.Lambda.Templates (6.8.1)
* Fixed S3 Lambda blueprint using inconsistent name between the class name and the function handler value.
* Pull request [#1374](https://github.com/aws/aws-lambda-dotnet/pull/1374), fixed typo in Native AOT blueprint. Thanks [Pieter Germishuys](https://github.com/pgermishuys)

## Release 2022-11-16

### Amazon.Lambda.Templates (6.8.0)
* Update container based blueprints to use .NET 7 base image.

## Release 2022-11-09

### Amazon.Lambda.TestTool (0.12.7)
* Fixed issue with .NET 7 version looking in build output folder for .NET 6.

## Release 2022-11-08

### Amazon.Lambda.Templates (6.7.0)
* Add Native AOT .NET 7 blueprints.

## Release 2022-11-04

### Amazon.Lambda.Templates (6.6.0)
* Update packagereferences for Amazon NuGet packages to latest versions.
* Update Custom Runtime blueprint to target .NET 7.
* Miscellaneous style and documentation clean up in S3 event, Annotations and ASP.NET Core Web API blueprints.
### Amazon.Lambda.TestTool (0.12.6)
* Release NuGet package Amazon.Lambda.TestTool-7.0 for .NET 7.
  
## Release 2022-10-26

### Amazon.Lambda.Annotations (0.9.0-preview)
* Fixed issue with incorrectly syncing CloudFormation template when there are build errors.

## Release 2022-10-12

### Amazon.Lambda.Templates (6.5.0)
* Updated Javascript Dependencies in ASPNetCoreWeb App template to latest versions

## Release 2022-10-04

### Amazon.Lambda.TestTool (0.12.6)
* Fixed issue with Lambda Functions using source generator serializer and returning a non-generic Task.

## Release 2022-10-03

### Amazon.Lambda.TestTool (0.12.5)
* Fixed issue [#1124](https://github.com/aws/aws-lambda-dotnet/issues/1124) with Lambda Functions using source generator serializer.

## Release 2022-09-29

### Amazon.Lambda.Templates (6.4.0)
* Fixed issue with using templates with .NET 7 RC is installed.

## Release 2022-09-13

### Amazon.Lambda.Annotations (0.8.0-preview)
* Add third party licenses
* Correctly populate the role property in CloudFormation template
* Copy XML documentation for Amazon.Lambda.Annotations into nupkg to drive IntelliSense
* Add more detail to the Lambda Annotations README.md
### Amazon.Lambda.TestTool (0.12.4)
* Pull request [#1308](https://github.com/aws/aws-lambda-dotnet/pull/1308) fixing serialization of messages while monitoring dead letter queue. Thanks [William Keller](https://github.com/william-keller) 

## Release 2022-08-29

### Amazon.Lambda.Annotations (0.7.0-preview)
* Add support for syncing YAML based CloudFormation templates
* Fix code generation bug for non API Gateway based Lambda functions returning a Task with no value

## Release 2022-08-03

### Amazon.Lambda.Templates (6.3.0)
* Updated PackageReferences of AWS packages referenced in the templates.

## Release 2022-08-02

### Amazon.Lambda.Annotations (0.6.0-preview)  
* Breaking Change: API Gataway attributes have been moved to the Amazon.Lambda.Annotations.APIGateway namespace.
* Fix issue with incorrect code being generated when Lambda function return void.
* Fix issue with CloudFormation template not being sync when all LambdaFunction attributes are removed from code. 
### Amazon.Lambda.S3Events (3.0.0)
* Remove dependency from the AWSDK.S3. This reduces deployment bundle size and fixes serialization issues with the SDK enum like classes. This is a small breaking change due to class having different namespaces.
### Amazon.Lambda.LexV2Events (1.0.1)
* Fixed the type of LexV2Interpretation.NluConfidence to double.
 

## Release 2022-06-28

### BlueprintPackager**
* Bump Newtonsoft.Json to 13.0.1
### VS2017 Blueprints**
* Bump Newtonsoft.Json to 13.0.1
### VS2019 Blueprints**
* Bump Newtonsoft.Json to 13.0.1
### Amazon.Lambda.Serialization.Json (2.1.0)
* Bump Newtonsoft.Json to 13.0.1
### Amazon.Lambda.APIGatewayEvents (2.5.0)
* Bump Newtonsoft.Json to 13.0.1
### Amazon.Lambda.TestTool-3.1 (0.12.3)
* Bump Newtonsoft.Json to 13.0.1
### Amazon.Lambda.TestTool-5.0 (0.12.3)
* Bump Newtonsoft.Json to 13.0.1
### Amazon.Lambda.TestTool-6.0 (0.12.3)
* Bump Newtonsoft.Json to 13.0.1

## Release 2022-05-26

### Amazon.Lambda.RuntimeSupport (1.8.2)
* Fixed issue with large log messages triggering an extra empty CloudWatch Log record being created.

## Release 2022-05-26

### Amazon.Lambda.RuntimeSupport (1.8.1)
* Fixed issue when log messages were larger then 1K they would be broken up over multiple CloudWatch Log records.
### Amazon.Lambda.AspNetCoreServer.Hosting (1.3.1)
* Updated dependency on Amazon.Lambda.RuntimeSupport to version 1.8.1

## Release 2022-05-25

### Amazon.Lambda.LexV2Events (1.0.0)
* New package that contains classes that can be used as input and response types for Lambda functions that process Amazon Lex V2 event.

## Release 2022-05-18

### Amazon.Lambda.AspNetCoreServer (7.2.0)
* Set the HttpContext.TraceIdentifier to the trace id for the Lambda invocation
### Amazon.Lambda.AspNetCoreServer.Hosting (1.3.0)
* Updated dependency on Amazon.Lambda.AspNetCoreServer to version 7.2.0
  
  
## Release 2022-05-06

### Amazon.Lambda.RuntimeSupport (1.8.0)
* Logging messages with newlines will now be a single CloudWatch Log record instead of a record for each line. Note, container based Lambda functions will continue to have a separate record per line.
### Amazon.Lambda.AspNetCoreServer.Hosting (1.2.0)
* Updated dependency on Amazon.Lambda.RuntimeSupport to version 1.8.0
  
## Release 2022-05-02

### Amazon.Lambda.KafkaEvents (1.0.1)
* Corrected the return type of Partition property in KafkaEventRecord.
### Amazon.Lambda.TestTool-3.1 (0.12.2)
* Corrected the return type of partition property for Kafka event.
* Added test request for Application Load Balancer.
### Amazon.Lambda.TestTool-5.0 (0.12.2)
* Corrected the return type of partition property for Kafka event.
* Added test request for Application Load Balancer.
### Amazon.Lambda.TestTool-6.0 (0.12.2)
* Corrected the return type of partition property for Kafka event.
* Added test request for Application Load Balancer.
### Amazon.Lambda.Annotations (0.5.1-preview)
* Pull Request [#1101](https://github.com/aws/aws-lambda-dotnet/pull/1101) Error message for missing Amazon.Lambda.APIGatewayEvents package.
* Fixed bug with Amazon.Lambda.Annotations not correctly using assembly name for computing function handler string.
* Fix issue with naming collisions for request and context with Amazon.Lambda.Annotations.
### Amazon.Lambda.MQEvents (1.0.0)
* New package that contains classes that can be used as input types for Lambda functions that process Amazon ActiveMQ and RabbitMQ events.
### Amazon.Lambda.APIGatewayEvents (2.4.1)
* [Breaking Change] Corrected property name ConnectionAt to ConnectedAt in APIGatewayProxyRequest.ProxyRequestContext class.
### Amazon.Lambda.DynamoDBEvents (2.1.1)
* Update AWSSDK.DynamoDBv2 reference to version 3.7.3.24.
* Update blueprint definitions to reference new Amazon.Lambda.DynamoDBEvents version 2.1.1 and AWSSDK.DynamoDBv2 version 3.7.3.24.

## Release 2022-03-29

### AWSLambdaPSCore PowerShell Module (3.0.1)
* Pull Request [#1096](https://github.com/aws/aws-lambda-dotnet/pull/1096) Fix error message when .NET SDK found that is below .NET 6 requirement. Thanks [Lachlan Blake](https://github.com/Otimie)

## Release 2022-03-25

### Amazon.Lambda.Templates (6.1.0)
* Updated PackageReferences of AWS packages referenced in the templates.

## Release 2022-03-24

### Amazon.Lambda.Serialization.SystemTextJson (2.3.0)
* Change SourceGeneratorLambdaJsonSerializer to use the same JsonSerializerOptions as DefaultLambdaJsonSerializer
* Allow SourceGeneratorLambdaJsonSerializer to be extended with a custom Action<JsonSerializerOptions> to customize the JsonSerializerOptions

## Release 2022-03-14

### Amazon.Lambda.AspNetCoreServer (7.1.0)
* Fix issue with ASP.NET Core Minimal API not binding HTTP request body to complex types
* Pull Request [#1099](https://github.com/aws/aws-lambda-dotnet/pull/1099) Logging improvements. Thanks [Martin Costello](https://github.com/martincostello)
* Pull Request [#1103](https://github.com/aws/aws-lambda-dotnet/pull/1103) Support image/x-icon as base64. Thanks [Martin Costello](https://github.com/martincostello)  
* Pull Request [#1103](https://github.com/aws/aws-lambda-dotnet/pull/1102) Switch to TryGetValue for minor perf improvement. Thanks [Martin Costello](https://github.com/martincostello)    
### Amazon.Lambda.AspNetCoreServer.Hosting (1.1.0)
* Updated dependency on Amazon.Lambda.AspNetCoreServer to 7.1.0
### Amazon.Lambda.TestTool-3.1 (0.12.1)
* Fix issue with sending large function input events being truncated.
* Pull Request [#1098](https://github.com/aws/aws-lambda-dotnet/pull/1098) Add HTTP API sample request. Thanks [Martin Costello](https://github.com/martincostello)
### Amazon.Lambda.TestTool-5.0 (0.12.1)
* Fix issue with sending large function input events being truncated.
* Pull Request [#1098](https://github.com/aws/aws-lambda-dotnet/pull/1098) Add HTTP API sample request. Thanks [Martin Costello](https://github.com/martincostello)  
### Amazon.Lambda.TestTool-6.0 (0.12.1)
* Fix issue with sending large function input events being truncated.
* Pull Request [#1098](https://github.com/aws/aws-lambda-dotnet/pull/1098) Add HTTP API sample request. Thanks [Martin Costello](https://github.com/martincostello)  
  
  
## Release 2022-02-24  

### Amazon.Lambda.TestTool-3.1 (0.12.0)
* Add new page for testing executable assemblies. Useful for testing Lambda functions using top-level statements or function deployed as a custom runtime.
### Amazon.Lambda.TestTool-5.0 (0.12.0)
* Add new page for testing executable assemblies. Useful for testing Lambda functions using top-level statements or function deployed as a custom runtime.
### Amazon.Lambda.TestTool-6.0 (0.12.0)
* Add new page for testing executable assemblies. Useful for testing Lambda functions using top-level statements or function deployed as a custom runtime.

## Release 2022-02-23

### AWSLambdaPSCore PowerShell Module (3.0.0)
* Switch publishing to target the .NET 6 Lambda runtime
### Amazon.Lambda.Templates (6.0.0)
* Update Lambda templates to target .NET 6.

## Release 2022-02-02

### Amazon.Lambda.RuntimeSupport (1.7.0)
* Pull Request [#1063](https://github.com/aws/aws-lambda-dotnet/pull/1063) Minor performance improvements particular when targeting .NET 6.

## Release 2022-01-05

### Amazon.Lambda.SQSEvents (2.1.0)
* Pull Request [#1039](https://github.com/aws/aws-lambda-dotnet/pull/1039) Add new `SQSBatchResponse` type to indicate which messages failed and need to be retried. Thanks [jon-armen](https://github.com/jon-armen)
### Amazon.Lambda.Templates (5.8.0)
* Pull Request [#1041](https://github.com/aws/aws-lambda-dotnet/pull/1041) Updating README files explaining how to use ARM64. Thanks [Bryan J Hogan](https://github.com/bryanjhogan)
  
## Release 2021-12-21

### Amazon.Lambda.Annotations (0.4.2-preview)
* First preview release of the [Lambda Annotation framework](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.Annotations)

## Release 2021-12-12

### Amazon.Lambda.Templates (5.7.0)
* Updated container image based templates to use .NET 6
* Updated PackageReferences to latest version for Amazon packages

## Release 2021-12-12

### Amazon.Lambda.AspNetCoreServer (7.0.1)
* Fixed issue of duplicate log messages written to CloudWatch Logs
### Amazon.Lambda.RuntimeSupport (1.6.0)
* Write unhandled exceptions from Lambda function to CloudWatch Logs
* Add exception information into X-Ray trace
* Port .NET Core 3.1 managed runtime functionality for AWS_LAMBDA_DOTNET_PREJIT environment variable
* Switch JSON parsing to System.Text.Json
* Fixed incorrect JSON parsing for Lambda context Cognito fields
* Fixed deadlock issue when using both Console.WriteX and ILambdaLogger with multiple threads
### Amazon.Lambda.TestTool-6.0 (0.11.4)
* Updated to latest version of Amazon.Lambda.Core (2.1.0)
### Amazon.Lambda.TestTool-5.0 (0.11.4)
* Updated to latest version of Amazon.Lambda.Core (2.1.0)
### Amazon.Lambda.TestTool-3.1 (0.11.4)
* Updated to latest version of Amazon.Lambda.Core (2.1.0)

## Release 2021-11-22

### Amazon.Lambda.AspNetCoreServer (7.0.0)
* [Breaking Change] Removed support for .NET Core 2.1
### Amazon.Lambda.RuntimeSupport (1.5.0)
* Added new environment variable `AWS_LAMBDA_HANDLER_LOG_FORMAT` to configure logging format. Supported values are `Default` and `Unformatted`.
### Amazon.Lambda.Templates (5.6.0)
* Updated custom runtime templates to use .NET 6.
### Amazon.Lambda.TestTool-6.0 (0.11.3)
* Added .NET 6 support for test tool support to help with custom runtime .NET 6 functions.

  
## Release 2021-11-05

### Amazon.Lambda.AspNetCoreServer.Hosting (1.0.0)
* New package to make easy to configure ASP.NET Core project using minimal api style as Lambda functions.
### Amazon.Lambda.AspNetCoreServer (6.1.0)
* Changes to support the new Amazon.Lambda.AspNetCoreServer.Hosting packages
### Amazon.Lambda.Core (2.1.0)
* Add new Log level APIs for .NET 6
### Amazon.Lambda.Serialization.SystemTextJson (2.2.0)
* Add new source generator based serializers for .NET 6
### Amazon.Lambda.RuntimeSupport (1.4.0)
* Added new LambdaBootstrapBuilder class to build the LambdaBootstrap
* Implemented the new Amazon.Lambda.Core logging APIs for .NET 6
### Amazon.Lambda.CloudWatchEvents (4.0.0)
* [Breaking Change] Moved NameValue class from Amazon.Lambda.CloudWatchEvents.ECSEvents to Amazon.Lambda.CloudWatchEvents namespace for reusability.
* [Breaking Change] Updated the model definitions for BatchJobStateChangeEvent.

## Release 2021-10-15

### AWSLambdaPSCore PowerShell Module (2.2.0)
* Added ability to set AWS credentials explicilty using `-AWSAccessKeyId`, `-AWSSecretKey`, and `-AWSSessionToken`
* Added `-Architecture` parameter to configure the Lambda function to use ARM64 architecture
  
## Release 2021-09-28

### Amazon.Lambda.Templates (5.5.0)
* Updated PackageReference versions for AWS SDK for .NET
* Updated container image blueprints to use .NET version independent publish path.
* Updated custom runtime blueprints to use provided.al2 Lambda runtime
* Fixed issue with project name not correctly replacing all instances of BlueprintBaseName

## Release 2021-08-26

### Amazon.Lambda.DynamoDBEvents (2.1.0)
* Added support for reporting batch item failures when processing streams for DynamoDBEvent.

## Release 2021-07-15

### Amazon.Lambda.AspNetCoreServer (6.0.3)
* Fixed issue with internal server errors not returning as HTTP status code 500

## Release 2021-06-09

### Amazon.Lambda.ConnectEvents (1.0.0)
* Added support for Amazon Connect ContactFlow event.

## Release 2021-06-02

### Amazon.Lambda.CloudWatchEvents (3.0.0)
* [Breaking Change] Updated the model definitions for ECSTaskStateChangeEvent.
### Amazon.Lambda.KinesisAnalyticsEvents (2.2.1)
* Added missing System.Text.Json.Serialization.JsonPropertyName attribute for some properties in KinesisAnalyticsFirehoseInputPreprocessingEvent and KinesisAnalyticsStreamsInputPreprocessingEvent classes.
### Amazon.Lambda.LexEvents (3.0.0)
* Added OriginalValue field to SlotDetail class for LexEvent.
* [Breaking Change] Changed data type of LexCurrentIntent.NluIntentConfidenceScore to Nullable\<double\> for LexEvent.

## Release 2021-05-06

### Amazon.Lambda.AspNetCoreServer (6.0.2)
* Fixed issue with HTTP Status Code not being initialized to 200.

## Release 2021-05-06

### AWSLambdaPSCore PowerShell Module (2.1.0)
* Pull Request [#726](https://github.com/aws/aws-lambda-dotnet/pull/726) the default version of PowerShell Core to 6.1.1. Thanks [Ben Gelens](https://github.com/bgelens)
* Set HOME environment variable for PowerShell Lambda (work around for [PowerShell/PowerShell#13189](https://github.com/PowerShell/PowerShell/issues/13189))

## Release 2021-05-02

### Amazon.Lambda.Templates (5.3.0)
* Fixed permission issue in DynamoDB Blog blueprint
* Updated dependencies on AWS libraries
  
## Release 2021-04-30

### Amazon.Lambda.AspNetCoreServer (6.0.0)
* Remove unnecessary log message that was cluttering the attached CloudWatch Log stream

## Release 2021-04-06

### Amazon.Lambda.TestTool-2.1 (0.11.3)
* Fixed issue with testing ASP.NET Core based Lambda functions and services registered in the IServiceCollection not resolving.
### Amazon.Lambda.TestTool-3.1 (0.11.3)
* Fixed issue with testing ASP.NET Core based Lambda functions and services registered in the IServiceCollection not resolving.
### Amazon.Lambda.TestTool-5.0 (0.11.3)
* Fixed issue with testing ASP.NET Core based Lambda functions and services registered in the IServiceCollection not resolving.

## Release 2021-04-05

### Amazon.Lambda.AspNetCoreServer (6.0.0)
* [Breaking Change] Pull request [#721](https://github.com/aws/aws-lambda-dotnet/pull/721). Fixes double encoding issue with query string parameters. Thanks [Peter Liljenberg](https://github.com/petli)
### Amazon.Lambda.S3 (2.0.1)
* Fixed bug with deserializing XAmzRequestId property of S3ObjectLambdaEvent.
  
## Release 2021-03-29

### Amazon.Lambda.CognitoEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.ConfigEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.Core (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.DynamoDBEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.KinesisEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.S3Events (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.Serialization.Json (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.SimpleEmailEvents (3.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.SNSEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.SQSEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.SQSEvents (2.0.0)
* Netstandard 1.3 support removed
### Amazon.Lambda.TestUtilities (2.0.0)
* Netstandard 1.3 support removed

## Release 2021-03-24

### Amazon.Lambda.S3 (1.3.0)
* Added event object for S3 Object Lambda.
### Amazon.Lambda.LexEvents (2.2.0)
* Pull request [#833](https://github.com/aws/aws-lambda-dotnet/pull/833). Added advanced options for LexBot events. Thanks [ssorc3](https://github.com/ssorc3)
### Amazon.Lambda.TestTool-2.1 (0.11.2)
* Pull request [#834](https://github.com/aws/aws-lambda-dotnet/pull/834). Fixed issue invoking Lambda function in test tool with no payload. Thanks [#russau](https://github.com/russau).
### Amazon.Lambda.TestTool-3.1 (0.11.2)
* Pull request [#834](https://github.com/aws/aws-lambda-dotnet/pull/834). Fixed issue invoking Lambda function in test tool with no payload. Thanks [#russau](https://github.com/russau).
### Amazon.Lambda.TestTool-5.0 (0.11.2)
* Pull request [#834](https://github.com/aws/aws-lambda-dotnet/pull/834). Fixed issue invoking Lambda function in test tool with no payload. Thanks [#russau](https://github.com/russau).

## Release 2021-02-19

### Amazon.Lambda.AspNetCoreServer (5.3.1)
* Pull request [#815](https://github.com/aws/aws-lambda-dotnet/pull/815). Fix for when the certificate PEM string contains a trailing new line ('\n'). Thanks [Damian Hickey](https://github.com/damianh)

## Release 2021-01-11

### Amazon.Lambda.AspNetCoreServer (5.3.0)
* Pull request [#787](https://github.com/aws/aws-lambda-dotnet/pull/787). Add support for ITlsConnectionFeature and marshall the APIGW client cert to HttpContext. Thanks [Damian Hickey](https://github.com/damianh)
### Amazon.Lambda.SimpleEmailEvents (2.2.0)
* Pull request [#777](https://github.com/aws/aws-lambda-dotnet/pull/777). Add DMARC verdict. Thanks [Lu�s Sousa](https://github.com/luiscnsousa).
### Amazon.Lambda.TestTool-3.1 (0.11.1)
* Fixed issue with incorrectly parsing ImageUri from the serverless.template.
### Amazon.Lambda.TestTool-5.0 (0.11.1)
* Fixed issue with incorrectly parsing ImageUri from the serverless.template.
### Amazon.Lambda.Templates (5.1.0)
* Update all references to the deprecated AWSLambdaFullAccess managed policy to AWSLambda_FullAccess
* Pull request [#755](https://github.com/aws/aws-lambda-dotnet/pull/755). Added Support for Binary Media Types in AspNetCoreWebApp Blueprint. Thanks [Carlos Santos](https://github.com/csantos).

## Release 2020-12-01

### Amazon.Lambda.APIGatewayEvents (2.4.0)
* Add Lambda and IAM authorizer fields to APIGatewayHttpApiV2ProxyRequest
### Amazon.Lambda.RuntimeSupport (1.3.0)
* This version is the implementation for Lambda Runtime Interface Client used in the .NET 5 base container image `public.ecr.aws/lambda/dotnet:5.0`
* Added support to load user's .NET function based on function handler string. 
### Amazon.Lambda.Templates (5.0.0)
* Added templates targeting .NET 5 as container images.
* Fixed .NET 5 Custom Runtime template to use `DefaultLambdaJsonSerializer`
### Amazon.Lambda.TestTool-2.1 (0.11.0)
* Added support for reading container image configuration information from `aws-lambda-tools-defaults.json` or the CloudFormation template.
### Amazon.Lambda.TestTool-3.1 (0.11.0)
* Added support for reading container image configuration information from `aws-lambda-tools-defaults.json` or the CloudFormation template.
### Amazon.Lambda.TestTool-5.0 (0.11.0)
* New version of the test tool to support .NET 5.0. Feature set and codebase is same as the .NET Core 3.1 just retargeted to .NET 5.0.
### AWS Lambda .NET 5 Base Image**
* Added the **LambdaRuntimeDockerfiles** directory to this repository which contains the Dockerfile used to build AWS Lambda .NET 5 base image.


## Release 2020-10-30

### Amazon.Lambda.Templates (4.2.0)
* Updated custom runtime templates to target .NET 5.

## Release 2020-10-21

### Amazon.Lambda.APIGatewayEvents (2.3.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.ApplicationLoadBalancerEvents (2.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.AspNetCoreServer (5.2.0)
* Update code signing certificate for signing the assembly.
* Pull request [#751](https://github.com/aws/aws-lambda-dotnet/pull/751) Return cookies through proxy response message to support multiple cookies. Thanks [Peter Liljenberg](https://github.com/petli)
### Amazon.Lambda.CloudWatchEvents (2.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.CloudWatchLogsEvents (2.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.CognitoEvents (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.ConfigEvents (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.Core (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.DynamoDBEvents (1.2.0)
* Update code signing certificate for signing the assembly.
* Updated to latest version of AWSSDK.DynamoDBv2.
### Amazon.Lambda.KinesisAnalyticsEvents (2.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.KinesisEvents (1.2.0)
* Update code signing certificate for signing the assembly.
* Updated to latest version of AWSSDK.Kinesis.
### Amazon.Lambda.KinesisFirehoseEvents (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.LexEvents (2.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.Logging.AspNetCore (3.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.PowerShellHost (2.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.RuntimeSupport (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.S3Events (1.2.0)
* Update code signing certificate for signing the assembly.
* Updated to latest version of AWSSDK.S3.
### Amazon.Lambda.Serialization.Json (1.8.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.Serialization.SystemTextJson (2.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.SimpleEmailEvents (2.1.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.SNSEvents (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.SQSEvents (1.2.0)
* Update code signing certificate for signing the assembly.
### Amazon.Lambda.TestUtilities (1.2.0)
* Update code signing certificate for signing the assembly.


## Release 2020-09-30

### Amazon.Lambda.APIGatewayEvents (2.2.0)
* Added new properties for API Gateway's mutual tls feature.
### Amazon.Lambda.AspNetCoreServer (5.1.6)
* Updated to version 2.2.0 of Amazon.Lambda.APIGatewayEvents

## Release 2020-09-16

### Amazon.Lambda.Serialization.SystemTextJson (2.0.2)
* Added default JsonWriterOptions to change serialization of quotation marks from ascii representation to an escaped quote
### Amazon.Lambda.AspNetCoreServer (5.1.5)
* Updated to version 2.0.2 of Amazon.Lambda.Serialization.SystemTextJson
  
## Release 2020-09-09

### Amazon.Lambda.AspNetCoreServer (5.1.4)
* Pull request [#729](https://github.com/aws/aws-lambda-dotnet/pull/729) Added code to load cookies from HTTPv2 request. Thanks [Andy Hopper](https://github.com/andyhopp)

## Release 2020-07-23

### Amazon.Lambda.TestTool-3.1 (0.10.1)
* Fixed issue with dead locking getting triggered Lambda function being executed blocks on async calls.
### Amazon.Lambda.PowerShellHost (2.1.0)
* Set the HOME environment variable before executing PowerShell script. This is a work around to the following PowerShell issue: [PowerShell/PowerShell/issues/13189](https://github.com/PowerShell/PowerShell/issues/13189) 
### Amazon.Lambda.AspNetCoreServer (5.1.3)
* Pull request [#672](https://github.com/aws/aws-lambda-dotnet/pull/672) Improve error handling when marshalling API Gateway request. Thanks [Grahame Horner](https://github.com/grahamehorner)

## Release 2020-06-24

### Amazon.Lambda.Logging.AspNetCore (3.0.1)
* Pull request [#683](https://github.com/aws/aws-lambda-dotnet/pull/683) Fixed issue with using internal NullScope type. Thanks [Zdenek Havlin](https://github.com/wdolek)
### Amazon.Lambda.AspNetCoreServer (5.1.2)
* Updated to use latest **Amazon.Lambda.Logging.AspNetCore** dependency.

## Release 2020-05-04

### Amazon.Lambda.Serialization.SystemTextJson (2.0.1)
* Fixed issue with response not being written to log when LAMBDA_NET_SERIALIZER_DEBUG is set to true.
### Amazon.Lambda.AspNetCoreServer (5.1.1)
* Updated to version 2.0.1 of Amazon.Lambda.Serialization.SystemTextJson
### Amazon.Lambda.Templates (4.1.2)
* Updated blueprints to use latest versions of AWS packages

## Release 2020-04-28

### Amazon.Lambda.Serialization.SystemTextJson (2.0.0)
* Added new `DefaultLambdaJsonSerializer` class to replace `LambdaJsonSerializer` has inconsistent casing issues with the JSON serialized from .NET objects.
* DefaultLambdaJsonSerializer addresses with with LambdaJsonSerializer not honoring the JsonSerializerOptions when LAMBDA_NET_SERIALIZER_DEBUG environment variable set.
* Added `CamelCaseLambdaJsonSerializer` for use cases where the JSON serialized from .NET object need camelCase.
* Obsoleted `LambdaJsonSerializer` due to issues with inconsistent JSON casing. Users should update to `DefaultLambdaJsonSerializer`.
### Amazon.Lambda.AspNetCoreServer (5.1.0)
* When targeting .NET Core 3.1 bootstrapping switched to `IHostBuilder`.
* Updated [README](https://github.com/aws/aws-lambda-dotnet/tree/master/Libraries/src/Amazon.Lambda.AspNetCoreServer#bootstrapping-application-iwebhostbuilder-vs-ihostbuilder) explaining how bootstrapping works for Lambda. 
### Amazon.Lambda.APIGatewayEvents (2.1.0)
* Add `PathParameters` to `APIGatewayHttpApiV2ProxyRequest`
### Amazon.Lambda.ApplicationLoadBalancerEvents (2.0.0)
* Updated Application LoadBalancer response objects to have `JsonPropertyName` on the properties to make sure the casing matched what the Application LoadBalancer expected.
### Amazon.Lambda.KinesisAnalyticsEvents (2.1.0)
* Updated Kinesis Analytics response objects to have `JsonPropertyName` on the properties to make sure the casing matched what Kinesis Analytics expected.
### Amazon.Lambda.LexEvents (2.0.0)
* Updated Lex response objects to have `JsonPropertyName` on the properties to make sure the casing matched what Lex expected.
### Amazon.Lambda.Templates (4.1.1)
* Updated blueprints to use latest versions of AWS packages
* Updated ASP.NET Core 3.1 blueprints to use `IHostBuilder`

## Release 2020-04-07

### Amazon.Lambda.TestTool.WebTester21 (0.10.1)
* Fixed issue with not correctly loading assemblies for the selected project in the custom AssemblyLoadContext.

## Release 2020-04-03

### Amazon.Lambda.RuntimeSupport (1.1.1)
* Pull Request [#611](https://github.com/aws/aws-lambda-dotnet/pull/611) Fixes issue with RemainingTime from the LambdaContext returning negative values. Thanks [Martin Costello](https://github.com/martincostello)


## Release 2020-03-31

### Amazon.Lambda.Serialization.SystemTextJson (1.0.0)
* New JSON serializer based on System.Text.Json
### Amazon.Lambda.AspNetCoreServer (5.0.0)
* Added support for API Gateway HTTP API using the new `APIGatewayHttpApiV2ProxyFunction` base class
* Fixed issue with HttpContext.RequestServices returning null
* Use new **Amazon.Lambda.Serialization.SystemTextJson** for JSON serialization when targeting .NET Core 3.1
### Amazon.Lambda.APIGatewayEvents (2.0.0)
* Added support for API Gateway HTTP API support using `APIGatewayHttpApiV2ProxyRequest` and `APIGatewayHttpApiV2ProxyResponse` classes
### Amazon.Lambda.TestTool.WebTester21 (0.10.0)
* Load Lambda code in separate AssemblyLoadContext to avoid assembly collisions
* Added new switch `--no-ui` to start debugging code immediately with using the web interface. More info can be found [here](https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool#skip-using-the-web-interface)
### Amazon.Lambda.TestTool.WebTester31 (0.10.0)
* New test tool for .NET Core 3.1 Lambda functions
* Uses same class library for loading and executing Lambda code
* Has separate UI built using Server Side Blazor framework
### Amazon.Lambda.Templates (4.0.0)
* Updated templates to target .NET Core 3.1
* Added WebSocket API template

## Release 2019-12-18

### Amazon.Lambda.AspNetCoreServer (4.1.0)
* Pull Request [#558](https://github.com/aws/aws-lambda-dotnet/pull/558) Add support for response compression. Thanks [Justin Dearing](https://github.com/jdearing)
* Fixed NPE bug when using the new API Gateway HTTP API support.
* Fixed issue with request content-length header not being set.
### Amazon.Lambda.APIGatewayEvents (1.3.0)
* Added OperationName, Error, IntegrationLatency, MessageDirection, RequestTime, RequestTimeEpoch, Status, ApiKeyId and AccessKey fields.


## Release 2019-11-05

### Amazon.Lambda.RuntimeSupport (1.1.0)
* Pull Request [#540](https://github.com/aws/aws-lambda-dotnet/pull/540) Improve testability of for LambdaBootstrap. Thanks [Martin Costello](https://github.com/martincostello)

## Release 2019-10-24

### Amazon.Lambda.AspNetCoreServer (4.0.0)
* Added support for ASP.NET Core 3.0 which can be used with Lambda Custom Runtime.
* Added <strong>PostMarshallHttpAuthenticationFeature</strong>. Allows subclasses to customize the <strong>ClaimsPrincipal</strong> for the incoming request.</li>
* Added <strong>PostMarshallItemsFeatureFeature</strong>. Allows subclasses to customize what is added to the <strong>Items</strong> collection of the HttpContext for the incoming request. 
* Breaking changes to support ASP.NET Core 3.0
  * Removed <strong>PostCreateContext</strong>. 
  * Items collection on HttpContext has been changed to return null when attempting to get a value that does not exist. This was done to match the behavior of ASP.NET Core requests coming from Kestrel.
### Amazon.Lambda.Logging.AspNetCore (3.0.0)
* Pull Request [#520](https://github.com/aws/aws-lambda-dotnet/pull/520) Match type namespace prefix as well when building `LogLevels`. Thanks [Zdenek Havlin](https://github.com/wdolek)
* Pull Request [#522](https://github.com/aws/aws-lambda-dotnet/pull/522) Adjust handling of `Default` log category, adjusting it to .NET. Thanks [Zdenek Havlin](https://github.com/wdolek)   
### Amazon.Lambda.Serialization.Json (1.7.0)
* Pull Request [#525](https://github.com/aws/aws-lambda-dotnet/pull/525) add naming strategy option to JsonSerializer. Thanks [Maxime Beaudry](https://github.com/mabead)
* Pull Request [#518](https://github.com/aws/aws-lambda-dotnet/pull/518) performance improvement reusing Contract resolvers. Thanks [Daniel Marbach](https://github.com/danielmarbach)
### Amazon.Lambda.SimpleEmailEvents (2.0.0)
* Pull Request [#496](https://github.com/aws/aws-lambda-dotnet/pull/496) Split out SimpleEmailEventsReceiptAction into different action types. Thanks [Craig Brett](https://github.com/craigbrett17)
### Amazon.Lambda.TestTool-2.1 (0.9.5) (Preview)
* Pull Request [#513](https://github.com/aws/aws-lambda-dotnet/pull/513) Fix typos in Lambda.TestTool/WebTester js files. Thanks [Clay](https://github.com/cyrisX2)
### Amazon.Lambda.Templates (3.9.0)
* Added ASP.NET Core 3.0 Custom Runtime template.
* Updated lambda.CustomRuntimeFunction template to .NET Core 3.0
* Updated AWS SDK for.NET and Amazon Lambda package references in all of the templates.


## Release 2019-08-15

### Amazon.Lambda.Serialization.Json (1.6.0)
* Pull Request [#503](https://github.com/aws/aws-lambda-dotnet/pull/503) add constructor that allows changing the serializer settings. Thanks [Maxime Beaudry](https://github.com/mabead)
### Amazon.Lambda.TestTool-2.1 (0.9.4) (Preview)
* Pull Request [#506](https://github.com/aws/aws-lambda-dotnet/pull/506) Update docs and error messages. Thanks [ericksoen](https://github.com/ericksoen)
### Amazon.Lambda.Templates (3.8.1)
* Updated AWS SDK for.NET and Amazon Lambda package references in all of the templates.

## Release 2019-06-20

### Amazon.Lambda.TestTool-2.1 (0.9.3)
* Explicily reference the latest version of Newtonsoft.Json (12.0.2). This allows 
Lambda functions that are using a newer then what ASP.NET Core uses by default to have issues 
loading Newtonsoft.Json.

## Release 2019-06-19

### Amazon.Lambda.Logging.AspNetCore (2.3.0)
* Pull Request [#471](https://github.com/aws/aws-lambda-dotnet/pull/471) added support for logging scopes. Thanks [Piotr Karpala](https://github.com/karpikpl)
### Amazon.Lambda.AspNetCoreServer (3.1.0)
* Updated to use version 2.3.0 of Amazon.Lambda.Logging.AspNetCore
* Pull Request [#459](https://github.com/aws/aws-lambda-dotnet/pull/459) add warning when using incorrect base type. Thanks [Hans van Bakel](https://github.com/hvanbakel)
### Amazon.Lambda.Templates (3.8.0)
* Pull Request [#457](https://github.com/aws/aws-lambda-dotnet/pull/457) added Simple Notification Service template. Thanks [Nathan Westfall](https://github.com/nwestfall)
* Remove version number for Microsoft.AspNetCore.App in ASP.NET Core templates. The validation check is no longer needed in current versions of .NET Core and has been removed from AWS .NET Tooling.
* Updated AWS SDK for.NET and Amazon Lambda package references in all of the templates.

## Release 2019-05-01

### Amazon.Lambda.AspNetCoreServer (3.0.4)
* Pull Request [#449](https://github.com/aws/aws-lambda-dotnet/pull/449) fixing routing with escape characters in resource path. Thanks [Chris/0](https://github.com/chrisoverzero)
* Fixed url encoding issue with query string values when called by API Gateway. [#451](https://github.com/aws/aws-lambda-dotnet/pull/451)
* Fixed issue handling ELB Health Checks when Lambda function placed behind an Application Load Balancer. [#452](https://github.com/aws/aws-lambda-dotnet/pull/452)
### Amazon.Lambda.Templates (3.7.1)
* Updated dependencies for AWS SDK for .NET and the Amazon Lambda packages to the latest version.

## Release 2019-03-18

### Amazon.Lambda.TestTool-2.1 (0.9.2) (Preview)
* Fixed issue loading dependent assemblies when the name differs from the NuGet package.

## Release 2019-03-18

### Amazon.Lambda.RuntimeSupport (1.0.0)
* New package to support running custom .NET Core Lambda runtimes like .NET Core 2.2. Read the following blog for more information. [https://aws.amazon.com/blogs/developer/announcing-amazon-lambda-runtimesupport/](https://aws.amazon.com/blogs/developer/announcing-amazon-lambda-runtimesupport/)
### Blueprints
* New Custom Runtime blueprint for both C# and F#
* **Amazon.Lambda.Templates (3.7.0)** released with latest blueprints.


## Release 2019-02-21

### Amazon.Lambda.AspNetCoreServer (3.0.3)
* Pull Request [#409](https://github.com/aws/aws-lambda-dotnet/pull/409) allowing claims from custom authorizer to be passed into ASP.NET Core. Thanks [Lukas Sinkus](https://github.com/LUS1N)

## Release 2019-02-21

### Amazon.Lambda.AspNetCoreServer (3.0.2)
* Fixed bug with Amazon.Lambda.Logging.AspNetCoreServer not reading logging settings from configuration like appsettings.json.
* Added PostCreateWebHost virtual method to run code after the IWebHost has been created but not started.
### Amazon.Lambda.Logging.AspNetCore (2.2.0)
* Pull Request [#401](https://github.com/aws/aws-lambda-dotnet/pull/401) adds ability to log EventId and Exception. Thanks [Piotr Karpala](https://github.com/aws/aws-lambda-dotnet/pull/401)
### Amazon.Lambda.TestTool-2.1 (0.9.1) (Preview)
* Pull Request [#403](https://github.com/aws/aws-lambda-dotnet/pull/403) added `--path` command line argument. Thanks [Aidan Ryan](https://github.com/aidanjryan)
* Fixed bug when searching for default config files during startup.
### Blueprints
* Updated logging section in appsettings.json to Informational to match before the logging fix in Amazon.Lambda.AspNetCoreServer
* Updated NuGet dependencies.
* **Amazon.Lambda.Templates (3.6.0)** released with latest blueprints.

## Release 2019-02-08

### Amazon.Lambda.AspNetCoreServer (3.0.1)
* Fixed issue with content-type being incorrectly set by API Gateway when ASP.NET Core does not return a content-type.
### Blueprints
* ASP.NET Core based templates updated to use 3.0.1 of Amazon.Lambda.AspNetCoreServer.
* **Amazon.Lambda.Templates (3.5.1)** released with latest blueprints.

## Release 2019-02-07

### Amazon.Lambda.TestTool-2.1 (0.9.0) (Preview)
* Pull Request [#364](https://github.com/aws/aws-lambda-dotnet/pull/364) added support for parsing YAML CloudFormation template. Thanks [Samuele Resca](https://github.com/samueleresca)
### Amazon.Lambda.APIGatewayEvents (1.2.0)
* Pull Request [#382](https://github.com/aws/aws-lambda-dotnet/pull/382) added "ConnectionId" and "DomainName" to APIGatewayProxyRequest. Thanks [FranciscoJCLus](https://github.com/FranciscoJCLus)
* Added support for multi value headers and query string parameters.
* Added netstandard2.0 target framework.
### Amazon.Lambda.ApplicationLoadBalancerEvents (1.0.0)
* New package for AWS Lambda request and response types when Lambda function is integrated with an ELB Application Load Balancer.
### Amazon.Lambda.AspNetCoreServer (3.0.0)
* Support for Application load Balancer via new **ApplicationLoadBalancerFunction** base class.
* Switch to use multi value headers and query string parameters support from Amazon.Lambda.APIGatewayEvents.
* Fixed issue with url decoded resource parameters
* Fixed issue incorrectly url encoding query string parameters
### Amazon.Lambda.CloudWatchEvents (1.0.0)
* New package for AWS Lambda event types for CloudWatch Events.
* Pull Request [#329](https://github.com/aws/aws-lambda-dotnet/pull/329) added support for Schedule events. Thanks [Kalarrs Topham](https://github.com/exocom)
* Pull Request [#328](https://github.com/aws/aws-lambda-dotnet/pull/328) added support for Batch events. Thanks [Kalarrs Topham](https://github.com/exocom)
* Pull Request [#327](https://github.com/aws/aws-lambda-dotnet/pull/327) added support for ECS events. Thanks [Kalarrs Topham](https://github.com/exocom)
### Amazon.Lambda.CloudWatchLogsEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.CognitoEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.ConfigEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.Core (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.DynamoDBEvents (1.1.0)
* Added netstandard2.0 target framework.
* Updated dependency on AWSSDK.DynamoDBv2 to 3.3.17.5
### Amazon.Lambda.KinesisAnalyticsEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.KinesisEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.KinesisFirehoseEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.LexEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.Logging.AspNetCore (2.1.0)
* Updated to use 1.1.0 of Amazon.Lambda.Core to take advantage of the new .netstandard2.0 version.
### Amazon.Lambda.S3Events (1.1.0)
* Added netstandard2.0 target framework.
* Updated dependency on AWSSDK.S3 to 3.3.31.15
### Amazon.Lambda.Serialization.Json (1.5.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.SimpleEmailEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.SNSEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.SQSEvents (1.1.0)
* Added netstandard2.0 target framework.
### Amazon.Lambda.TestUtilties (1.1.0)
* Added netstandard2.0 target framework.
### AWSLambdaPSCore PowerShell Module (1.2.0)
* Pull Request [#372](https://github.com/aws/aws-lambda-dotnet/pull/372) the default version of PowerShell Core to 6.1.1. Thanks [Andrew Pearce](https://github.com/austoonz)
* Pull Request [#380](https://github.com/aws/aws-lambda-dotnet/pull/380) added SQS blueprints. Thanks [Andrew Pearce](https://github.com/austoonz)
* Pull Request [#381](https://github.com/aws/aws-lambda-dotnet/pull/381) added S3 blueprints. Thanks [Andrew Pearce](https://github.com/austoonz)
### Blueprints
* New Application Load Balancer blueprint.
* Updated Amazon Lambda and AWS SDK for .NET package dependencies to latest version.
* Updated ASP.NET Core test projects for the switch to multi value headers.
* **Amazon.Lambda.Templates (3.5.0)** released with latest blueprints.


## Release 2018-11-19

### Amazon.Lambda.TestTool-2.1 (0.8.0) (Preview)
* Initial release of the new AWS .NET Mock Lambda Test Tool. Checkout the [README.md](https://github.com/aws/aws-lambda-dotnet/tree/master/Tools/LambdaTestTool) for more details.

## Release 2018-09-25

### AWSLambdaPSCore PowerShell Module (1.1.0)
* Updated the default version of PowerShell Core to 6.1.0. This can be adjusted using the `PowerShellSdkVersions` parameter.
* Pull Request [#322](https://github.com/aws/aws-lambda-dotnet/pull/322) added CloudFormation custom resource template. Thanks [Nate Ferrell](https://github.com/scrthq)
### Amazon.Lambda.Serialization.Json (1.4.0)
* Added more informative error message when unable to serialize JSON request and responses.
### Amazon.Lambda.AspNetCoreServer (2.1.0)
* Pull Request [#308](https://github.com/aws/aws-lambda-dotnet/pull/308) added typed startup class. Thanks [Chris/0](https://github.com/chrisoverzero)
* Pull Request [#304](https://github.com/aws/aws-lambda-dotnet/pull/304) switched to use ASP.NET Core Logging allowing consumers to filter the logging messages. Thanks [Albert Szilvasy](https://github.com/szilvaa)
* Pull Request [#315](https://github.com/aws/aws-lambda-dotnet/pull/315) added image/jpg to list of binary encoding types. Thanks [Khaja Nizamuddin](https://github.com/NizamLZ)
### Blueprints
* Pull Request [#324](https://github.com/aws/aws-lambda-dotnet/pull/324) fixed issue with SQS template not including the `<AWSProjectType>Lambda</AWSProjectType>` element. Thanks [Greg
Hartshorne](https://github.com/ghartsho)
in the project file.
* Updated all templates to version 1.1.0 Amazon.Lambda.Serialization.Json.
* **Amazon.Lambda.Templates (3.4.0)** released with latest blueprints.

## Release 2018-09-11

### Amazon.Lambda.PowerShellHost (1.0.0)
* New NuGet package that hosts the PowerShell Core runtime within a Lambda function. 
When the Lambda function is invoked it will execute a provided PowerShell script.
### AWSLambdaPSCore PowerShell Module (1.0.0.2)
* New PowerShell module for authoring and publishing PowerShell based Lambda functions. 
For further details view the [PowerShell Lambda Readme](https://github.com/aws/aws-lambda-dotnet/tree/master/PowerShell).
### Blueprints
* Remove **DotNetCliToolReference** reference to **Amazon.Lambda.Tools** now that Amazon.Lambda.Tools has 
been converted to a Global Tool. Check out the [announcement blog](https://aws.amazon.com/blogs/developer/net-core-global-tools-for-aws/) for further details. 
* **Amazon.Lambda.Templates (3.3.0)** released with latest blueprints.

## Release 2018-07-09

### Blueprints
* Updated blueprints to use the new .NET Core 2.1 Lambda runtime.
* Pull request [#291](https://github.com/aws/aws-lambda-dotnet/pull/291). Improving F# blueprints. Thanks to [sharptom](https://github.com/sharptom)
* **Amazon.Lambda.Templates (3.2.0)** released with latest blueprints.

## Release 2018-06-28

### Amazon.Lambda.SQSEvents (1.0.0)
* New package for AWS Lambda event types for Amazon Simple Queue Service (SQS).
### Amazon.Lambda.Serialization.Json (1.3.0)
* Updated to handle the base 64 encoded strings coming from SQS events into .NET System.IO.MemoryStream objects.
### Blueprints
* New Amazon SQS blueprint.
* **Amazon.Lambda.Templates (3.1.0)** released with latest blueprints.

## Release 2018-05-29

### Amazon.Lambda.AspNetCoreServer (2.0.4)
* Pull request [#277](https://github.com/aws/aws-lambda-dotnet/pull/277). Fixed issue with calculating PathBase for URLs with trailing slashes.
* Pull request [#267](https://github.com/aws/aws-lambda-dotnet/pull/267). Provide ability to delay initializing the ASP.NET Core framework till first request.
* Fixed issue with ASP.NET Core not returning a content-type header and API Gateway incorrectly converting content-type to `application/json`.
### Amazon.Lambda.APIGatewayEvents (1.1.3)
* Add missing property `UsageIdentifierKey` to `APIGatewayCustomAuthorizerResponse`

## Release 2018-04-30

### Amazon.Lambda.AspNetCoreServer (2.0.3)
* Add work around for returning multiple cookies. API Gateway only allows returning one value per header. Cookies are returned by the SET-COOKIE header. To get around the limitation the SET-COOKIE header is returned with difference casing for each cookie.
* Change how ASP.NET Core Lambda functions choose how to configure logging by checking for the existence of the LAMBDA_TASK_ROOT environment variable instead of the ASPNETCORE_ENVIRONMENT environment variable being set to Development.
### Amazon.Lambda.Templates (3.0.0)
* Add F# based project templates including a new [Giraffe](https://github.com/giraffe-fsharp/Giraffe) based project template. To create an F# based Lambda project pass in the **-lang F#** command line switch.
  *  ``dotnet new serverless.Giraffe -lang F# --region us-west-2 --profile default -o MyFSharpLambdaProject`` 
*  Change shortname prefix for Serverless based projects to **serverless** from **lambda**. Serverless projects are deployed with CloudFormation with any other required AWS resources defined in the CloudFormation template.
*  Add Serverless version of **DetectImageLabels** and **S3** templates which also create the S3 bucket and configure the notification as part of deployment.
*  Fixed issues when creating projects with '.' and '-' in the project name.

## Release 2018-03-26

### Amazon.Lambda.AspNetCoreServer (2.0.2)
* Fixed issue with encoding HTTP request resource path
### Amazon.Lambda.Serialization.Json (1.2.0)
* Pull request [#234](https://github.com/aws/aws-lambda-dotnet/pull/234). Added constructor to allow passing in custom converters. 
This was needed to support F# specific converters. Thanks to [rfrerebe](https://github.com/rfrerebe).
### Amazon.Lambda.Tools (2.1.2)
* Moved this package to the [AWS Extensons for .NET CLI](https://github.com/aws/aws-extensions-for-dotnet-cli) repository 
along with the Amazon Elastic Container Service and AWS Elastic Beanstalk CLI extensions.

## Release 2018-02-28

### Amazon.Lambda.KinesisAnalyticsEvents (1.0.0)
* Pull request [#232](https://github.com/aws/aws-lambda-dotnet/pull/232). New event package for Kinesis Analytics.

## Release 2018-02-12

### Amazon.Lambda.AspNetCoreServer (2.0.1)
* Implemented the OnStarting and OnCompleted callbacks for an HttpResponse.
* Fixed marshalling issue with API Gateway request to ASP.NET Core request.
### Amazon.Lambda.Tools (2.1.1)
* Add dependency to **AWSSDK.SecurityToken** to support profiles that use assume role features of Security Token Service.
### Blueprints
* **Amazon.Lambda.Templates (2.0.3)** released with updated NuGet dependencies for the blueprints.

## Release 2018-02-05

### Amazon.Lambda.APIGatewayEvents (1.1.2)
* **APIGatewayCustomAuthorizerRequest** updated to have the parameters for a [request](https://docs.aws.amazon.com/apigateway/latest/developerguide/use-custom-authorizer.html#api-gateway-custom-authorizer-types) type custom authorizer.
* **APIGatewayCustomAuthorizerContextOutput** Updated to support custom return fields.
### Amazon.Lambda.Tools (2.1.0)
* Add new **tracing-mode** switch for AWS X-Ray support
* Add new **tags** switch to set tags on deployed functions
* Add new **msbuild-parameters** switch to pass in additional arguments to the **dotnet publish** command. 
Also any arguments passed in on the commandline starting with **/p:** are assumed to be parameters to be passed to **dotnet publish**.
For example `dotnet lambda deploy-function TestFunc /p:Version=2.0.0`

## Release 2018-01-16

### Amazon.Lambda.Tools (2.0.1)
* Fixed issue with .NET Core 2.0 version requiring .NET Core 1.0 runtime being installed
### Blueprints
* **Amazon.Lambda.Templates (2.0.1)** released with Amazon.Lambda.Tools reference bumped to 2.0.1 in blueprints.

## Release 2018-01-15

### Amazon.Lambda.Tools (2.0.0)
* Added support for **.NET Core 2.0** runtime in AWS Lambda.
* Add Validation if project is using a newer version of **Microsoft.AspNetCore.All** than is currently available in Lambda.
* Ignore, with warning, passed-in template parameters that are not declared in serverless.template.
* Fixed issue with **--function-publish** switch not working during function update.
### Amazon.Lambda.APIGatewayEvents (1.1.1)
* Deserialize incoming claims and custom authorizer properties to **APIGatewayCustomAuthorizerContext**
* Add missing **Path** property on **ProxyRequestContext**
### Amazon.Lambda.AspNetCoreServer (2.0.0)
* Updated target framework to **.NET Standard 2.0** and **ASP.NET Core 2.0**
* If Cognito claims are found on an incoming API Gateway request create a **ClaimsPrincipal** with the claims for the **HttpContext.User**.
* Added virtual methods **PostMarshallRequestFeature**, **PostMarshallConnectionFeature**, **PostMarshallResponseFeature** and **PostCreateContext** 
to allow derived classes a chance to alter how requests and responses are marshalled. 
* Mimic **WebHost.CreateDefaultBuilder** when creating the IWebHostBuilder and replace Kestrel registration with API Gateway. 
* When not in development switch out the Console logger with **Amazon.Lambda.Logger.AspNetCore** to make sure
application logging goes to the associated CloudWatch Logs without ANSI Console colors syntax.
* Fixed issue with not setting PathBase when marshalling request.
* Updated implementation of **Microsoft.AspNetCore.Hosting.Server.IServer** to match ASP.NET Core 2.0 declaration.
### Amazon.Lambda.Logging.AspNetCore (2.0.0)  
* Updated target framework to **.NET Standard 2.0** and **ASP.NET Core 2.0**.
* Added registration extension methods to **Microsoft.Extensions.Logging.ILoggingBuilder**.
### Blueprints
* New blueprint for an ASP.NET Core Web Application with Razor Pages.
* **Amazon.Lambda.Templates (2.0.0)** released with latest .NET Core 2.0 blueprints.


## Release 2017-12-23

### Amazon.Lambda.S3Events (1.0.2)
* Updated to use latest AWSSDK.S3 to fix issue with comparing EventName property to the SDK EventType constants.
### Blueprints
* Update S3 blueprint to use latest Amazon.Lambda.S3Events.
* **Amazon.Lambda.Templates (1.4.5)** released with latest blueprints.


## Release 2017-12-22

### Amazon.Lambda.CloudWatchLogsEvents (1.0.0)
* Pull request [#188](https://github.com/aws/aws-lambda-dotnet/pull/188). New event package for CloudWatch Logs. Thanks to [Toshinori Sugita](https://github.com/toshi0607).
### Amazon.Lambda.Tools (1.9.0)
* Added new **--apply-defaults** switch. If set to true from either the command 
line or aws-lambda-tools-defaults.json, values from the aws-lambda-tools-defaults.json 
file will be applied when updating an existing function. By default function 
configuration values from aws-lambda-tools-defaults.json are ignored when 
updating an existing function to avoid unattended changes to production functions.
### Blueprints
* Update dependency reference for **Amazon.Lambda.Tools** to 1.9.0
* **Amazon.Lambda.Templates (1.4.4)** released with latest blueprints.

## Release 2017-12-20

### Amazon.Lambda.LexEvents (1.0.2)
* Add slot details and request attributes to LexEvent.
### Blueprints
* Update dependency reference for **Amazon.Lambda.LexEvents**
* **Amazon.Lambda.Templates (1.4.3)** released with latest blueprints.

## Release 2017-12-12

### Amazon.Lambda.LexEvents (1.0.1)
* Pull request [#184](https://github.com/aws/aws-lambda-dotnet/pull/184), fixing issue with InputTranscript property on the wrong class. Thanks to [jmeijon](https://github.com/jmeijon).
### Blueprints
* Update dependency reference for **Amazon.Lambda.LexEvents**
* **Amazon.Lambda.Templates (1.4.2)** released with latest blueprints.

## Release 2017-10-12

### Amazon.Lambda.Tools (1.8.1)
* Fixed issue deploying to AWS Lambda in the US GovCloud region.

## Release 2017-09-15

### Amazon.Lambda.Tools (1.8.0)
* Add support using YAML formatted CloudFormation templates.
### Blueprints
* Update dependency reference for **Amazon.Lambda.Tools**
* **Amazon.Lambda.Templates (1.4.1)** released with latest blueprints.

## Release 2017-08-23

### Amazon.Lambda.Tools (1.7.1)
* Fixed error message when missing required parameters.
* Improved logic for searching for the current dotnet CLI
* Added **--disable-version-check** switch for users that want to try running with libraries that declare .NET Core 1.1 dependencies.
    * Note, running with these libraries can have unforeseen side effects so only recommended for advanced cases with lots of testing done on the functions.


## Release 2017-08-22

### Blueprints
* Added new AWS Step Function Hello World blueprint.
* **Amazon.Lambda.Templates (1.4.0)** released with latest blueprints.

## Release 2017-07-26

### Amazon.Lambda.Tools (1.7.0)
* Add **--disable-interactive** switch for use in CI systems to prevent the tooling from blocking waiting missing required parameters.
* Fixed issue with serverless deployment that was not returning a failed exit code when the CloudFormation stack failed to be created.
### Blueprints
* Update dependency reference for **Amazon.Lambda.Tools**
*  **Amazon.Lambda.Templates (1.3.1)** released with latest blueprints.

## Release 2017-06-23

### Amazon.Lambda.AspNetCoreServer (0.10.2-preview1)
* Fixed issue computing resource path for custom domains.
* Fixed issue with resource path not being URL decoded.


## Release 2017-06-01

### Amazon.Lambda.Tools (1.6.0)
* Add new **package-ci** command to use for deployment with [AWS CodePipeline](https://aws.amazon.com/codepipeline/). This is the .NET Core Lambda equivalent of the AWS CLI command [aws cloudformation package](http://docs.aws.amazon.com/cli/latest/reference/cloudformation/package.html).
* Add **--template-substitutions** option to **deploy-serverless** and **package-ci** commands allowing parts of the serverless-template to be defined in separate files.
* Fixed issue with dead letter queue configuration getting cleared out during redeploy.
* Pull request [#117](https://github.com/aws/aws-lambda-dotnet/pull/117), displaying tool version number. Thanks to [Corey Coto](https://github.com/coreycoto).
* Add error check when deploying from Linux and the `zip` command line utility is not found. The `zip` tool is required on Linux to maintain file permissions.
### Amazon.Lambda.Logging.AspNetCore (1.1.0)
* Pull request [#110](https://github.com/aws/aws-lambda-dotnet/pull/110), adding support for log category wildcards. Thanks to [Cris Barbero](https://github.com/cfbarbero).
### Blueprints
* Update dependency reference for **Amazon.Lambda.Tools** and **Amazon.Lambda.Logging.AspNetCore**
*  **Amazon.Lambda.Templates (1.2.0)** released with latest blueprints.


## Release 2017-04-28

### Amazon.Lambda.AspNetCoreServer (0.10.1-preview1)
* Fixed issue with not registering the JSON serializer.
### Blueprints
* Updated ASP.NET Core WebAPI blueprints to use version **Amazon.Lambda.AspNetCoreServer (0.10.1-preview1)**.
*  **Amazon.Lambda.Templates (1.2.1)** released with latest blueprints.
      
## Release 2017-04-27

### Amazon.Lambda.KinesisFirehoseEvents (1.0.0)
* New package for AWS Lambda event types for Amazon Kinesis Firehose.
### Blueprints
* New Amazon Kinesis Firehose blueprint.
*  **Amazon.Lambda.Templates (1.2.0)** released with latest blueprints.


## Release 2017-04-26

### Amazon.Lambda.Tools (1.5.0)
* Added validation to stop deploying .NET Core 1.0 AWS Lambda functions if the project includes .NET Core 1.1 dependencies.
### Amazon.Lambda.LexEvents (1.0.0)
* New package for AWS Lambda event types for Amazon Lex	
### Amazon.Lambda.Serialization.Json (1.1.0)
* Added serialization logging which can be enabled by setting the environment variable **LAMBDA_NET_SERIALIZER_DEBUG = true**
### Amazon.Lambda.AspNetCoreServer (0.10.0-preview1)
* Pull request [#75](https://github.com/aws/aws-lambda-dotnet/pull/75), adding binary support. Check the [README.md](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.AspNetCoreServer/README.md) for details. Thanks to [Eugene Bekker](https://github.com/ebekker).
  * Pull request [#89](https://github.com/aws/aws-lambda-dotnet/pull/89), populate RemoteIpAddress and RemotePort on HttpContext.Connection. Thanks to [Marcus Lum](https://github.com/Marcus-L).
  * Added the **APIGatewayProxyRequest** and **ILambdaContext** to the **HttpContext.Items** collection with the collection keys **APIGatewayRequest** and **LambdaContext**.
* Removed request and response logging and rely on the new logging available in **Amazon.Lambda.Serialization.Json**. This allowed the method signature to be changed back to directly use the **Amazon.Lambda.APIGatewayEvents** class.
### Amazon.Lambda.APIGatewayEvents (1.1.0)
* Added IsBase64Encoded property to APIGatewayProxyResponse as part of the binary support for **Amazon.Lambda.AspNetCoreServer**.
### Blueprints
* New Amazon Lex blueprint for the Lex Book Trip getting started [sample](http://docs.aws.amazon.com/lex/latest/dg/ex-book-trip.html).
* Updated all blueprints to latest version of the Amazon NuGet packages.
* Amazon.Lambda.Templates (1.1.0) released with latest blueprints.
 
## Release 2017-03-21

### Amazon.Lambda.Tools (1.4.0)
* Update to latest AWSSDK.Core to pull in latest AWS SDK for .NET [credential enhancments](https://aws.amazon.com/blogs/developer/aws-sdk-dot-net-credential-profiles/)
* Promote to GA release
### Amazon.Lambda.DynamoDBEvents (1.0.1)
* Update to latest version of AWS SDK for .NET
### Amazon.Lambda.KinesisEvents (1.0.1)
* Update to latest version of AWS SDK for .NET
### Amazon.Lambda.S3Events (1.0.1)
* Update to latest version of AWS SDK for .NET
### Amazon.Lambda.Templates (1.0.0)
* New NuGet package adding the Lambda blueprints to the dotnet CLI
  * To install: dotnet new -i Amazon.Lambda.Templates::*
### Blueprints
* Updated dependencies to latest AWS SDK for .NET dependencies and Amazon.Lambda.* dependencies.
* Fixed issue with **AspNetCoreWebAPI** not correctly checking if the BucketName property was set.
* Migrate blueprints to new msbuild project system.
* Projects all migrated Visual Studio 2017
	
## Release 2017-02-20

### Amazon.Lambda.Tools (1.3.0-preview1)
* Flatten the publish runtime folder to help the Lambda runtime resolve platform specific dependencies. This also reduces the size of the Lambda package bundle by only including the dependencies needed for the Lambda environment.
### Blueprints
* Updated all blueprints to version 1.3.0-preview1 of Amazon.Lambda.Tools

## Release 2017-02-11

### Amazon.Lambda.Tools (1.2.1-preview1)
* Pull request [#60](https://github.com/aws/aws-lambda-dotnet/pull/60), fixing issue with subnet ids in aws-lambda-tools-defaults.json . Thanks to [Scott Brady](https://github.com/scott-brady) for the pull request.
### Blueprints
* Updated all blueprints to version 1.2.1-preview1 of Amazon.Lambda.Tools

## Release 2017-02-10

### Amazon.Lambda.Tools (1.2.0-preview1)
* Reworked how the AWS region is determined. New logic follows the following pattern:
  * Use region specified on command line
  * Use region specified in aws-lambda-tools-defaults.json
  * Determine region using the AWS SDK for .NET [default region lookup](https://aws.amazon.com/blogs/developer/updates-to-credential-and-region-handling/)
  * Ask user for region
* Added **--cloudformation-role** commandline switch for the **deploy-serverless** command to specify an IAM role for 
CloudFormation to assume when creating or updating CloudFormation stacks.
* Changed **deploy-serverless** command to upload CloudFormation template directly to CloudFormation instead of S3 if the template size was less then 50,000 bytes.
This was done to help users that were running into issues with the presigned URL to the template being too long for CloudFormation.
### Amazon.Lambda.AspNetCoreServer (0.9.0-preview1)
* Add **EnableRequestLogging** and **EnableResponseLogging** properties to **APIGatewayProxyFunction**. If set to 
true the request and/or response will be logged to the associated CloudWatchLogs. This required the method signature for
**FunctionHandlerAsync** to be changed to use Streams so the raw request data could be captured. An extension method
was added in the **Amazon.Lambda.TestUtilities** namespace with the previous signature to help testing.

## Release 2017-01-27

### Amazon.Lambda.APIGatewayEvents (1.0.2)
* Pull request [#42](https://github.com/aws/aws-lambda-dotnet/pull/42), adding custom authorizer support. Thanks to [Justin Yancey](https://github.com/thedevopsmachine) for the pull request.
### Amazon.Lambda.AspNetCoreServer (0.8.6-preview1)
* Pull request [#44](https://github.com/aws/aws-lambda-dotnet/pull/44), improve error handling.
* Updated dependency of Amazon.Lambda.APIGatewayEvents to version 1.0.2.

## Release 2017-01-26

### Amazon.Lambda.AspNetCoreServer (0.8.5-preview1)
* Fixed issue with accessing a closed response stream.
  
## Release 2017-01-25

### Blueprints
* Added a preview ASP.NET Core Web API blueprint

## Release 2017-01-17

### Amazon.Lambda.AspNetCoreServer (0.8.4-preview1)
* Pull request [#33](https://github.com/aws/aws-lambda-dotnet/pull/33), fixing issue with returning error HTTP status codes. Thanks to [Travis Gosselin](https://github.com/travisgosselin) for the pull request.

## Release 2017-01-14

### Amazon.Lambda.AspNetCoreServer (0.8.3-preview1)
* Pull request [#32](https://github.com/aws/aws-lambda-dotnet/pull/32), refactoring base Lambda function to allow sub types to customize the function invoke handling. Thanks to [Justin Yancey](https://github.com/thedevopsmachine) for the pull request.

## Release 2017-01-06

### Amazon.Lambda.SimpleEmailEvents (1.0.0)
* New package for AWS Lambda event types for Amazon Simple Email Service. Thanks to [Tom Winzig](https://github.com/winzig) for the pull request.

## Release 2017-01-06

### Amazon.Lambda.Tools (1.1.0-preview1)
* Added command line switches **--config-file** and **--persist-config-file** allowing use of alternative default config files and persisting the current values to the config file.
* Added **--package** switch to **deploy-function** and **deploy-serverless** commands to use a precompiled application package that skips building the project.
* Fixed issue with **dotnet lambda package** when output file was not a full file path.
### Blueprints
* Updated all blueprints to version 1.1.0-preview1 of Amazon.Lambda.Tools


## Release 2016-12-21

### Amazon.Lambda.Tools (1.0.4-preview1)
* Fixed issue with zipping application bundles from paths that contain spaces
### Amazon.Lambda.APIGatewayEvents (1.0.1)
* Added IsBase64Encoded property to APIGatewayProxyRequest
### Amazon.Lambda.AspNetCoreServer (0.8.2-preview1)
* Added support for marshaling request body
### Blueprints
* Updated EmptyServerless and DynamoDBBlogAPI to 1.0.1 of Amazon.Lambda.APIGatewayEvents
* Updated all blueprints to version 1.0.4-preview1 of Amazon.Lambda.Tools

## Release 2016-12-16

### Amazon.Lambda.Tools (1.0.3-preview1)
* Fixed issue with quoted strings in users path while searching for the dotnet CLI
### Blueprints
* DynamoDBBlogAPI: Change content-type to text/plain for AddBlogAsync which returns the ID of the new blog
* Updated all blueprints to version 1.0.3-preview1 of Amazon.Lambda.Tools

## Release 2016-12-12

### Amazon.Lambda.Tools (1.0.2-preview1)
* Add CAPABILITY_NAMED_IAM capability when performing serverless deployment
* Add ability to disable capabilities for serverless deployment using the switch **--disable-capabilities**
### Blueprints
* Updated DynamoDBBlogAPI to map GetBlogAsync in serverless.template
* Updated all blueprints to version 1.0.2-preview1 of Amazon.Lambda.Tools

## Release 2016-12-07

## Amazon.Lambda.Tools (1.0.1-preview1)
* Added PowerUserAccess as a managed policy used to create new IAM roles
* Added support for setting dead letter target with new switch **--dead-letter-target-arn**
## Blueprints
* Added new "Detect Label Images" blueprint
