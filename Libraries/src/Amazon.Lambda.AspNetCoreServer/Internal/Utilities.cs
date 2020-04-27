using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

        internal static void EnsureLambdaServerRegistered(IServiceCollection services)
        {
            IList<ServiceDescriptor> toRemove = new List<ServiceDescriptor>();
            var serviceDescriptions = services.Where(x => x.ServiceType == typeof(IServer));
            int lambdaServiceCount = 0;

            // There can be more then one IServer implementation registered if the consumer called ConfigureWebHostDefaults in the Init override for the IHostBuilder.
            // This makes sure there is only one registered IServer using LambdaServer and removes any other registrations.
            foreach (var serviceDescription in serviceDescriptions)
            {
                // If Lambda server has already been added the skip out.
                if (serviceDescription.ImplementationType == typeof(LambdaServer))
                {
                    lambdaServiceCount++;
                    if(lambdaServiceCount > 1)
                    {
                        toRemove.Add(serviceDescription);
                    }
                }                        
                // If there is an IServer registered that isn't LambdaServer then remove it. This is mostly likely caused
                // by leaving the UseKestrel call.
                else
                {
                    toRemove.Add(serviceDescription);
                }
            }

            foreach(var serviceDescription in toRemove)
            {
                services.Remove(serviceDescription);
            }

            if(lambdaServiceCount == 0)
            {
                services.AddSingleton<IServer, LambdaServer>();
            }
        }

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

        /// <summary>
        /// The RawQueryString property from the HTTP API V2 request is used instead of the QueryStringParameters
        /// because QueryStringParameters url decodes the parameters taking out + from timestamps. ASP.NET Core
        /// does expect the query string values to be URL encoded so the RawQueryString has to broken down into
        /// pieces so the individual query string values can be url encoding and the recombined together.
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        public static string CreateQueryStringParametersFromHttpApiV2(string queryString)
        {
            if (string.IsNullOrEmpty(queryString))
                return null;

            var sb = new StringBuilder("?");
            foreach (var parameter in queryString.Split('&'))
            {
                if (sb.Length > 1)
                {
                    sb.Append("&");
                }

                int pos = parameter.IndexOf('=');
                if(pos == -1)
                {
                    sb.Append(parameter);
                }
                else
                {
                    var name = parameter.Substring(0, pos);
                    var value = parameter.Substring(pos + 1);
                    sb.Append($"{name}={WebUtility.UrlEncode(value)}");
                }
            }

            return sb.ToString();
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
