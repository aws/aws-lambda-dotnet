using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
#if NET6_0_OR_GREATER
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

// This assembly is also used in the source generator which needs the .NET attributes and type infos defined in this project.
// The source generator requires all dependencies to target .NET Standard 2.0 to be compatible with .NET Framework used
// by Visual Studio. Several APIs are used in this file that are not available in .NET Standard 2.0 but the source
// generator doesn't use the methods that use those .NET APIs. Several areas in this file put stub implementations
// for .NET Standard 2.0 to allow the types to be available in the source generator but the implementations
// are not actually called in the source generator.


namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// The options used by the IHttpResult to serialize into the required format for the event source of the Lambda funtion.
    /// </summary>
    public class HttpResultSerializationOptions
    {
        /// <summary>
        /// The API Gateway protocol format used as the event source.
        /// </summary>
        public enum ProtocolFormat { 
            /// <summary>
            /// Used when a function is defined with the RestApiAttribute.
            /// </summary>
            RestApi, 
            /// <summary>
            /// Used when a function is defined with the HttpApiAttribute.
            /// </summary>
            HttpApi 
        }

        /// <summary>
        /// The API Gateway protocol version.
        /// </summary>
        public enum ProtocolVersion {
            /// <summary>
            /// V1 format for API Gateway Proxy responses. Used for functions defined with RestApiAttribute or HttpApiAttribute with explicit setting to V1.
            /// </summary>
            V1, 
            /// <summary>
            /// V2 format for API Gateway Proxy responses. Used for functions defined with HttpApiAttribute with an implicit version of an explicit setting to V2.
            /// </summary>
            V2 
        }

        /// <summary>
        /// The API Gateway protocol used as the event source. 
        ///     RestApi -> RestApiAttrbute
        ///     HttpApi -> HttpApiAttribute
        /// </summary>
        public ProtocolFormat Format { get; set; }

        /// <summary>
        /// The API Gateway protocol version used as the event source. 
        ///     V1 -> RestApi or HttpApi specifically set as V1
        ///     V2 -> HttpApi either implicit or explicit set to V2
        /// </summary>
        public ProtocolVersion Version { get; set; }
    }

    /// <summary>
    /// If this inteface is returned for an API Gateway Lambda function it will serialize itself to the correct JSON format for the 
    /// configured event source's protocol format and version.
    /// 
    /// Users should use the implementation class HttpResults to construct an instance of IHttpResult with the configured, status code, response body and headers.
    /// </summary>
    /// <example>
    /// return HttpResults.Ok("All Good")
    ///                   .AddHeader("Custom-Header", "FooBar");
    /// 
    /// </example>
    public interface IHttpResult
    {
        /// <summary>
        /// Used by the Lambda Annotations framework to serialize the IHttpResult to the correct JSON response.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        Stream Serialize(HttpResultSerializationOptions options);

        /// <summary>
        /// Add header to the IHttpResult. The AddHeader method can be called multiple times for the same header to add multi values for a header.
        /// HTTP header names are case insensitive and the AddHeader method will normalize header name casing by calling ToLower on them.
        /// </summary>
        /// <param name="name">HTTP header name</param>
        /// <param name="value">HTTP header value</param>
        /// <returns>The same instance to allow fluent call pattern.</returns>
        IHttpResult AddHeader(string name, string value);
    }

    /// <summary>
    /// Implementation class for IHttpResult. Consumers should use one of the static methods to create the a result with the desired status code.
    /// </summary>
    /// <remarks>
    /// If a response body is provided it is format using the following rules:
    /// <list type="bullet">
    ///     <item>
    ///         <description>For string then returned as is.</description>
    ///     </item>
    ///     <item>
    ///         <description>For Stream, byte[] or IList&lt;byte&gt; the data is consided binary and base 64 encoded.</description>
    ///     </item>
    ///     <item>
    ///         <description>Anything other type is serialized to JSON.</description>
    ///     </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// return HttpResults.Ok("All Good")
    ///                   .AddHeader("Custom-Header", "FooBar");
    /// 
    /// </example>
    public class HttpResults : IHttpResult
    {
        private const string HEADER_NAME_CONTENT_TYPE = "content-type";
        private const string CONTENT_TYPE_APPLICATION_JSON = "application/json";
        private const string CONTENT_TYPE_TEXT_PLAIN = "text/plain";
        private const string CONTENT_TYPE_APPLICATION_OCTET_STREAM = "application/octet-stream";

        private HttpStatusCode _statusCode;
        private string _body;
        private IDictionary<string, IList<string>> _headers;
        private bool _isBase64Encoded;
        private string _defaultContentType;

        private HttpResults(HttpStatusCode statusCode, object body = null)
        {
            _statusCode = statusCode;

            FormatBody(body);
        }

        /// <inheritdoc/>
        public IHttpResult AddHeader(string name, string value)
        {
            name = name.ToLower();

            if (_headers == null)
            {
                _headers = new Dictionary<string, IList<string>>();
            }

            if (!_headers.TryGetValue(name, out var values))
            {
                values = new List<string>();
                _headers[name] = values;
            }
            values.Add(value);

            return this;
        }

        /// <summary>
        /// Creates an IHttpResult for a Accepted (202) status code.
        /// </summary>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult Accepted(object body = null)
        {
            return new HttpResults(HttpStatusCode.Accepted, body);
        }

        /// <summary>
        /// Creates an IHttpResult for a BadRequest (400) status code.
        /// </summary>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult BadRequest(object body = null)
        {
            return new HttpResults(HttpStatusCode.BadRequest, body);
        }

        /// <summary>
        /// Creates an IHttpResult for a Conflict (409) status code.
        /// </summary>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult Conflict(object body = null)
        {
            return new HttpResults(HttpStatusCode.Conflict, body);
        }

        /// <summary>
        /// Creates an IHttpResult for a Created (201) status code.
        /// </summary>
        /// <param name="uri">Optional URI for the created resource. The value is set to the Location response header.</param>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult Created(string uri = null, object body = null)
        {
            var result = new HttpResults(HttpStatusCode.Created, body);
            if (uri != null)
            {
                result.AddHeader("location", uri);
            }

            return result;
        }

        /// <summary>
        /// Creates an IHttpResult for a Forbidden (403) status code.
        /// </summary>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult Forbid(object body = null)
        {
            return new HttpResults(HttpStatusCode.Forbidden, body);
        }

        /// <summary>
        /// Creates an IHttpResult for a NotFound (404) status code.
        /// </summary>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult NotFound(object body = null)
        {
            return new HttpResults(HttpStatusCode.NotFound, body);
        }

        /// <summary>
        /// Creates an IHttpResult for a Ok (200) status code.
        /// </summary>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult Ok(object body = null)
        {
            return new HttpResults(HttpStatusCode.OK, body);
        }

        /// <summary>
        /// Creates an IHttpResult for redirect responses.
        /// </summary>
        /// <remarks>
        /// This method uses the same logic for determing the the Http status code as the Microsoft.AspNetCore.Http.TypedResults.Redirect uses.
        /// https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.typedresults.redirect
        /// </remarks>
        /// <param name="uri">The URI to redirect to. The value will be set in the location header.</param>
        /// <param name="permanent">Whether the redirect should be a permanet (301) or temporary (302) redirect.</param>
        /// <param name="preserveMethod">Whether the request method should be preserved. If set to true use 308 for permanent or 307 for temporary redirects.</param>
        /// <returns></returns>
        public static IHttpResult Redirect(string uri, bool permanent = false, bool preserveMethod = false)
        {
            HttpStatusCode code;
            if (permanent && preserveMethod)
            {
                code = (HttpStatusCode)308; // .NET Standard 2.0 does not have the enum value for PermanentRedirect so using direct number;
            }
            else if (!permanent && preserveMethod)
            {
                code = HttpStatusCode.TemporaryRedirect;
            }
            else if (permanent && !preserveMethod)
            {
                code = HttpStatusCode.MovedPermanently;
            }
            else
            {
                code = HttpStatusCode.Redirect;
            }


            var result = new HttpResults(code, null);
            if (uri != null)
            {
                result.AddHeader("location", uri);
            }

            return result;
        }

        /// <summary>
        /// Creates an IHttpResult for a Unauthorized (401) status code.
        /// </summary>
        /// <returns></returns>
        public static IHttpResult Unauthorized()
        {
            return new HttpResults(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Creates an IHttpResult for the specified status code.
        /// </summary>
        /// <param name="statusCode">Http status code used to create the IHttpResult instance.</param>
        /// <param name="body">Optional response body</param>
        /// <returns></returns>
        public static IHttpResult NewResult(HttpStatusCode statusCode, object body = null)
        {
            return new HttpResults(statusCode, body);
        }


    #region Serialization

        // See comment in class documentation on the rules for serializing. If any changes are made in this method be sure to update
        // the comment above.
        private void FormatBody(object body)
        {
        // See comment at the top about .NET Standard 2.0
#if NETSTANDARD2_0
            throw new NotImplementedException();
#else

            if (body == null)
                return;

            if (body is string str)
            {
                _defaultContentType = CONTENT_TYPE_TEXT_PLAIN;
                _body = str;
            }
            else if (body is Stream stream)
            {
                _defaultContentType = CONTENT_TYPE_APPLICATION_OCTET_STREAM;
                _isBase64Encoded = true;
                var buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);
                try
                {
                    var readLength = stream.Read(buffer, 0, buffer.Length);
                    _body = Convert.ToBase64String(buffer, 0, readLength);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else if (body is byte[] binaryData)
            {
                _defaultContentType = CONTENT_TYPE_APPLICATION_OCTET_STREAM;
                _isBase64Encoded = true;
                _body = Convert.ToBase64String(binaryData, 0, binaryData.Length);
            }
            else if (body is IList<byte> listBinaryData)
            {
                _defaultContentType = CONTENT_TYPE_APPLICATION_OCTET_STREAM;
                _isBase64Encoded = true;
                _body = Convert.ToBase64String(listBinaryData.ToArray(), 0, listBinaryData.Count);
            }
            else
            {
                _defaultContentType = CONTENT_TYPE_APPLICATION_JSON;
                _body = JsonSerializer.Serialize(body);
            }
#endif
        }

        /// <summary>
        /// Serialize the IHttpResult into the expect format for the event source.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public Stream Serialize(HttpResultSerializationOptions options)
        {
        // See comment at the top about .NET Standard 2.0
#if NETSTANDARD2_0
            throw new NotImplementedException();
#else
            // If the user didn't explicit set the content type then default to application/json
            if(!string.IsNullOrEmpty(_body) && (_headers == null || !_headers.ContainsKey(HEADER_NAME_CONTENT_TYPE)))
            {
                AddHeader(HEADER_NAME_CONTENT_TYPE, _defaultContentType);
            }

            var stream = new MemoryStream();
            if (options.Format == HttpResultSerializationOptions.ProtocolFormat.RestApi ||
                (options.Format == HttpResultSerializationOptions.ProtocolFormat.HttpApi && options.Version == HttpResultSerializationOptions.ProtocolVersion.V1))
            {
                var response = new APIGatewayV1Response
                {
                    StatusCode = (int)_statusCode,
                    Body = _body,
                    MultiValueHeaders = _headers,
                    IsBase64Encoded = _isBase64Encoded
                };

                JsonSerializer.Serialize(stream, response);
            }
            else
            {
                var response = new APIGatewayV2Response
                {
                    StatusCode = (int)_statusCode,
                    Body = _body,
                    Headers = ConvertToV2MultiValueHeaders(_headers),
                    IsBase64Encoded = _isBase64Encoded
                };

                JsonSerializer.Serialize(stream, response);
            }

            stream.Position = 0;
            return stream;
#endif
        }

        /// <summary>
        /// The V2 format used by HttpApi handles multi value headers by having the value be comma delimited. This 
        /// utility method handles converting the collection from the V1 format to the V2.
        /// </summary>
        /// <param name="v1MultiHeaders"></param>
        /// <returns></returns>
        private static IDictionary<string, string> ConvertToV2MultiValueHeaders(IDictionary<string, IList<string>> v1MultiHeaders)
        {
            if (v1MultiHeaders == null)
                return null;

            var v2MultiHeaders = new Dictionary<string, string>();
            foreach (var kvp in v1MultiHeaders)
            {
                var values = string.Join(",", kvp.Value);
                v2MultiHeaders[kvp.Key] = values;
            }

            return v2MultiHeaders;
        }

        // See comment at the top about .NET Standard 2.0
#if !NETSTANDARD2_0
        // Class representing the V1 API Gateway response. Very similiar to Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse but this library can
        // not take a dependency on Amazon.Lambda.APIGatewayEvents so it has to have its own version.
        private class APIGatewayV1Response
        {
            [JsonPropertyName("statusCode")]
            public int StatusCode { get; set; }

            [JsonPropertyName("multiValueHeaders")]
            public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

            [JsonPropertyName("body")]
            public string Body { get; set; }

            [JsonPropertyName("isBase64Encoded")]
            public bool IsBase64Encoded { get; set; }
        }

        // Class representing the V2 API Gateway response. Very similiar to Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse but this library can
        // not take a dependency on Amazon.Lambda.APIGatewayEvents so it has to have its own version.
        private class APIGatewayV2Response
        {
            [JsonPropertyName("statusCode")]
            public int StatusCode { get; set; }

            [JsonPropertyName("headers")]
            public IDictionary<string, string> Headers { get; set; }

            public string[] Cookies { get; set; }

            [JsonPropertyName("body")]
            public string Body { get; set; }

            [JsonPropertyName("isBase64Encoded")]
            public bool IsBase64Encoded { get; set; }
        }
#endif
#endregion
    }
}