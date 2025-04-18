using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.BedrockAgentEvents;
using Amazon.Lambda.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// This class extends from AbstractAspNetCoreFunction which contains the core functionality for converting
    /// incoming Amazon Bedrock Agent API events into ASP.NET Core request and then convert the response
    /// back to a format that Bedrock Agent API expects.
    /// </summary>
    public abstract class BedrockAgentApiFunction : AbstractAspNetCoreFunction<BedrockAgentApiRequest, BedrockAgentApiResponse>
    {
        /// <summary>
        /// The serializer context for the Bedrock Agent API. Used to serialize the incoming parameters from the bedrock request into the ASP.NET Core request body.
        /// Required for AOT compliation.
        /// </summary>
        private readonly JsonSerializerContext _serializerContext;
        private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// The constructor for the BedrockAgentApiFunction.
        /// </summary>
        protected BedrockAgentApiFunction() : base()
        {
            _serializerContext = new BedrockAgentApiSerializerContext(_jsonSerializerOptions);
        }

        /// <summary>
        /// The constructor for the BedrockAgentApiFunction that takes in the IServiceProvider for the ASP.NET Core application.
        /// </summary>
        /// <param name="services">The IServiceProvider for the ASP.NET Core application.</param>
        protected BedrockAgentApiFunction(IServiceProvider services) : base(services)
        {
            _serializerContext = new BedrockAgentApiSerializerContext(_jsonSerializerOptions);
        }

        /// <summary>
        /// Constructor that allows configuring when the ASP.NET Core framework will be initialized
        /// </summary>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected BedrockAgentApiFunction(StartupMode startupMode) : base(startupMode)
        {
            _serializerContext = new BedrockAgentApiSerializerContext(_jsonSerializerOptions);
        }

        /// <summary>
        /// Convert the incoming Bedrock Agent API request to ASP.NET Core request features.
        /// </summary>
        /// <param name="features">The ASP.NET Core request features.</param>
        /// <param name="bedrockAgentApiRequest">The Bedrock Agent API request.</param>
        /// <param name="lambdaContext">The Lambda context.</param>
        protected override void MarshallRequest(InvokeFeatures features, BedrockAgentApiRequest bedrockAgentApiRequest, ILambdaContext lambdaContext)
        {
            var path = bedrockAgentApiRequest.ApiPath ?? "/";
            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            var requestFeatures = (IHttpRequestFeature)features;
            requestFeatures.Path = Utilities.DecodeResourcePath(path);
            requestFeatures.PathBase = string.Empty;
            requestFeatures.QueryString = string.Empty;
            requestFeatures.Method = bedrockAgentApiRequest.HttpMethod ?? "GET";
            requestFeatures.Scheme = "https";
            requestFeatures.Headers = new HeaderDictionary
            {
                ["X-Bedrock-Agent-Id"] = bedrockAgentApiRequest.Agent?.Id ?? "",
                ["X-Bedrock-Agent-Name"] = bedrockAgentApiRequest.Agent?.Name ?? "",
                ["X-Bedrock-Agent-Alias"] = bedrockAgentApiRequest.Agent?.Alias ?? "",
                ["X-Bedrock-Agent-Version"] = bedrockAgentApiRequest.Agent?.Version ?? "",
                ["X-Bedrock-Session-Id"] = bedrockAgentApiRequest.SessionId ?? "",
                ["X-Bedrock-Action-Group"] = bedrockAgentApiRequest.ActionGroup ?? ""
            };

            if (bedrockAgentApiRequest.Parameters != null && bedrockAgentApiRequest.Parameters.Count > 0)
            {
                var pathParams = Utilities.ExtractPathParams(path);
                foreach (var param in bedrockAgentApiRequest.Parameters)
                {
                    if (pathParams.Contains(param.Name))
                    {
                        requestFeatures.Path = requestFeatures.Path.Replace($"{{{param.Name}}}", param.Value);
                    }
                }

                var queryParams = new List<string>();
                foreach (var param in bedrockAgentApiRequest.Parameters)
                {
                    if (pathParams.Contains(param.Name))
                    {
                        continue;
                    }
                    queryParams.Add($"{Uri.EscapeDataString(param.Name)}={Uri.EscapeDataString(param.Value)}");
                }
                requestFeatures.QueryString = "?" + string.Join("&", queryParams);
            }

            long contentLength = 0;
            // Only one content type is supported
            if (bedrockAgentApiRequest.RequestBody?.Content != null && bedrockAgentApiRequest.RequestBody.Content.Count > 0)
            {
                // Prioritize application/json content type if exists
                var content = bedrockAgentApiRequest.RequestBody.Content.ContainsKey("application/json")
                    ? bedrockAgentApiRequest.RequestBody.Content.First(x => x.Key == "application/json")
                    : bedrockAgentApiRequest.RequestBody.Content.First();
                var properties = new Dictionary<string, string>();

                if (content.Value.Properties != null)
                {
                    foreach (var prop in content.Value.Properties)
                    {
                        properties[prop.Name] = prop.Value;
                    }
                }

                requestFeatures.Headers["Content-Type"] = content.Key;
                var jsonTypeInfo = _serializerContext.GetTypeInfo(typeof(Dictionary<string, string>)) as JsonTypeInfo<Dictionary<string, string>>;
                var body = JsonSerializer.Serialize(properties, jsonTypeInfo);

                var stream = Utilities.ConvertLambdaRequestBodyToAspNetCoreBody(body, false);

                requestFeatures.Body = stream;
                contentLength = body.Length;
            }
            else
            {
                requestFeatures.Body = new MemoryStream();
            }

            requestFeatures.Headers["Content-Length"] = contentLength.ToString();

            // Call consumers customize method in case they want to change how API Gateway's request
            // was marshalled into ASP.NET Core request.
            PostMarshallRequestFeature(requestFeatures, bedrockAgentApiRequest, lambdaContext);
        }

        /// <summary>
        /// Convert the ASP.NET Core response to a Bedrock Agent API response.
        /// </summary>
        /// <param name="responseFeatures">The ASP.NET Core response features.</param>
        /// <param name="lambdaContext">The Lambda context.</param>
        /// <param name="statusCodeIfNotSet">The status code to use if not set in the response.</param>
        /// <returns>The Bedrock Agent API response.</returns>
        protected override BedrockAgentApiResponse MarshallResponse(IHttpResponseFeature responseFeatures, ILambdaContext lambdaContext, int statusCodeIfNotSet = 200)
        {
            var itemsFeature = responseFeatures as IItemsFeature;
            var request = itemsFeature?.Items[LAMBDA_REQUEST_OBJECT] as BedrockAgentApiRequest;

            if (request == null)
            {
                throw new InvalidOperationException("The request object was not found in the response features.");
            }

            var bodyFeature = responseFeatures as IHttpResponseBodyFeature;
            string responseBody = string.Empty;
            if (bodyFeature?.Stream != null)
            {
                responseBody = Encoding.UTF8.GetString(((MemoryStream)bodyFeature.Stream).ToArray());
            }

            // Default content type is application/json, unless otherwise specified.
            var contentType = "application/json";
            if (responseFeatures.Headers != null &&
                responseFeatures.Headers.ContainsKey("Content-Type") &&
                !responseFeatures.Headers.ContentType.ToString().Contains("application/json"))
            {
                contentType = responseFeatures.Headers.ContentType;
            }

            var response = new BedrockAgentApiResponse
            {
                MessageVersion = "1.0",
                Response = new Response
                {
                    ActionGroup = request.ActionGroup,
                    ApiPath = request.ApiPath,
                    HttpMethod = request.HttpMethod,
                    HttpStatusCode = responseFeatures.StatusCode != 0 ? responseFeatures.StatusCode : statusCodeIfNotSet,
                    ResponseBody = new Dictionary<string, ResponseContent>
                    {
                        [contentType] = new ResponseContent
                        {
                            Body = responseBody
                        }
                    }
                },
                SessionAttributes = request.SessionAttributes ?? [],
                PromptSessionAttributes = request.PromptSessionAttributes ?? []
            };

            PostMarshallResponseFeature(responseFeatures, response, lambdaContext);

            return response;
        }
    }
}
