using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

var GenerateBinary = (APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context) =>
{
    // Create a simple binary pattern (for example, counting bytes from 0 to 255)
    byte[] binaryData = new byte[256];
    for (int i = 0; i < 256; i++)
    {
        binaryData[i] = (byte)i;
    }

    return new APIGatewayHttpApiV2ProxyResponse
    {
        StatusCode = 200,
        Body = Convert.ToBase64String(binaryData),
        IsBase64Encoded = true,
        Headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/octet-stream" }
        }
    };
};

await LambdaBootstrapBuilder.Create(GenerateBinary, new CamelCaseLambdaJsonSerializer())
    .Build()
    .RunAsync();
