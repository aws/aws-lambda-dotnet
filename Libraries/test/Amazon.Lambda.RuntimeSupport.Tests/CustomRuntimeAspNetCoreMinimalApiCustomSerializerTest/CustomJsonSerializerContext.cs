using System.Text.Json.Serialization;
using Amazon.Lambda.APIGatewayEvents;

namespace CustomRuntimeAspNetCoreMinimalApiCustomSerializerTest;

[JsonSerializable(typeof(WeatherForecast))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
public partial class CustomJsonSerializerContext : JsonSerializerContext
{
}