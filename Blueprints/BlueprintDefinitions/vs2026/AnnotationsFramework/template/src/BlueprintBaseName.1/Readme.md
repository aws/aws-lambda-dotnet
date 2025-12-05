# Amazon.Lambda.Annotations

Lambda Annotations is a programming model for writing .NET Lambda functions. At a high level the programming model allows
idiomatic .NET coding patterns. [C# Source Generators](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) are used to bridge the 
gap between the Lambda programming model to the Lambda Annotations programming model without adding any performance penalty.

Topics:
* [Getting Started](#getting-started)
* [How does Lambda Annotations work?](#how-does-lambda-annotations-work)
* [Dependency Injection integration](#dependency-injection-integration)
* [Synchronizing CloudFormation template](#synchronizing-cloudFormation-template)
* [Getting build information](#getting-build-information)
* [Amazon API Gateway example](#amazon-api-gateway-example)
* [Amazon S3 example](#amazon-s3-example)
* [Lambda .NET Attributes Reference](#lambda-net-attributes-reference)

## How does Lambda Annotations work?

The default experience for writing .NET Lambda functions is to write a .NET method that takes in an event object. From there boiler plate code is written to
parse the data out of the event object and synchronize the CloudFormation template to define the Lambda function and the .NET method to call
for each event. Here is a simplistic example of a .NET Lambda function that acts like a calculator plus method using the default Lambda programming model. It responds to 
an API Gateway REST API, pulls the operands from the resource paths, does the 
addition and returns back an API Gateway response.

```csharp
public class Functions
{
    public APIGatewayProxyResponse LambdaMathPlus(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.PathParameters.TryGetValue("x", out var xs))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest
            };
        }
        if (!request.PathParameters.TryGetValue("y", out var ys))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.BadRequest
            };
        }

        var x = int.Parse(xs);
        var y = int.Parse(ys);

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Body = (x + y).ToString(),
            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
        };
    } 
}
```

Using Lambda Annotations the same Lambda function can remove a lot of that boiler plate code and write the method like this.

```csharp
public class Functions
{
    [LambdaFunction]
    [RestApi("/plus/{x}/{y}")]
    public int Plus(int x, int y)
    {
        return x + y;
    }
}
```

Lambda Annotations uses C# source generators to generate that boiler plate code to bridge the gap between the default Lambda programming model to Lambda Annotations programming model at compile time.
In addition the source generator also synchronizes the CloudFormation template to declare all of the .NET methods with the `LambdaFunction` attribute as 
Lambda functions in the CloudFormation template.

## Getting started

To get started with Lambda annotations a Lambda blueprint is available. For Visual Studio users the blueprint can be 
accessed using the [AWS Toolkit for Visual Studio](https://marketplace.visualstudio.com/items?itemName=AmazonWebServices.AWSToolkitforVisualStudio2022). 
For non-Visual Studio users the [Amazon.Lambda.Templates](https://www.nuget.org/packages/Amazon.Lambda.Templates) 
NuGet package is available for creating .NET Lambda projects from the .NET CLI.


### Visual Studio 2022

To get started with Visual Studio install [AWS Toolkit for Visual Studio](https://marketplace.visualstudio.com/items?itemName=AmazonWebServices.AWSToolkitforVisualStudio2022) 
extension. Once installed, a pre-configured Lambda Annotations project can be created using the following steps:

* Select **Create a new project**
* In the template search box enter **AWS Serverless**
* Select the **AWS Serverless Application (.NET Core C#)** from the search result and click **Next**
* Name the project and click **Create**
* The **Select Blueprint** wizard will be displayed for choosing the initial content of the project.
* Select the **Annotations Framework** blueprint and click **Finish**

### .NET CLI

.NET Lambda projects can be be created using the .NET CLI's `dotnet new` command. To create a project using the
Lambda Annotations library run the following steps from a terminal.

* Run `dotnet new install Amazon.Lambda.Templates` to install the AWS Lambda templates into the .NET CLI
* Run `dotnet new serverless.Annotations --output FirstAnnotationsProject` to create a project using Lambda Annotations

This will create a project in a sub directory of the current director called `FirstAnnotationsProject`. The directory 
will contain both a Lambda project using Annotations as well as a unit test project.

### The sample project

The sample project contains the following files:

* **Functions.cs** - Defines a collection of REST API Lambda functions using Lambda Annotation.
* **Startup.cs** - Where services can be registered for dependency injection into the Lambda functions.
* **serverless.template** - CloudFormation template used to deploy the Lambda functions. The Lambda Annotations library 
will automatically sync the functions defined in the project in the CloudFormation template.
* **aws-lambda-tools-defaults.json** - Config file for default settings used for deployment.

To reset to an empty project delete the code in the `Functions` class and recompile the project. The Lambda
Annotations library will remove all of the Lambda function declarations from the CloudFormation template.
If the project will not include any Lambda functions that use API Gateway's HTTP API event sources then the `ApiURL` 
output parameter should be manually removed from the CloudFormation template.

### Deployment

The Lambda Annotations library requires no special tooling for deployment. Any tool that supports CloudFormation-based 
.NET Lambda function deployment is compatible with Lambda Annotations. This includes 
[AWS Toolkit for Visual Studio](https://marketplace.visualstudio.com/items?itemName=AmazonWebServices.AWSToolkitforVisualStudio2022), 
[Amazon.Lambda.Tools](https://github.com/aws/aws-extensions-for-dotnet-cli/#aws-lambda-amazonlambdatools) for the .NET CLI and [AWS SAM CLI](https://aws.amazon.com/serverless/sam/).

For the AWS Toolkit for Visual Studio deployment can be initiated by right clicking on the Lambda project in the 
Solution Explorer and selecting **Publish to AWS Lambda...**. This will launch a wizard to configure the name
of the CloudFormation stack and a S3 bucket used for storage of the compiled Lambda function deployment bundles.

Amazon.Lambda.Tools is a .NET CLI global tool that can be install using the command 
`dotnet tool install --global Amazon.Lambda.Tools`. Once installed deployment can be initiated by running the command
`dotnet lambda deploy-serverless` in the directory of the Lambda project.


### Adding Lambda Annotations to an existing project

Lambda Annotations can be added to existing projects. Lambda Annotations does require that deployment of existing 
projects is done using a CloudFormation template. In the future Lambda Annotations may support
other deployment technologies.

To get started with Lambda Annotations in existing projects add a reference to the [Amazon.Lambda.Annotations](https://www.nuget.org/packages/Amazon.Lambda.Annotations/)
NuGet package. Then decorate C# methods that should be exposed as Lambda Functions with the `LambdaFunction` attribute.
If the `LambdaFunction` attribute is added to a method that was already declared in the CloudFormation template the
original declaration should be manually removed.


## Dependency Injection integration

Lambda Annotations supports dependency injection. A class can be marked with a `LambdaStartup` attribute. The class will 
have a `ConfigureServices` method for configuring services.

The services can be injected by either constructor injection or using the `FromServices` attribute on a method parameter of
the function decorated with the `LambdaFunction` attribute.

Services injected via the constructor have a lifecycle for the length of the Lambda compute container. For each Lambda 
invocation a scope is created and the services injected using the `FromServices` attribute are created within the scope.

Example startup class:
```csharp
[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAWSService<Amazon.S3.IAmazonS3>();
        services.AddScoped<ITracker, DefaultTracker>();
    }
}
```

Example function using DI:
```csharp
public class Functions
{
    IAmazonS3 _s3Client;

    public Functions(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }


    [LambdaFunction]
    [HttpApi(LambdaHttpMethod.Put, "/process/{name}")]
    public async Task Process([FromServices] ITracker tracker, string name, [FromBody] string data)
    {
        tracker.Record();

        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = "storage-bucket",
            Key = name,
            ContentBody = data
        });
    }
}
```

## Synchronizing CloudFormation template

When the .NET project is compiled the Lambda Annotation source generator will synchronize all of the C# methods with the `LambdaFunction` attribute in the 
project's CloudFormation template. Support is available for both JSON and YAML based CloudFormation templates.
The source generator identifies the CloudFormation template for the project by looking at the `template` property in the `aws-lambda-tools-defaults.json` 
file. If the `template` property is absent, the source generator will default to `serverless.template` and create the file if it does not exist.

The source generator synchronizes Lambda resources in the CloudFormation template. The template can still be edited to add additional AWS resources or to further customize the Lambda functions, such as adding other event sources that are not currently supported by Lambda Annotations attributes.

When a .NET Method is synchronized to the CloudFormation template the source generator adds the `Tool` metadata property shown below. This metadata 
links the CloudFormation resource to the source generator. If the `LambdaFunction` attribute is removed the C# method then the source generator 
will remove the CloudFormation resource. To unlink the CloudFormation resource from the source generator
remove the `Tool` metadata property.

```

  ...

"CloudCalculatorFunctionsAddGenerated": {
    "Type": "AWS::Serverless::Function",
    "Metadata": {
        "Tool": "Amazon.Lambda.Annotations",
        "SyncedEvents": [
            "RootGet"
        ]
    },
    "Properties": {
    "Runtime": "dotnet8",
    "CodeUri": ".",

  ...
}
```

The `LambdaFunction` attribute contains properties that map to properties of the CloudFormation resource. For example in this snippet the Lambda function's `MemorySize` and `Timeout` 
properties are set in the C# code. The source generator will synchronize these properties into the CloudFormation template.
```csharp
[LambdaFunction(MemorySize = 512, Timeout = 55)]
[HttpApi(LambdaHttpMethod.Get, "/add/{x}/{y}")]
public int Add(int x, int y, ILambdaContext context)
{
    context.Logger.LogInformation($"{x} plus {y} is {x + y}");
    return x + y;
}
```

Some CloudFormation properties are not set to a specific value but instead reference another resource or parameter defined in the CloudFormation template. To indicate the value for a 
property of the .NET attribute is meant to reference another CloudFormation resource prefix the value with `@`. Here is an example of the `Role` for the Lambda function to reference
an IAM role defined in the CloudFormation template as `LambdaRoleParameter`

```csharp
public class Functions
{
    [LambdaFunction( Role="@LambdaRoleParameter")]
    [RestApi("/plus/{x}/{y}")]
    public int Plus(int x, int y)
    {
        return x + y;
    }
}
```

```json
    "CloudCalculatorFunctionsAddGenerated": {
      "Type": "AWS::Serverless::Function",
      "Metadata": {
        "Tool": "Amazon.Lambda.Annotations",
        "SyncedEvents": [
          "RootGet"
        ]
      },
      "Properties": {
        "Runtime": "dotnet8",

...
        "Role": {
          "Fn::GetAtt": [
            "LambdaRoleParameter",
            "Arn"
          ]
        }
      }
    },
```

By default, Lambda Annotations will update the CloudFormation template's [description](https://docs.aws.amazon.com/AWSCloudFormation/latest/UserGuide/template-description-structure.html) 
field to include the version of Lambda Annotations that was used to modify the template.
```
{
  "AWSTemplateFormatVersion": "2010-09-09",
  "Transform": "AWS::Serverless-2016-10-31",
  "Description": "An AWS Serverless Application. This template is partially managed by Amazon.Lambda.Annotations (v0.9.0.0).",
   ...
```

This description allow AWS to record the version and the usage of the Lambda Annotations framework in order to improve its quality. We record details at the CloudFormation stack level, and do not identify the application, library, or tool that was deployed. Note that we do not record any personal information, such as usernames, email addresses or sensitive project-level information. 

If you do not want AWS to track the usage of the library, please set the following in your project (csproj) file:
```
<PropertyGroup>
  <AWSSuppressLambdaAnnotationsTelemetry>true</AWSSuppressLambdaAnnotationsTelemetry>
</PropertyGroup>
```

## Amazon API Gateway example

This example creates a REST API through Amazon API Gateway that exposes the common arithmetic operations. 

To avoid putting business logic inside the REST API a separate calculator service is created to encapsulate the logic of the arithmetic operations. Here is both the 
calculator service's interface and default implementation.

```csharp
public interface ICalculatorService
{
    int Add(int x, int y);

    int Subtract(int x, int y);

    int Multiply(int x, int y);

    int Divide(int x, int y);
}

public class DefaultCalculatorService : ICalculatorService
{
    public int Add(int x, int y) => x + y;

    public int Subtract(int x, int y) => x - y;

    public int Multiply(int x, int y) => x * y;

    public int Divide(int x, int y) => x / y;
}
```

The startup class contains the `LambdaStartup` attribute identifying it as the class to configure the services registered in the dependency injection framework. 
Here the `ICalculatorService` is registered as a singleton service in the collection of services.

```csharp
[LambdaStartup]
public class Startup
{

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICalculatorService, DefaultCalculatorService>();
    }
}
```

Since the `ICalculatorService` is registered as a singleton the service is injected into the Lambda function via the constructor. 
If the registered service is registered as scoped or transient and a new instance is needed for each Lambda invocation then the
`FromServices` attribute should be used on a method parameter of the Lambda function. 

```csharp
public class Functions
{
    ICalculatorService _calculatorService;
    public Functions(ICalculatorService calculatorService)
    {
        _calculatorService = calculatorService;
    }

    ...
```

For each arithmetic operation a separate C# method is added containing the `LambdaFunction` attribute. The `LambdaFunction` attribute
ensures the dependency injection framework is hooked up to the Lambda function and the Lambda function will be declared in the 
CloudFormation template. 

Since these Lambda functions are responding to API Gateway events the `HttpApi` attribute is added 
to register the event source in CloudFormation along with the HTTP verb and resource path. The `HttpApi` attribute also enables 
mapping of the HTTP request components to method parameters. In this case the operands used for the arithmetic operations are 
mapped from the resource path. Checkout the list of Lambda attributes in the reference section to see how to map other components
of the HTTP request to method parameters.

```csharp
[LambdaFunction()]
[HttpApi(LambdaHttpMethod.Get, "/add/{x}/{y}")]
public int Add(int x, int y, ILambdaContext context)
{
    context.Logger.LogInformation($"{x} plus {y} is {x + y}");
    return _calculatorService.Add(x, y);
}

[LambdaFunction()]
[HttpApi(LambdaHttpMethod.Get, "/subtract/{x}/{y}")]
public int Subtract(int x, int y, ILambdaContext context)
{
    context.Logger.LogInformation($"{x} subtract {y} is {x - y}");
    return _calculatorService.Subtract(x, y);
}

[LambdaFunction()]
[HttpApi(LambdaHttpMethod.Get, "/multiply/{x}/{y}")]
public int Multiply(int x, int y, ILambdaContext context)
{
    context.Logger.LogInformation($"{x} multiply {y} is {x * y}");
    return _calculatorService.Multiply(x, y);
}

[LambdaFunction()]
[HttpApi(LambdaHttpMethod.Get, "/divide/{x}/{y}")]
public int Divide(int x, int y, ILambdaContext context)
{
    context.Logger.LogInformation($"{x} divide {y} is {x / y}");
    return _calculatorService.Divide(x, y);
}
```

For each `LambdaFunction` declared the source generator will update the CloudFormation template with the corresponding resource. 
The Lambda CloudFormation resource has the `Handler` property set to the generated method by Lambda Annotations. This generated
method is where Lambda Annotations bridges the gap between the Lambda Annotation programming model and the Lambda programming model.
The `HttpApi` attribute also adds the API Gateway event source.

```json
    "CloudCalculatorFunctionsAddGenerated": {
      "Type": "AWS::Serverless::Function",
      "Metadata": {
        "Tool": "Amazon.Lambda.Annotations",
        "SyncedEvents": [
          "RootGet"
        ]
      },
      "Properties": {
        "Runtime": "dotnet8",
        "CodeUri": ".",
        "MemorySize": 512,
        "Timeout": 30,
        "PackageType": "Zip",
        "Handler": "CloudCalculator::CloudCalculator.Functions_Add_Generated::Add",
        "Events": {
          "RootGet": {
            "Type": "HttpApi",
            "Properties": {
              "Path": "/add/{x}/{y}",
              "Method": "GET",
              "PayloadFormatVersion": "2.0"
            }
          }
        }
      }
    },
```

Here is an example of the generated code from the source generator for the `Add` Lambda function. The generated code wraps around the 
C# method that has the `LambdaFunction` attribute. It takes care of
configuring the dependency injection, gets the parameters from the API Gateway event and invokes the wrapped `LambdaFunction`. This code snippet is here for 
informational purposes, as a user of the Lambda Annotations framework this code should not be needed to be seen.

```csharp
public class Functions_Add_Generated
{
    private readonly ServiceProvider serviceProvider;

    public Functions_Add_Generated()
    {
        var services = new ServiceCollection();

        // By default, Lambda function class is added to the service container using the singleton lifetime
        // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
        services.AddSingleton<Functions>();

        var startup = new CloudCalculator.Startup();
        startup.ConfigureServices(services);
        serviceProvider = services.BuildServiceProvider();
    }

    public Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse Add(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
    {
        // Create a scope for every request,
        // this allows creating scoped dependencies without creating a scope manually.
        using var scope = serviceProvider.CreateScope();
        var functions = scope.ServiceProvider.GetRequiredService<Functions>();

        var validationErrors = new List<string>();

        var x = default(int);
        if (request.PathParameters?.ContainsKey("x") == true)
        {
            try
            {
                x = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
            }
            catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
            {
                validationErrors.Add($"Value {request.PathParameters["x"]} at 'x' failed to satisfy constraint: {e.Message}");
            }
        }

        var y = default(int);
        if (request.PathParameters?.ContainsKey("y") == true)
        {
            try
            {
                y = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
            }
            catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
            {
                validationErrors.Add($"Value {request.PathParameters["y"]} at 'y' failed to satisfy constraint: {e.Message}");
            }
        }

        // return 400 Bad Request if there exists a validation error
        if (validationErrors.Any())
        {
            return new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
            {
                Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"},
                    {"x-amzn-ErrorType", "ValidationException"}
                },
                StatusCode = 400
            };
        }

        var response = functions.Add(x, y, context);

        var body = response.ToString();

        return new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
        {
            Body = body,
            Headers = new Dictionary<string, string>
            {
                {"Content-Type", "application/json"}
            },
            StatusCode = 200
        };
    }
}
```

## Amazon S3 example

Lambda functions that are not using API Gateway can take advantage of Lambda Annotation's dependency injection integration and CloudFormation 
synchronization features. This example is a Lambda function that responds to S3 events and resizes images that are uploaded to S3.

The `Startup` class is used to register the services needed for the function. Two services are registered in this example. First is the
AWS SDK's S3 client. The second is the `IImageServices` to handle image manipulation. In this example the `IImageService`
is registered as a transient service so we can have a new instance created for every invocation. This is commonly needed if a 
service has state that should not be preserved per invocation.

```csharp
[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Using the AWSSDK.Extensions.NETCore.Setup package add the AWS SDK's S3 client
        services.AddAWSService<Amazon.S3.IAmazonS3>();

        // Add service for handling image manipulation. 
        // IImageServices is added as transient service so a new instance
        // is created for each Lambda invocation. This can be important if services
        // have state that should not be persisted per invocation.
        services.AddTransient<IImageServices, DefaultImageServices>();
    }
}
```

In the Lambda function the AWS SDK's S3 client is injected by the dependency injection framework via the constructor. The constructor is only ever called
once per Lambda invocation so for the `IImageServices` which was registered as transient it would not make sense to inject that service via the constructor.
Instead the `IImageServices` is injected as a method parameter using the `FromServices` attribute. That ensures each time the method is called a new instance
of `IImageServices` is created.

On the `Resize` method the `LambdaFunction` attribute sets the `MemorySize` and `Timeout` properties for the Lambda function. The source generator will sync these
values to the corresponding properties in the CloudFormation template. The `Role` property is also set but in this case the value is prefixed with a `@`.
The `@` tells the source generator to treat the value for a role as a reference to another element in the CloudFormation template. In this case the 
CloudFormation template defines an IAM role called `LambdaResizeImageRole` and the Lambda function should use that IAM role.

```csharp
public class Functions
{
    private IAmazonS3 _s3Client;

    public Functions(IAmazonS3 s3Client)
    {
        _s3Client = s3Client;
    }

    [LambdaFunction(MemorySize = 1024, Timeout = 120, Role = "@LambdaResizeImageRole")]
    public async Task Resize([FromServices] IImageServices imageServices, S3Event evnt, ILambdaContext context)
    {
        var transferUtility = new TransferUtility(this._s3Client);

        foreach(var record in evnt.Records)
        {
            var tempFile = Path.GetTempFileName();

            // Download image from S3
            await transferUtility.DownloadAsync(tempFile, record.S3.Bucket.Name, record.S3.Object.Key);

            // Resize the image
            var resizeImagePath = await imageServices.ResizeImageAsync(imagePath: tempFile, width: 50, height: 50);

            // Upload resized image to S3 with a "/thumbnails" prefix in the object key.
            await transferUtility.UploadAsync(resizeImagePath, record.S3.Bucket.Name, "/thumbnails" + record.S3.Object.Key);
        }
    }
}
```

The source generator will create the Lambda function resource in the CloudFormation template. The source generator will sync the properties that were 
defined in the `LambdaFunction` attribute. The Lambda function resources synchronized in the template can also be modified directly in the template as well. 
In this example the function is modified to define the event source in this case to S3. 

```json
    "ImageResizerFunctionFunctionsResizeGenerated": {
      "Type": "AWS::Serverless::Function",
      "Metadata": {
        "Tool": "Amazon.Lambda.Annotations"
      },
      "Properties": {
        "Runtime": "dotnet8",
        "CodeUri": ".",
        "MemorySize": 1024,
        "Timeout": 120,
        "PackageType": "Zip",
        "Handler": "ImageResizerFunction::ImageResizerFunction.Functions_Resize_Generated::Resize",
        "Role": {
          "Fn::GetAtt": [
            "LambdaResizeImageRole",
            "Arn"
          ]
        },
        "Events": {
          "S3Objects": {
            "Type": "S3",
            "Properties": {
              "Bucket": {
                "Ref": "ImageBucket"
              },
              "Filter": {
                "S3Key": {
                  "Rules": [
                    {
                      "Name": "prefix",
                      "Value": "/images"
                    }
                  ]
                }
              },
              "Events": [
                "s3:ObjectCreated:*"
              ]
            }
          }
        }
      }
    },
```

This is the code the source generator will produce for this function. The constructor is handling setting up the dependency injection. During the generated `Resize`
method a dependency injection scope is created and then the `IImageServices` is retrieved from the dependency injection and passed into the function written 
by the developer. By creating the scope in the generated `Resize` method all services registered as scoped or transient will trigger a new instance to be created
when retrieved from the dependency injection framework. This code snippet is here for 
informational purposes, as a user of the Lambda Annotations framework this code should not be needed to be seen.

```csharp
public class Functions_Resize_Generated
{
    private readonly ServiceProvider serviceProvider;

    public Functions_Resize_Generated()
    {
        var services = new ServiceCollection();

        // By default, Lambda function class is added to the service container using the singleton lifetime
        // To use a different lifetime, specify the lifetime in Startup.ConfigureServices(IServiceCollection) method.
        services.AddSingleton<Functions>();

        var startup = new ImageResizerFunction.Startup();
        startup.ConfigureServices(services);
        serviceProvider = services.BuildServiceProvider();
    }

    public async System.Threading.Tasks.Task Resize(Amazon.Lambda.S3Events.S3Event evnt, Amazon.Lambda.Core.ILambdaContext __context__)
    {
        // Create a scope for every request,
        // this allows creating scoped dependencies without creating a scope manually.
        using var scope = serviceProvider.CreateScope();
        var functions = scope.ServiceProvider.GetRequiredService<Functions>();

        var imageServices = scope.ServiceProvider.GetRequiredService<ImageResizerFunction.IImageServices>();
        await functions.Resize(imageServices, evnt, __context__);
    }
}
```

## Getting build information

The source generator integrates with MSBuild's compiler error and warning reporting when there are problems generating the boiler plate code. 

To see the code that is generated by the source generator turn the verbosity to detailed when executing a build. From the command this 
is done by using the `--verbosity` switch.
```
dotnet build --verbosity detailed
```
To change the verbosity in Visual Studio go to Tools -> Options -> Projects and Solutions and adjust the MSBuild verbosity drop down boxes.



## Lambda .NET Attributes Reference

List of .NET attributes currently supported.


* LambdaFunction
    * Placed on a method. Indicates this method should be exposed as a Lambda function.
* LambdaStartup
    * Placed on a class. Indicates this type should be used as the startup class and is used to configure the dependency injection and middleware. There can only be one class in a Lambda project with this attribute.

### Event Attributes    

Event attributes configuring the source generator for the type of event to expect and setup the event source in the CloudFormation template. If an event attribute is not set the
parameter to the `LambdaFunction` must be the event object and the event source must be configured outside of the code.

* RestApi
    * Configures the Lambda function to be called from an API Gateway REST API. The HTTP method and resource path are required to be set on the attribute.
* HttpApi
    * Configures the Lambda function to be called from an API Gateway HTTP API. The HTTP method, HTTP API payload version and resource path are required to be set on the attribute.

### Parameter Attributes

* FromHeader
    * Map method parameter to HTTP header value
* FromQuery
    * Map method parameter to query string parameter
* FromRoute
    * Map method parameter to resource path segment
* FromBody
    * Map method parameter to HTTP request body. If parameter is a complex type then request body will be assumed to be JSON and deserialized into the type.
* FromServices
    * Map method parameter to registered service in IServiceProvider

### Customizing responses for API Gateway Lambda functions

The attributes `RestApi` or `HttpApi` configure a `LambdaFunction` method to use API Gateway as the event source for the function. By default these methods return an 
HTTP status code of 200. To customize the HTTP response, including adding HTTP headers, the method signature must return an `Amazon.Lambda.Annotations.APIGateway.IHttpResult`
or `Task<Amazon.Lambda.Annotations.APIGateway.IHttpResult>`.
The `Amazon.Lambda.Annotations.APIGateway.HttpResults` class contains static methods for creating an instance of `IHttpResult` with the appropriate HTTP status code and headers.

The example below shows how to return a HTTP status code 404 with a response body and custom header.

```
[LambdaFunction(PackageType = LambdaPackageType.Image)]
[HttpApi(LambdaHttpMethod.Get, "/resource/{id}")]
public IHttpResult NotFoundResponseWithHeaderV2(int id, ILambdaContext context)
{
    return HttpResults.NotFound($"Resource with id {id} could not be found")
                        .AddHeader("Custom-Header1", "Value1");
}
```

Available static methods for creating an instance of `IHttpResult`.
* HttpResults.Accepted()
* HttpResults.BadGateway()
* HttpResults.BadRequest()
* HttpResults.Conflict()
* HttpResults.Created()
* HttpResults.Forbid()
* HttpResults.InternalServerError()
* HttpResults.NotFound()
* HttpResults.Ok()
* HttpResults.Redirect()
* HttpResults.ServiceUnavailable()
* HttpResults.Unauthorized()
* HttpResults.NewResult()

#### Content-Type
`HttpResults` will automatically assign a content-type for the response if there is a response body and content type was not specified using the `AddHeader` method.
The content type is determined using the following rules.

* Content type will be set to `text/plain` when the response body is a string.
* Content type will be set to `application/octet-stream` when the response body is a `Stream`, `byte[]` or `IList<byte>`.
* For any other response body the content type is set to `application/json`.


## Project References

If API Gateway event attributes, such as `RestAPI` or `HttpAPI`, are being used then a package reference to `Amazon.Lambda.APIGatewayEvents` must be added to the project, otherwise the project will not compile. We do not include it by default in order to keep the `Amazon.Lambda.Annotations` library lightweight. 
