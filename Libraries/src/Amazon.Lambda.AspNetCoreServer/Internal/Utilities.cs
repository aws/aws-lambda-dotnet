using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// 
    /// </summary>
    public static class Utilities
    {
        internal static Stream ConvertLambdaRequestBodyToAspNetCoreBody(string body, bool isBase64Encoded)
        {
            Byte[] binaryBody;
            if (isBase64Encoded)
            {
                binaryBody = Convert.FromBase64String(body);
            }
            else
            {
                binaryBody = UTF8Encoding.UTF8.GetBytes(body);
            }

            return new MemoryStream(binaryBody);
        }

        internal static (string body, bool isBase64Encoded) ConvertAspNetCoreBodyToLambdaBody(Stream aspNetCoreBody, ResponseContentEncoding rcEncoding)
        {

            // Do we encode the response content in Base64 or treat it as UTF-8
            if (rcEncoding == ResponseContentEncoding.Base64)
            {
                // We want to read the response content "raw" and then Base64 encode it
                byte[] bodyBytes;
                if (aspNetCoreBody is MemoryStream)
                {
                    bodyBytes = ((MemoryStream)aspNetCoreBody).ToArray();
                }
                else
                {
                    using (var ms = new MemoryStream())
                    {
                        aspNetCoreBody.CopyTo(ms);
                        bodyBytes = ms.ToArray();
                    }
                }
                return (body: Convert.ToBase64String(bodyBytes), isBase64Encoded: true);
            }
            else if (aspNetCoreBody is MemoryStream)
            {
                return (body: UTF8Encoding.UTF8.GetString(((MemoryStream)aspNetCoreBody).ToArray()), isBase64Encoded: false);
            }
            else
            {
                aspNetCoreBody.Position = 0;
                using (StreamReader reader = new StreamReader(aspNetCoreBody, Encoding.UTF8))
                {
                    return (body: reader.ReadToEnd(), isBase64Encoded: false);
                }
            }
        }

        internal static string CreateQueryStringParameters(IDictionary<string, string> singleValues, IDictionary<string, IList<string>> multiValues, bool urlEncodeValue)
        {
            if (multiValues?.Count > 0)
            {
                StringBuilder sb = new StringBuilder("?");
                foreach (var kvp in multiValues)
                {
                    foreach (var value in kvp.Value)
                    {
                        if (sb.Length > 1)
                        {
                            sb.Append("&");
                        }
                        sb.Append($"{kvp.Key}={(urlEncodeValue ? WebUtility.UrlEncode(value) : value)}");
                    }
                }
                return sb.ToString();
            }
            else if (singleValues?.Count > 0)
            {
                var queryStringParameters = singleValues;
                if (queryStringParameters != null && queryStringParameters.Count > 0)
                {
                    StringBuilder sb = new StringBuilder("?");
                    foreach (var kvp in singleValues)
                    {
                        if (sb.Length > 1)
                        {
                            sb.Append("&");
                        }
                        sb.Append($"{kvp.Key}={(urlEncodeValue ? WebUtility.UrlEncode(kvp.Value) : kvp.Value)}");
                    }
                    return sb.ToString();
                }
            }

            return string.Empty;
        }

        internal static void SetHeadersCollection(IHeaderDictionary headers, IDictionary<string, string> singleValues, IDictionary<string, IList<string>> multiValues)
        {
            if (multiValues?.Count > 0)
            {
                foreach (var kvp in multiValues)
                {
                    headers[kvp.Key] = new StringValues(kvp.Value.ToArray());
                }
            }
            else if (singleValues?.Count > 0)
            {
                foreach (var kvp in singleValues)
                {
                    headers[kvp.Key] = new StringValues(kvp.Value);
                }
            }
        }

        internal static string DecodeResourcePath(string resourcePath) => WebUtility.UrlDecode(resourcePath
            // Convert any + signs to percent encoding before URL decoding the path.
            .Replace("+", "%2B")
            // Double-escape any %2F (encoded / characters) so that they survive URL decoding the path.
            .Replace("%2F", "%252F")
            .Replace("%2f", "%252f"));
    }
}
