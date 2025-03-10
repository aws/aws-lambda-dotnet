using System.Collections.Generic;
using System.Text.Json.Serialization;
using Amazon.Lambda.BedrockAgentEvents;

namespace Amazon.Lambda.AspNetCoreServer;

/// <summary>
/// Custom serializer context for the Amazon Bedrock Agent API. Used to serialize the incoming parameters from the bedrock request into the ASP.NET Core request body.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(BedrockAgentApiRequest))]
[JsonSerializable(typeof(BedrockAgentApiResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, ResponseContent>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
public partial class BedrockAgentApiSerializerContext : JsonSerializerContext
{
}
