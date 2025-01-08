// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace Amazon.Lambda.TestTool.Utilities;

/// <summary>
/// Utility class for handling HTTP requests in the context of API Gateway emulation.
/// </summary>
public static class HttpRequestUtility
{
    /// <summary>
    /// Determines whether the specified content type represents binary content.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns>True if the content type represents binary content; otherwise, false.</returns>
    public static bool IsBinaryContent(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;

        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/zip", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/wasm", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/x-protobuf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the body of the HTTP request as a string. Returns null if the request body is empty.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The body of the request as a string, or null if the body is empty.</returns>
    public static async Task<string?> ReadRequestBody(HttpRequest request)
    {
        if (request.ContentLength == 0 || request.Body == null || !request.Body.CanRead)
        {
            return null;
        }

        // Check if the content is binary
        bool isBinary = HttpRequestUtility.IsBinaryContent(request.ContentType);

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        using (var memoryStream = new MemoryStream())
        {
            await request.Body.CopyToAsync(memoryStream);

            // If the stream is empty, return null
            if (memoryStream.Length == 0)
            {
                return null;
            }

            memoryStream.Position = 0;

            if (isBinary)
            {
                // For binary data, convert to Base64 string
                byte[] bytes = memoryStream.ToArray();
                return Convert.ToBase64String(bytes);
            }
            else
            {
                // For text data, read as string
                using (var reader = new StreamReader(memoryStream))
                {
                    string content = await reader.ReadToEndAsync();
                    return string.IsNullOrWhiteSpace(content) ? null : content;
                }
            }
        }
    }



    /// <summary>
    /// Extracts headers from the request, separating them into single-value and multi-value dictionaries.
    /// </summary>
    /// <param name="headers">The request headers.</param>
    /// <param name="lowerCaseKeyName">Whether to lowercase the key name or not.</param>
    /// <returns>A tuple containing single-value and multi-value header dictionaries.</returns>
    /// <example>
    /// For headers:
    /// Accept: text/html
    /// Accept: application/xhtml+xml
    /// X-Custom-Header: value1
    /// 
    /// The method will return:
    /// singleValueHeaders: { "Accept": "application/xhtml+xml", "X-Custom-Header": "value1" }
    /// multiValueHeaders: { "Accept": ["text/html", "application/xhtml+xml"], "X-Custom-Header": ["value1"] }
    /// </example>
    public static (IDictionary<string, string>, IDictionary<string, IList<string>>) ExtractHeaders(IHeaderDictionary headers, bool lowerCaseKeyName = false)
    {
        var singleValueHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var multiValueHeaders = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            var key = lowerCaseKeyName ? header.Key.ToLower() : header.Key;
            singleValueHeaders[key] = header.Value.Last() ?? "";
            multiValueHeaders[key] = [.. header.Value];
        }

        return (singleValueHeaders, multiValueHeaders);
    }

    /// <summary>
    /// Extracts query string parameters from the request, separating them into single-value and multi-value dictionaries.
    /// </summary>
    /// <param name="query">The query string collection.</param>
    /// <returns>A tuple containing single-value and multi-value query parameter dictionaries.</returns>
    /// <example>
    /// For query string: ?param1=value1&amp;param2=value2&amp;param2=value3
    /// 
    /// The method will return:
    /// singleValueParams: { "param1": "value1", "param2": "value3" }
    /// multiValueParams: { "param1": ["value1"], "param2": ["value2", "value3"] }
    /// </example>
    public static (IDictionary<string, string>, IDictionary<string, IList<string>>) ExtractQueryStringParameters(IQueryCollection query)
    {
        var singleValueParams = new Dictionary<string, string>();
        var multiValueParams = new Dictionary<string, IList<string>>();

        foreach (var param in query)
        {
            singleValueParams[param.Key] = param.Value.Last() ?? "";
            multiValueParams[param.Key] = [.. param.Value];
        }

        return (singleValueParams, multiValueParams);
    }

    /// <summary>
    /// Generates a random API Gateway request ID for HTTP API v1 and v2.
    /// </summary>
    /// <returns>A string representing a random request ID in the format used by API Gateway for HTTP APIs.</returns>
    /// <remarks>
    /// The generated ID is a 145character string consisting of lowercase letters and numbers, followed by an equals sign.
    public static string GenerateRequestId()
    {
        return $"{Guid.NewGuid().ToString("N").Substring(0, 8)}{Guid.NewGuid().ToString("N").Substring(0, 7)}=";
    }

    /// <summary>
    /// Generates a random X-Amzn-Trace-Id for REST API mode.
    /// </summary>
    /// <returns>A string representing a random X-Amzn-Trace-Id in the format used by API Gateway for REST APIs.</returns>
    /// <remarks>
    /// The generated trace ID includes:
    /// - A root ID with a timestamp and two partial GUIDs
    /// - A parent ID
    /// - A sampling decision (always set to 0 in this implementation)
    /// - A lineage identifier
    /// </remarks>
    public static string GenerateTraceId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString("x");
        var guid1 = Guid.NewGuid().ToString("N");
        var guid2 = Guid.NewGuid().ToString("N");
        return $"Root=1-{timestamp}-{guid1.Substring(0, 12)}{guid2.Substring(0, 12)};Parent={Guid.NewGuid().ToString("N").Substring(0, 16)};Sampled=0;Lineage=1:{Guid.NewGuid().ToString("N").Substring(0, 8)}:0";
    }

    public static long CalculateContentLength(HttpRequest request, string? body)
    {
        if (!string.IsNullOrEmpty(body))
        {
            return Encoding.UTF8.GetByteCount(body);
        }
        else if (request.ContentLength.HasValue)
        {
            return request.ContentLength.Value;
        }
        return 0;
    }


}
