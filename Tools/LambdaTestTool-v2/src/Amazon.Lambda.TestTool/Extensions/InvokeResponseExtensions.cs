// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Models;

/// <summary>
/// Provides extension methods for converting Lambda InvokeResponse to API Gateway response types.
/// </summary>
public static class InvokeResponseExtensions
{
    /// <summary>
    /// Converts an Amazon Lambda InvokeResponse to an APIGatewayProxyResponse.
    /// </summary>
    /// <param name="invokeResponse">The InvokeResponse from a Lambda function invocation.</param>
    /// <param name="emulatorMode">The API Gateway emulator mode (Rest or Http).</param>
    /// <returns>An APIGatewayProxyResponse object.</returns>
    /// <remarks>
    /// If the response cannot be deserialized as an APIGatewayProxyResponse, it returns an error response.
    /// The error response differs based on the emulator mode:
    /// - For Rest mode: StatusCode 502 with a generic error message.
    /// - For Http mode: StatusCode 500 with a generic error message.
    /// </remarks>
    public static APIGatewayProxyResponse ToApiGatewayProxyResponse(this InvokeResponse invokeResponse, ApiGatewayEmulatorMode emulatorMode)
    {
        if (emulatorMode == ApiGatewayEmulatorMode.HttpV2)
        {
            throw new NotSupportedException("This function should only be used with Rest and Httpv1 emulator modes");
        }

        using var reader = new StreamReader(invokeResponse.Payload);
        string responseJson = reader.ReadToEnd();
        try
        {
            return JsonSerializer.Deserialize<APIGatewayProxyResponse>(responseJson)!;
        }
        catch
        {
            return ToApiGatewayErrorResponse(emulatorMode);
        }
    }

    /// <summary>
    /// Creates an API Gateway error response based on the emulator mode.
    /// </summary>
    /// <param name="emulatorMode">The API Gateway emulator mode (Rest or Http).</param>
    /// <returns>An APIGatewayProxyResponse object representing the error response.</returns>
    /// <remarks>
    /// This method generates different error responses based on the API Gateway emulator mode:
    /// - For Rest mode: Returns a response with StatusCode 502 and a generic error message.
    /// - For Http mode: Returns a response with StatusCode 500 and a generic error message.
    /// Both responses include a Content-Type header set to application/json.
    /// </remarks>
    public static APIGatewayProxyResponse ToApiGatewayErrorResponse(ApiGatewayEmulatorMode emulatorMode)
    {
        if (emulatorMode == ApiGatewayEmulatorMode.Rest)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 502,
                Body = "{\"message\":\"Internal server error\"}",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                IsBase64Encoded = false
            };
        }
        else
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = "{\"message\":\"Internal Server Error\"}",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                },
                IsBase64Encoded = false
            };
        }
    }

    /// <summary>
    /// Converts an Amazon Lambda InvokeResponse to an APIGatewayHttpApiV2ProxyResponse.
    /// </summary>
    /// <param name="invokeResponse">The InvokeResponse from a Lambda function invocation.</param>
    /// <returns>An APIGatewayHttpApiV2ProxyResponse object.</returns>
    /// <remarks>
    /// This method reads the payload from the InvokeResponse and passes it to ToHttpApiV2Response
    /// for further processing and conversion.
    /// </remarks>
    public static APIGatewayHttpApiV2ProxyResponse ToApiGatewayHttpApiV2ProxyResponse(this InvokeResponse invokeResponse)
    {
        using var reader = new StreamReader(invokeResponse.Payload);
        string responseJson = reader.ReadToEnd();
        return ToHttpApiV2Response(responseJson);
    }

    /// <summary>
    /// Converts a response string to an APIGatewayHttpApiV2ProxyResponse.
    /// </summary>
    /// <param name="response">The response string to convert.</param>
    /// <returns>An APIGatewayHttpApiV2ProxyResponse object.</returns>
    /// <remarks>
    /// This method replicates the observed behavior of API Gateway's HTTP API
    /// with Lambda integrations using payload format version 2.0, which differs
    /// from the official documentation.
    ///
    /// Observed behavior:
    /// 1. If the response is a JSON object with a 'statusCode' property:
    ///    - It attempts to deserialize it as a full APIGatewayHttpApiV2ProxyResponse.
    ///    - If deserialization fails, it returns a 500 Internal Server Error.
    /// 2. For any other response (including non-JSON strings, invalid JSON, or partial JSON):
    ///    - Sets statusCode to 200
    ///    - Uses the response as-is for the body
    ///    - Sets Content-Type to application/json
    ///    - Sets isBase64Encoded to false
    ///
    /// This behavior contradicts the official documentation, which states:
    /// "If your Lambda function returns valid JSON and doesn't return a statusCode,
    /// API Gateway assumes a 200 status code and treats the entire response as the body."
    ///
    /// In practice, API Gateway does not validate the JSON. It treats any response
    /// without a 'statusCode' property as a raw body, regardless of whether it's
    /// valid JSON or not.
    ///
    /// For example, if a Lambda function returns:
    ///     '{"name": "John Doe", "age":'
    /// API Gateway will treat this as a raw string body in a 200 OK response, not attempting
    /// to parse or validate the JSON structure.
    ///
    /// This method replicates this observed behavior rather than the documented behavior.
    /// </remarks>
    private static APIGatewayHttpApiV2ProxyResponse ToHttpApiV2Response(string response)
    {
        try
        {
            // Try to deserialize as JsonElement first to inspect the structure
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(response);

            // Check if it's an object that might represent a full response
            if (jsonElement.ValueKind == JsonValueKind.Object &&
                jsonElement.TryGetProperty("statusCode", out _))
            {
                // It has a statusCode property, so try to deserialize as full response
                try
                {
                    return JsonSerializer.Deserialize<APIGatewayHttpApiV2ProxyResponse>(response)!;
                }
                catch
                {
                    // If deserialization fails, return Internal Server Error
                    return ToHttpApiV2ErrorResponse();
                }
            }

            // If it's a JSON string value, extract the actual string value.
            // The reason this is needed is because when the lambda function returns a string by doing something like
            // return "test", it actually comes as "\"test\"" to response. So we need to get the raw string which is what api gateway does.
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                response = jsonElement.GetString()!;
            }

        }
        catch
        {
            // If JSON parsing fails, fall through to default behavior
        }

        // Default behavior: return the response as-is
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 200,
            Body = response,
            Headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" }
        },
            IsBase64Encoded = false
        };
    }

    /// <summary>
    /// Creates a standard HTTP API v2 error response.
    /// </summary>
    /// <returns>An APIGatewayHttpApiV2ProxyResponse object representing the error response.</returns>
    /// <remarks>
    /// This method generates a standard error response for HTTP API v2:
    /// - StatusCode is set to 500 (Internal Server Error).
    /// - Body contains a JSON string with a generic error message.
    /// - Headers include a Content-Type set to application/json.
    /// - IsBase64Encoded is set to false.
    /// </remarks>
    public static APIGatewayHttpApiV2ProxyResponse ToHttpApiV2ErrorResponse()
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = 500,
            Body = "{\"message\":\"Internal Server Error\"}",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            },
            IsBase64Encoded = false
        };
    }

}
