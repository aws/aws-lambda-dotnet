### Release 2017-12-22 23:50
* **Amazon.Lambda.S3Events (1.0.2)**
    * Updated to use latest AWSSDK.S3 to fix issue with comparing EventName property to the SDK EventType constants.
* **Blueprints**
    * Update S3 blueprint to use latest Amazon.Lambda.S3Events.
    *  **Amazon.Lambda.Templates (1.4.5)** released with latest blueprints.


### Release 2017-12-22 23:50
* **Amazon.Lambda.CloudWatchLogsEvents (1.0.0)**
    * Pull request [#188](https://github.com/aws/aws-lambda-dotnet/pull/188). New event package for CloudWatch Logs. Thanks to [Toshinori Sugita](https://github.com/toshi0607).
* **Amazon.Lambda.Tools (1.9.0)**
    * Added new **--apply-defaults** switch. If set to true from either the command 
    line or aws-lambda-tools-defaults.json, values from the aws-lambda-tools-defaults.json 
    file will be applied when updating an existing function. By default function 
    configuration values from aws-lambda-tools-defaults.json are ignored when 
    updating an existing function to avoid unattended changes to production functions.
* **Blueprints**
    * Update dependency reference for **Amazon.Lambda.Tools** to 1.9.0
    *  **Amazon.Lambda.Templates (1.4.4)** released with latest blueprints.

### Release 2017-12-20 18:30
* **Amazon.Lambda.LexEvents (1.0.2)**
    * Add slot details and request attributes to LexEvent.
* **Blueprints**
    * Update dependency reference for **Amazon.Lambda.LexEvents**
    *  **Amazon.Lambda.Templates (1.4.3)** released with latest blueprints.

### Release 2017-12-12 00:30
* **Amazon.Lambda.LexEvents (1.0.1)**
    * Pull request [#184](https://github.com/aws/aws-lambda-dotnet/pull/184), fixing issue with InputTranscript property on the wrong class. Thanks to [jmeijon](https://github.com/jmeijon).
* **Blueprints**
    * Update dependency reference for **Amazon.Lambda.LexEvents**
    *  **Amazon.Lambda.Templates (1.4.2)** released with latest blueprints.

### Release 2017-10-12 18:00
* **Amazon.Lambda.Tools (1.8.1)**
    * Fixed issue deploying to AWS Lambda in the US GovCloud region.

### Release 2017-09-15 23:00
* **Amazon.Lambda.Tools (1.8.0)**
    * Add support using YAML formatted CloudFormation templates.
* **Blueprints**
    * Update dependency reference for **Amazon.Lambda.Tools**
    *  **Amazon.Lambda.Templates (1.4.1)** released with latest blueprints.


### Release 2017-08-23 20:30
* **Amazon.Lambda.Tools (1.7.1)**
    * Fixed error message when missing required parameters.
    * Improved logic for searching for the current dotnet CLI
    * Added **--disable-version-check** switch for users that want to try running with libraries that declare .NET Core 1.1 dependencies.
        * Note, running with these libraries can have unforeseen side effects so only recommended for advanced cases with lots of testing done on the functions.


### Release 2017-08-22 18:00
* **Blueprints**
    * Added new AWS Step Function Hello World blueprint.
    *  **Amazon.Lambda.Templates (1.4.0)** released with latest blueprints.

### Release 2017-07-26 21:30
* **Amazon.Lambda.Tools (1.7.0)**
    * Add **--disable-interactive** switch for use in CI systems to prevent the tooling from blocking waiting missing required parameters.
    * Fixed issue with serverless deployment that was not returning a failed exit code when the CloudFormation stack failed to be created.
* **Blueprints**
    * Update dependency reference for **Amazon.Lambda.Tools**
    *  **Amazon.Lambda.Templates (1.3.1)** released with latest blueprints.

### Release 2017-06-23 00:30
* **Amazon.Lambda.AspNetCoreServer (0.10.2-preview1)**
	* Fixed issue computing resource path for custom domains.
	* Fixed issue with resource path not being URL decoded.


### Release 2017-06-01 06:21
* **Amazon.Lambda.Tools (1.6.0)**
    * Add new **package-ci** command to use for deployment with [AWS CodePipeline](https://aws.amazon.com/codepipeline/). This is the .NET Core Lambda equivalent of the AWS CLI command [aws cloudformation package](http://docs.aws.amazon.com/cli/latest/reference/cloudformation/package.html).
    * Add **--template-substitutions** option to **deploy-serverless** and **package-ci** commands allowing parts of the serverless-template to be defined in separate files.
    * Fixed issue with dead letter queue configuration getting cleared out during redeploy.
	* Pull request [#117](https://github.com/aws/aws-lambda-dotnet/pull/117), displaying tool version number. Thanks to [Corey Coto](https://github.com/coreycoto).
	* Add error check when deploying from Linux and the `zip` command line utility is not found. The `zip` tool is required on Linux to maintain file permissions.
* **Amazon.Lambda.Logging.AspNetCore (1.1.0)**
	* Pull request [#110](https://github.com/aws/aws-lambda-dotnet/pull/110), adding support for log category wildcards. Thanks to [Cris Barbero](https://github.com/cfbarbero).
* **Blueprints**
    * Update dependency reference for **Amazon.Lambda.Tools** and **Amazon.Lambda.Logging.AspNetCore**
    *  **Amazon.Lambda.Templates (1.2.0)** released with latest blueprints.


### Release 2017-04-28 18:21
* **Amazon.Lambda.AspNetCoreServer (0.10.1-preview1)**
	* Fixed issue with not registering the JSON serializer.
* **Blueprints**
    * Updated ASP.NET Core WebAPI blueprints to use version **Amazon.Lambda.AspNetCoreServer (0.10.1-preview1)**.
    *  **Amazon.Lambda.Templates (1.2.1)** released with latest blueprints.
      
### Release 2017-04-27 18:00
* **Amazon.Lambda.KinesisFirehoseEvents (1.0.0)**
	* New package for AWS Lambda event types for Amazon Kinesis Firehose.
* **Blueprints**
    * New Amazon Kinesis Firehose blueprint.
    *  **Amazon.Lambda.Templates (1.2.0)** released with latest blueprints.


### Release 2017-04-26 05:30
* **Amazon.Lambda.Tools (1.5.0)**
	* Added validation to stop deploying .NET Core 1.0 AWS Lambda functions if the project includes .NET Core 1.1 dependencies.
* **Amazon.Lambda.LexEvents (1.0.0)**
	* New package for AWS Lambda event types for Amazon Lex	
* **Amazon.Lambda.Serialization.Json (1.1.0)**
	* Added serialization logging which can be enabled by setting the environment variable **LAMBDA_NET_SERIALIZER_DEBUG = true**
* **Amazon.Lambda.AspNetCoreServer (0.10.0-preview1)**
	* Pull request [#75](https://github.com/aws/aws-lambda-dotnet/pull/75), adding binary support. Check the [README.md](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.AspNetCoreServer/README.md) for details. Thanks to [Eugene Bekker](https://github.com/ebekker).
    * Pull request [#89](https://github.com/aws/aws-lambda-dotnet/pull/89), populate RemoteIpAddress and RemotePort on HttpContext.Connection. Thanks to [Marcus Lum](https://github.com/Marcus-L).
    * Added the **APIGatewayProxyRequest** and **ILambdaContext** to the **HttpContext.Items** collection with the collection keys **APIGatewayRequest** and **LambdaContext**.
	* Removed request and response logging and rely on the new logging available in **Amazon.Lambda.Serialization.Json**. This allowed the method signature to be changed back to directly use the **Amazon.Lambda.APIGatewayEvents** class.
* **Amazon.Lambda.APIGatewayEvents (1.1.0)**
    * Added IsBase64Encoded property to APIGatewayProxyResponse as part of the binary support for **Amazon.Lambda.AspNetCoreServer**.
* **Blueprints**
    * New Amazon Lex blueprint for the Lex Book Trip getting started [sample](http://docs.aws.amazon.com/lex/latest/dg/ex-book-trip.html).
    * Updated all blueprints to latest version of the Amazon NuGet packages.
    * **Amazon.Lambda.Templates (1.1.0)** released with latest blueprints.
 
### Release 2017-03-21 06:00
* **Amazon.Lambda.Tools (1.4.0)**
	* Update to latest AWSSDK.Core to pull in latest AWS SDK for .NET [credential enhancments](https://aws.amazon.com/blogs/developer/aws-sdk-dot-net-credential-profiles/)
	* Promote to GA release
* **Amazon.Lambda.DynamoDBEvents (1.0.1)**
	* Update to latest version of AWS SDK for .NET
* **Amazon.Lambda.KinesisEvents (1.0.1)**
	* Update to latest version of AWS SDK for .NET
* **Amazon.Lambda.S3Events (1.0.1)**
	* Update to latest version of AWS SDK for .NET
* **Amazon.Lambda.Templates (1.0.0)**
	* New NuGet package adding the Lambda blueprints to the dotnet CLI
		* To install: dotnet new -i Amazon.Lambda.Templates::*
* **Blueprints**
	* Updated dependencies to latest AWS SDK for .NET dependencies and Amazon.Lambda.* dependencies.
	* Fixed issue with **AspNetCoreWebAPI** not correctly checking if the BucketName property was set.
    * Migrate blueprints to new msbuild project system.
* Projects all migrated Visual Studio 2017
	
### Release 2017-02-20 20:30
* **Amazon.Lambda.Tools (1.3.0-preview1)**
	* Flatten the publish runtime folder to help the Lambda runtime resolve platform specific dependencies. This also reduces the size of the Lambda package bundle by only including the dependencies needed for the Lambda environment.
* **Blueprints**
	* Updated all blueprints to version 1.3.0-preview1 of Amazon.Lambda.Tools

### Release 2017-02-11 05:30 UTC
* **Amazon.Lambda.Tools (1.2.1-preview1)**
  * Pull request [#60](https://github.com/aws/aws-lambda-dotnet/pull/60), fixing issue with subnet ids in aws-lambda-tools-defaults.json . Thanks to [Scott Brady](https://github.com/scott-brady) for the pull request.
* **Blueprints**
	* Updated all blueprints to version 1.2.1-preview1 of Amazon.Lambda.Tools

### Release 2017-02-10 06:00 UTC
* **Amazon.Lambda.Tools (1.2.0-preview1)**
  * Reworked how the AWS region is determined. New logic follows the following pattern:
    * Use region specified on command line
    * Use region specified in aws-lambda-tools-defaults.json
    * Determine region using the AWS SDK for .NET [default region lookup](https://aws.amazon.com/blogs/developer/updates-to-credential-and-region-handling/)
    * Ask user for region
  * Added **--cloudformation-role** commandline switch for the **deploy-serverless** command to specify an IAM role for 
CloudFormation to assume when creating or updating CloudFormation stacks.
  * Changed **deploy-serverless** command to upload CloudFormation template directly to CloudFormation instead of S3 if the template size was less then 50,000 bytes.
This was done to help users that were running into issues with the presigned URL to the template being too long for CloudFormation.
* **Amazon.Lambda.AspNetCoreServer (0.9.0-preview1)**
  * Add **EnableRequestLogging** and **EnableResponseLogging** properties to **APIGatewayProxyFunction**. If set to 
true the request and/or response will be logged to the associated CloudWatchLogs. This required the method signature for
**FunctionHandlerAsync** to be changed to use Streams so the raw request data could be captured. An extension method
was added in the **Amazon.Lambda.TestUtilities** namespace with the previous signature to help testing.

### Release 2017-01-27 18:30 UTC
* **Amazon.Lambda.APIGatewayEvents (1.0.2)**
  * Pull request [#42](https://github.com/aws/aws-lambda-dotnet/pull/42), adding custom authorizer support. Thanks to [Justin Yancey](https://github.com/thedevopsmachine) for the pull request.
* **Amazon.Lambda.AspNetCoreServer (0.8.6-preview1)**
  * Pull request [#44](https://github.com/aws/aws-lambda-dotnet/pull/44), improve error handling.
  * Updated dependency of Amazon.Lambda.APIGatewayEvents to version 1.0.2.

### Release 2017-01-26 06:30 UTC
* **Amazon.Lambda.AspNetCoreServer (0.8.5-preview1)**
  * Fixed issue with accessing a closed response stream.
  
### Release 2017-01-25 00:00 UTC
* **Blueprints**
  * Added a preview ASP.NET Core Web API blueprint

### Release 2017-01-17 08:00 UTC
* **Amazon.Lambda.AspNetCoreServer (0.8.4-preview1)**
  * Pull request [#33](https://github.com/aws/aws-lambda-dotnet/pull/33), fixing issue with returning error HTTP status codes. Thanks to [Travis Gosselin](https://github.com/travisgosselin) for the pull request.

### Release 2017-01-14 20:45 UTC
* **Amazon.Lambda.AspNetCoreServer (0.8.3-preview1)**
  * Pull request [#32](https://github.com/aws/aws-lambda-dotnet/pull/32), refactoring base Lambda function to allow sub types to customize the function invoke handling. Thanks to [Justin Yancey](https://github.com/thedevopsmachine) for the pull request.

### Release 2017-01-06 20:45 UTC
* **Amazon.Lambda.SimpleEmailEvents (1.0.0)**
  * New package for AWS Lambda event types for Amazon Simple Email Service. Thanks to [Tom Winzig](https://github.com/winzig) for the pull request.

### Release 2017-01-06 00:30 UTC
* **Amazon.Lambda.Tools (1.1.0-preview1)**
  * Added command line switches **--config-file** and **--persist-config-file** allowing use of alternative default config files and persisting the current values to the config file.
  * Added **--package** switch to **deploy-function** and **deploy-serverless** commands to use a precompiled application package that skips building the project.
  * Fixed issue with **dotnet lambda package** when output file was not a full file path.
* **Blueprints**
	* Updated all blueprints to version 1.1.0-preview1 of Amazon.Lambda.Tools


### Release 2016-12-21 08:00 UTC
* **Amazon.Lambda.Tools (1.0.4-preview1)**
  * Fixed issue with zipping application bundles from paths that contain spaces
* **Amazon.Lambda.APIGatewayEvents (1.0.1)**
  * Added IsBase64Encoded property to APIGatewayProxyRequest
* **Amazon.Lambda.AspNetCoreServer (0.8.2-preview1)**
  * Added support for marshaling request body
* **Blueprints**
    * Updated EmptyServerless and DynamoDBBlogAPI to 1.0.1 of Amazon.Lambda.APIGatewayEvents
    * Updated all blueprints to version 1.0.4-preview1 of Amazon.Lambda.Tools

### Release 2016-12-16 01:36 UTC
* **Amazon.Lambda.Tools (1.0.3-preview1)**
	* Fixed issue with quoted strings in users path while searching for the dotnet CLI
* **Blueprints**
	* DynamoDBBlogAPI: Change content-type to text/plain for AddBlogAsync which returns the ID of the new blog
    * Updated all blueprints to version 1.0.3-preview1 of Amazon.Lambda.Tools

### Release 2016-12-12 07:30 UTC
* **Amazon.Lambda.Tools (1.0.2-preview1)**
	* Add CAPABILITY_NAMED_IAM capability when performing serverless deployment
	* Add ability to disable capabilities for serverless deployment using the switch **--disable-capabilities**
* **Blueprints**
	* Updated DynamoDBBlogAPI to map GetBlogAsync in serverless.template
	* Updated all blueprints to version 1.0.2-preview1 of Amazon.Lambda.Tools

### Release 2016-12-07 17:30 UTC
* **Amazon.Lambda.Tools (1.0.1-preview1)**
    * Added PowerUserAccess as a managed policy used to create new IAM roles
    * Added support for setting dead letter target with new switch **--dead-letter-target-arn**
* **Blueprints**
    *  Added new "Detect Label Images" blueprint