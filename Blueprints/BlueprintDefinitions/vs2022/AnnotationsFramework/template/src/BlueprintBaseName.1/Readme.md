# .NET Lambda Annotations Framework (Preview)

Lambda Annotations is a programming model for writing .NET Lambda functions. At a high level the programming model allows
idiomatic .NET coding patterns. [C# Source Generators](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) are used to bridge the 
gap between the Lambda programming model to the Lambda Annotations programming model without adding any performance penalty.

The documentation for .NET Lambda Annotation framework can be found [here](https://github.com/aws/aws-lambda-dotnet/blob/master/Libraries/src/Amazon.Lambda.Annotations/README.md)

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

## Deployment

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
    "Runtime": "dotnet6",
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
        "Runtime": "dotnet6",

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

Event attributes configuring the source generator for the type of event to expect and setup the event source in the CloudFormation temlate. If an event attribute is not set the
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


## Project References

If API Gateway event attributes, such as `RestAPI` or `HttpAPI`, are being used then a package reference to `Amazon.Lambda.APIGatewayEvents` must be added to the project, otherwise the project will not compile. We do not include it by default in order to keep the `Amazon.Lambda.Annotations` library lightweight. 