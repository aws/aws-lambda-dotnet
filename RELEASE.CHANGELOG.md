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
* **Amazon.Lambda.Tools (1.0.4-preview1)**
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