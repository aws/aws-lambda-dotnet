# .NET Lambda Annotations Design

The Lambda Annotation design is a new programming model for writing .NET Lambda function. At a high level the new programming model allows for idiomatic .NET coding patterns and uses [C# source generator](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) technology to bridge the gap between the Lambda programming model to the more idiomatic programming model.

The current experience for building .NET Lambda functions falls into 2 categories. One is to use the basic Lambda experience that is common for all Lambda Languages. The second approach which is applicable only for REST APIs is to use the ASP .NET Core framework. 

### Basic Lambda experience

All Lambda runtimes share a common low level programming experience. That experience is to write a function that takes in an event and Lambda context object. The developer inspects the event object and often based on the data in the event object dispatches it to certain business logic. This is a very low level experience which requires developers to write a lot of boiler plate code. Below is a simple example of an API Gateway Lambda function.


```csharp
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
```

Developers also have to keep a CloudFormation template that links to actual piece of code in sync. Every time a developer writes a new Lambda function they have to remember to update the CloudFormation template. If the user renames the method, class or namespace and they forget to make the corresponding changes to the CloudFormation template there is no error till the Lambda function is invoked in Lambda.


### ASP.NET Core Lambda Functions

The alternative for writing .NET Lambda functions for REST APIs is to use .NET's ASP.NET Core web framework. This has been a popular feature of our .NET experience since the beginning. It allows developers to write REST APIs in a well known and documented framework. The style is idiomatic .NET using .NET attributes and reflection to map HTTP request data into corresponding to method parameters. Some of the features that are most appreciated with this approach are:

* .NET Attributes for mapping route mapping and HTTP request data
* Injecting middleware into request pipelines 
    * A piece of code that runs before or after any request in the project
* Integrated dependency injection
    * A central location for registering all of the dependencies for the request in the project.
* Authentication
    * Define policies and add annotations to request paths that ensure the caller has the permissions for the policies.


Here is an example of a ASP.NET Core REST controller doing similar logic to the low level example mentions before.


```csharp
[Route("[controller]")]
public class MathController : ControllerBase
{
    [HttpGet("/plus/{x}/{y}")]
    public int Plus(int x, int y)
    {
        return x + y;
    }

    [HttpGet("/subtract/{x}/{y}")]
    public int Subtract(int x, int y)
    {
        return x - y;
    }
}
```


There are a few significant negative side effect for writing REST APIs for Lambda using ASP.NET Core. 

* Significant impact to cold start due to size of the framework and reliance on reflection.
* The common features of ASP.NET Core for REST APIs are supported in Lambda but a significant number of the features are not and there is no clear indication what is and isn't supported. For example SignalR, Blazor Serverside and response eventing.
* The usage of API Gateway is dumbed down because the API Gateway REST API is defined as a single wild card resource path letting ASP.NET Core handling the routing. This takes away a lot of the features of API Gateway like per resource path IAM permissions and memory sizing. 
* ASP.NET Core can only be used for defining REST APIs. It can not be used for other types of Lambda functions like S3 events.

## Goals for a better experience

To improve the experience of writing Lambda functions we need an experience that is similar to what developers are used to with ASP.NET Core without the negative side effects. The major goals are:

* An experience similar to what .NET developers are used in frameworks like ASP.NET Core
    * Although it will follow similar patterns we will not reuse the same classes as ASP.NET Core to avoid an implicit contract agreement between the developer and the framework.
* No significant impact to cold start
    * Measuring with a prototype the plus math operation cold start was in the **~320ms** the same as the basic Lambda programming model. Using ASP.NET Core the cold start was **~1,200ms**.
* Can be used for more scenarios then just REST APIs
* Underlying power of AWS services is still intuitively available
* Keeping code and CloudFormation template in sync



## The new experience for writing .NET Lambda functions

To create a new experience for developing .NET Lambda functions we will use a combination of .NET attributes developers apply to their code and C# source generator to generate code and CloudFormation snippets at compile time. This will remove the need for reflection at runtime and the only runtime overhead component will be the inclusion of the new .NET attributes and interfaces. The .NET attributes will have negligible effect to cold start.

### What are C# source generators?

C# source generators were added to the C# compiler as part of .NET 5. Although it is possible to use source generators with the 3.1 if the .NET 5 SDK is installed. This library will target .NET 6 the next LTS version of .NET. Supporting 3.1 will cause support pain given that source generators would silently not run if users don't have .NET 5+ installed. Also users of this library are most likely starting with new Lambda functions.

A C# source generator works similar to a Roslyn analyzer which inspect the code at compile time and in the analyzer case reports back custom errors it finds. With a source generator new code can be generated into the .NET assembly at compile time. In our use case we can inspect the code at compile time looking for our Lambda attributes and generate .NET code at compile to bridge the Lambda basic programming model to our higher level programming model.

For more information about source generator implementation see appendix A.

### User experience example

A developer gets started by including the **Amazon.Lambda.Annotations** NuGet package. No other tooling is required. This package contains 2 .NET assemblies. One assembly is used at runtime and contains the new Lambda attributes. The second assembly is the source generator which is used at compile time. 

Since this tooling is pushed into the .NET compiler it will work on all of AWS's tooling for deploying .NET Lambda functions. Whether that is our Visual Studio or command line tooling, using SAM with Rider and Visual Studio Code, or developer's own custom process as long at it is CloudFormation based. We could potentially make it extensible to support other tooling that third parties could use to integrate with Terraform, Pulumi or [LambdaSharp](https://lambdasharp.net/) from one of our community heroes.

Earlier we talked about the basic Lambda programming model and we used an example of a Lambda function that was doing a simple plus math operation. That Lambda function took about 20 lines code which was full of undesirable boiler plate code. Using Amazon.Lambda.Annotations that example becomes this:

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

There is no boiler plate code in this snippet. The **LambdaFunction** attribute signifies this is a piece of code that should be exposed as a Lambda function. The **RestApi** sets up the event source for the Lambda function. This similar pattern can be used for other event sources like S3 events.

### What happens when you compile?

The basic Lambda programming model hasn't changed and a Lambda function is required to take one argument only which is the Lambda event object. When the code above compiles the source generator is invoked by the .NET compiler. The source generator will generate the Lambda basic function taking in the single event. The generated code will be responsible for:

* If enabled, during initial invocation run provided method for configuring dependency injection.
* If any middleware is registered execute it before or after each request
* Translate the data from the single event to the provided method signature
* Execute provided Lambda function method
* If appropriate translate response to expect service response.
    * Response objects for API Gateway is most common use case.

For the simple plus math operation example the generated code would look something like this:

```csharp
using System;
using System.Collections.Generic;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace MathLambdaFunctions
{
    public class Functions_Plus_Generated
    {
        IServiceProvider _provider
    
        public Functions_Plus_Generated()
        {
            IServiceCollection collection = ...
            var = startup = new Startup();
            startup.ConfigureServics(collection);
            
            _provider = collection.Build();
            
            _wrapped = Activator.CreateInstance(typeof(Function), _provider);
        }
    
        public Functions _wrapped;

        public APIGatewayProxyResponse Plus(APIGatewayProxyRequest request, ILambdaContext context)
        {
            int p0 = default(int);
            if(request.PathParameters.ContainsKey("x")) {
                p0 = (int)Convert.ChangeType(request.PathParameters["x"], typeof(int));
            }
            int p1 = default(int);
            if(request.PathParameters.ContainsKey("y")) {
                p1 = (int)Convert.ChangeType(request.PathParameters["y"], typeof(int));
            }

            var response =  _wrapped.Plus(p0, p1);

            return new APIGatewayProxyResponse
                        {
                            StatusCode = 200,
                            Body = response.ToString(),
                            Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
                        };


        }
    }
}
```

*(Note: this is an example of what the generated code could look like. The actual code would need to take into account registered middleware and services registered into the dependency injection when constructing the developers type.)*

The developer will never have to see this code as this whole experience will be taken care for the user at compile time. To find the request parameters no reflection was used because the source generator was able to understand at compile time where all of the data was to come from.

### CloudFormation

The source generator will have 2 responsibilities. The first is the code generation as discussed earlier. The second is maintaining the Lambda resource defined in the CloudFormation template or CDK project. Because the actual .NET function that Lambda will call is generated at runtime this is critical to make sure the function handler string is set correctly. For the Lambda function above the source generator will add to the CloudFormation template the following snippet.

```json
    "MathLambdaFunctionsFunctionsPlus": {
      "Type": "AWS::Serverless::Function",
      "Metadata": {
        "Tool": "Amazon.Lambda.Annotations"
      },
      "Properties": {
        "Runtime": "dotnetcore3.1",
        "CodeUri": "",
        "MemorySize": 256,
        "Timeout": 30,
        "Policies": [
          "AWSLambdaBasicExecutionRole"
        ],
        "Handler": "MathLambdaFunctions::MathLambdaFunctions.Functions_Plus_Generated::Plus",
        "Events": {
          "RestRoute1": {
            "Type": "Api",
            "Properties": {
              "Path": "/plus/{x}/{y}",
              "Method": "GET"
            }
          }
        }
      }
    } 
```

The Handler field is set to the generated method from the source generator. Runtime is specified based on the target framework. The Events section contains the route that was defined by the **RestApi** attribute. The other fields are set to the default values. Developers can choose to either modify the values in the template or set the values as parameters on the **LambdaFunction** attribute.

The source generator will need to be configurable where it writes to the CloudFormation template. By default the source generator will look for write to the `serverless.template` in the project directory. This can be overriden by either setting the `template` property in the `aws-lambda-tools-defaults.json` file or by setting the `LambdaCloudFormationTemplate` property in the `csproj` file.

## Full example

Using the simple calculator example lets expand it to show how a consumer of the new library can better organize their project using common patterns like dependency injection and middleware for all of their Lambda functions. The calculator problem space is very simplistic but we will for demonstrations purposes use it in over engineered fashion to show how the library could be used.

### Startup

To keep the logic of our Lambda functions simple we will abstract the actual math operations into its own `ICalculatorService` type. The implementation will be registered into the dependency injection.

Like ASP .NET Core we will register a `startup` class. This is a class that has methods for configuring the services and the request pipeline. To keep the experience similar the user can write a `startup` class and indicated it is the startup class by adding the **LambdaStartup** attribute. Here is an example of a `startup` class for our calculator project.


```csharp
[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var configBuilder = new ConfigurationBuilder()
            .AddSystemsManager("/calculator-settings/");

        services.AddSingleton<IConfiguration>(configBuilder.Build());
        services.AddSingleton<ICalculatorService>(new DefaultCalculatorService());

        // Could also add AWS service clients
        services.AddAWSService<Amazon.S3.IAmazonS3>();
    }

    public void Configure(ILambdaInvokeBuilder builder)
    {
        builder.Use(async (evnt, context, next) =>
        {
            context.Logger.LogLine("Processing event type: " + evnt.GetType());
            try
            {
                await next();
            }
            catch(ExternalSystemUnreachableException)
            {
                // Ugh oh something is wrong with external system. TODO send a special alert or implement a retry logic.
                throw;
            }
            catch(Exception e)
            {
                context.Logger.LogLine($"Got an exception of type {e.GetType()}");
                throw;
            }
            finally
            {
                // Global cleanup after each Lambda invocation for all Lambda functions defined in project.
            }
        });
    }
}
```

The `ConfigureServices` maps directly to ASP.NET Core and it is where users register the services for their application. In our example we registered the implementation of `ICalculatorService`. We could also register the .NET configuration system and even load configurations for system manager using our Amazon.Extensions.Configuration.SystemsManager library.

The `Configure` is similar to ASP.NET Core Configure method. It is used to register middleware. Middleware is code that will run before and after each Lambda invocation. In this example we just added extra logging and potentially retry logic. These are common asks we get from Lambda customers looking for a global way to make sure events and logging are flushed before invocation is done.

### Lambda Functions

In this case the Lambda functions are defined in a class called LambdaFunctions. The individual functions could also be defined in separate classes throughout the project.

The source generator will generate code that wraps each of the Lambda functions. It will have executed the `startup` class and the constructor of LambdaFunctions during the Lambda functions initialization stage. The constructors parameters for LambdaFunctions will be supplied by services registered by the dependency injection framework. This is very similar to how API controllers are created in an ASP.NET Core project.


```csharp
public class LambdaFunctions
{
    ICalculatorService _calculator;
    public LambdaFunctions(ICalculatorService calculator)
    {
        _calculator = calculator;
    }

    [LambdaFunction]
    [ApiRouteAttribute("/plus/{x}/{y}")]
    public int Plus(int x, int y)
    {
        return _calculator.Plus(x, y);
    }

    [LambdaFunction]
    [ApiRouteAttribute("/subtract/{x}/{y}")]
    public int Subtract(int x, int y)
    {
        return _calculator.Subtract(x, y);
    }

...

}
```



## Lambda .NET Attributes

Here is a preliminary list of .NET attributes that will tell the source generator what to generate. The event attributes will map to the SAM event types. Not all of them are listed but we should be able to implement most if not all of them. https://github.com/aws/serverless-application-model/blob/develop/versions/2016-10-31.md#event-source-types


* LambdaFunction
    * Placed on a method. Indicates this method should be exposed as a Lambda function.
* LambdaStartup
    * Placed on a class. Indicates this type should be used as the startup class and is used to configure the dependency injection and middleware. There can only be one class in a Lambda project with this attribute.

### Event Attributes    
* RestApi
    * Configures the Lambda function to be called from an API Gateway REST API. The HTTP method and resource path are required to be set on the attribute.
* HttpApi
    * Configures the Lambda function to be called from an API Gateway HTTP API. The HTTP method, HTTP API payload version and resource path are required to be set on the attribute.
* S3Event
    * Configures S3 as the event source.
* SQSEvent
    * Configures SQS as the event source.
* DynamoDbEvent
    * Configures DynamoDB as the event source.
* ScheduleEvent
    * Configures the Lambda function to be called on a schedule.

### Parameter Attributes

* FromHeader
    * Map method parameter to HTTP header value
* FromQuery
    * Map method parameter to query string pareamter
* FromRoute
    * Map method parameter to resource path segement
* FromBody
    * Map method parameter to HTTP request body. If parameter is a complex type then request body will be assumed to be JSON and deserialized into the type.
* FromServices
    * Map method parameter to registered service in IServiceProvider
