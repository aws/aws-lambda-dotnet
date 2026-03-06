// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#if NET8_0_OR_GREATER
using System.Collections.Generic;
using System.Net;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Amazon.Lambda.Core.ResponseStreaming
{
    /// <summary>
    /// The HTTP response prelude to be sent as the first chunk of a streaming response when using <see cref="LambdaResponseStreamFactory.CreateHttpStream"/>.
    /// </summary>
    [RequiresPreviewFeatures(LambdaResponseStreamFactory.ParameterizedPreviewMessage)]
    public class HttpResponseStreamPrelude
    {
        /// <summary>
        /// The Http status code to include in the response prelude.
        /// </summary>
        public HttpStatusCode? StatusCode { get; set; }

        /// <summary>
        /// The response headers to include in the response prelude. This collection supports setting single value for the same headers.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The response headers to include in the response prelude. This collection supports setting multiple values for the same headers.
        /// </summary>
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; } = new Dictionary<string, IList<string>>();

        /// <summary>
        /// The list of cookies to include in the response prelude. This is used for Lambda Function URL responses, which support a separate "cookies" field in the response JSON for setting cookies, rather than requiring cookies to be set via the "Set-Cookie" header.
        /// </summary>
        public IList<string> Cookies { get; set; } = new List<string>();

        internal byte[] ToByteArray()
        {
            var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                writer.WriteStartObject();

                if (StatusCode.HasValue)
                    writer.WriteNumber("statusCode", (int)StatusCode);

                if (Headers?.Count > 0)
                {
                    writer.WriteStartObject("headers");
                    foreach (var header in Headers)
                    {
                        writer.WriteString(header.Key, header.Value);
                    }
                    writer.WriteEndObject();
                }

                if (MultiValueHeaders?.Count > 0)
                {
                    writer.WriteStartObject("multiValueHeaders");
                    foreach (var header in MultiValueHeaders)
                    {
                        writer.WriteStartArray(header.Key);
                        foreach (var value in header.Value)
                        {
                            writer.WriteStringValue(value);
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }

                if (Cookies?.Count > 0)
                {
                    writer.WriteStartArray("cookies");
                    foreach (var cookie in Cookies)
                    {
                        writer.WriteStringValue(cookie);
                    }
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return bufferWriter.WrittenSpan.ToArray();
        }
    }
}
#endif
