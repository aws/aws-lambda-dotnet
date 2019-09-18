﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        // This code is taken from the Apache 2.0 licensed ASP.NET Core repo.
        // https://github.com/aspnet/AspNetCore/blob/d7bfbb5824b5f8876bcd4afaa29a611efc7aa1c9/src/Http/Shared/StreamCopyOperationInternal.cs
        internal static async Task CopyToAsync(Stream source, Stream destination, long? count, int bufferSize, CancellationToken cancel)
        {
            long? bytesRemaining = count;

            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                Debug.Assert(source != null);
                Debug.Assert(destination != null);
                Debug.Assert(!bytesRemaining.HasValue || bytesRemaining.GetValueOrDefault() >= 0);
                Debug.Assert(buffer != null);

                while (true)
                {
                    // The natural end of the range.
                    if (bytesRemaining.HasValue && bytesRemaining.GetValueOrDefault() <= 0)
                    {
                        return;
                    }

                    cancel.ThrowIfCancellationRequested();

                    int readLength = buffer.Length;
                    if (bytesRemaining.HasValue)
                    {
                        readLength = (int)Math.Min(bytesRemaining.GetValueOrDefault(), (long)readLength);
                    }
                    int read = await source.ReadAsync(buffer, 0, readLength, cancel);

                    if (bytesRemaining.HasValue)
                    {
                        bytesRemaining -= read;
                    }

                    // End of the source stream.
                    if (read == 0)
                    {
                        return;
                    }

                    cancel.ThrowIfCancellationRequested();

                    await destination.WriteAsync(buffer, 0, read, cancel);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
