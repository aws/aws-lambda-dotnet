using System.Text.Json;

namespace Amazon.Lambda.Annotations.Testing
{
    internal class ServerlessTemplateParser
    {
        public static List<LambdaRouteConfig> GetConfigsFromTemplate(string filePath)
        {
            var content = File.ReadAllText(filePath);
            var jsonDocument = JsonDocument.Parse(content);
            return GetRouteConfigsFromFunctionsWithHttpEvents(jsonDocument);
        }

        private static List<LambdaRouteConfig> GetRouteConfigsFromFunctionsWithHttpEvents(JsonDocument jsonDocument)
        {
            var routeConfigs = new List<LambdaRouteConfig>();

            var functionElements = GetFunctionElements(jsonDocument);

            foreach (var functionElement in functionElements)
            {
                if (!functionElement.TryGetProperty("Properties", out var properties))
                    continue;

                if (!properties.TryGetProperty("Handler", out var handlerElement))
                {
                    if (!properties.TryGetProperty("ImageConfig", out var imageConfig)
                        || !imageConfig.TryGetProperty("Command", out var commandElement))
                    {
                        continue;
                    }

                    handlerElement = commandElement.EnumerateArray().First();
                }

                if (!properties.TryGetProperty("Events", out var events))
                    continue;

                foreach (var eventElement in events.EnumerateObject().Select(p => p.Value))
                {
                    if (!eventElement.TryGetProperty("Type", out var eventTypeElement))
                        continue;

                    var payloadFormat = eventTypeElement.GetString() switch
                    {
                        "Api" => PayloadFormat.RestApi,
                        "HttpApi" => PayloadFormat.HttpApiV2,
                        _ => (PayloadFormat?)null
                    };

                    if (payloadFormat is null)
                        continue;

                    if (!eventElement.TryGetProperty("Properties", out var eventProperties))
                        continue;

                    var path = eventProperties.GetProperty("Path").GetString();
                    var method = eventProperties.GetProperty("Method").GetString();

                    var handlerParts = handlerElement.GetString().Split(new[] { "::" }, StringSplitOptions.None);

                    if (handlerParts.Length != 3)
                        continue;

                    routeConfigs.Add(new LambdaRouteConfig
                    {
                        PayloadFormat = payloadFormat.Value,
                        AssemblyName = handlerParts[0],
                        TypeName = handlerParts[1],
                        MethodName = handlerParts[2],
                        HttpMethod = method,
                        PathTemplate = path
                    });
                }
            }

            return routeConfigs;
        }

        private static JsonElement[] GetFunctionElements(JsonDocument jsonDocument)
        {
            return jsonDocument.RootElement
                .GetProperty("Resources").EnumerateObject()
                .Select(p => p.Value)
                .Where(e => e.TryGetProperty("Type", out var type) && type.GetString() == "AWS::Serverless::Function")
                .ToArray();
        }
    }
}
