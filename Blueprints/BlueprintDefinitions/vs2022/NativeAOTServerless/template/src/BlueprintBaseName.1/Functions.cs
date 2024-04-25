using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using BlueprintBaseName._1;

[assembly:LambdaGlobalProperties(GenerateMain = true)]
[assembly:LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>))]

namespace BlueprintBaseName._1;

/// <summary>
/// This class is used to register the input event and return type for the FunctionHandler method with the System.Text.Json source generator.
/// There must be a JsonSerializable attribute for each type used as the input and return type or a runtime error will occur 
/// from the JSON serializer unable to find the serialization information for unknown types.
/// 
/// Request and response types used with Annotations library must also have a JsonSerializable for the type. The Annotations library will use the same
/// source generator serializer to use non-reflection based serialization. For example parameters with the [FromBody] or types returned using 
/// the HttpResults utility class.
/// </summary>
[JsonSerializable(typeof(APIGatewayProxyRequest))]
[JsonSerializable(typeof(APIGatewayProxyResponse))]
[JsonSerializable(typeof(NewProductDTO))]
[JsonSerializable(typeof(ProductDTO))]
public partial class LambdaFunctionJsonSerializerContext : JsonSerializerContext
{
    // By using this partial class derived from JsonSerializerContext, we can generate reflection free JSON Serializer code at compile time
    // which can deserialize our class and properties. However, we must attribute this class to tell it what types to generate serialization code for.
    // See https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-source-generation
}

public record NewProductDTO(string Name, string Description);

public record ProductDTO(string ID, string Name, string Description);

public class Functions
{
    /// <summary>
    /// A Lambda function to respond to HTTP Get methods from API Gateway.
    /// </summary>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Get, "/")]
    public IHttpResult GetFunctionHandler(ILambdaContext context)
    {
        context.Logger.LogInformation("Get Request");

        return HttpResults.Ok("Hello AWS Serverless")
                            .AddHeader("Content-Type", "text/plain");
    }

    /// <summary>
    /// A Lambda function to respond to HTTP POST methods from API Gateway
    /// </summary>
    /// <param name="product">The new product to post to the system.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Post, "/")]
    public IHttpResult PostFunctionHandler([FromBody] NewProductDTO product, ILambdaContext context)
    {
        // TODO: Add business logic
        ProductDTO savedProduct = new(Guid.NewGuid().ToString(), product.Name, product.Description);

        context.Logger.LogInformation($"Saved product {savedProduct.ID} with name {product.Name}.");

        return HttpResults.Ok(savedProduct);
    }


    /// <summary>
    /// This method demonstrates methods being exposed as Lambda functions using Amazon.Lambda.Annotations without API Gateway attributes.
    /// The event source for the Lambda function can be configured in the serverless.template. The Lambda function can be invoked manually
    /// using the AWS SDKs.
    /// </summary>
    /// <returns></returns>
    [LambdaFunction]
    public async Task<string> GetCallingIPAsync(ILambdaContext context)
    {
        context.Logger.LogInformation("Checking IP address");

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "AWS Lambda .Net Client");

        var msg = await client.GetStringAsync("http://checkip.amazonaws.com/").ConfigureAwait(continueOnCapturedContext: false);

        return msg.Replace("\n", "");
    }
}