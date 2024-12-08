namespace Amazon.Lambda.TestTool
{
    /// <summary>
    /// Utility class for handling HTTP requests in the context of API Gateway emulation.
    /// </summary>
    public class HttpRequestUtility : IHttpRequestUtility
    {
        /// <summary>
        /// Determines whether the specified content type represents binary content.
        /// </summary>
        /// <param name="contentType">The content type to check.</param>
        /// <returns>True if the content type represents binary content; otherwise, false.</returns>
        public bool IsBinaryContent(string? contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                   contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                   contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                   contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the body of the HTTP request as a string.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The body of the request as a string.</returns>
        public string ReadRequestBody(HttpRequest request)
        {
            using (var reader = new StreamReader(request.Body))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Extracts headers from the request, separating them into single-value and multi-value dictionaries.
        /// </summary>
        /// <param name="headers">The request headers.</param>
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
        public (IDictionary<string, string>, IDictionary<string, IList<string>>) ExtractHeaders(IHeaderDictionary headers)
        {
            var singleValueHeaders = new Dictionary<string, string>();
            var multiValueHeaders = new Dictionary<string, IList<string>>();

            foreach (var header in headers)
            {
                singleValueHeaders[header.Key] = header.Value.Last();
                multiValueHeaders[header.Key] = header.Value.ToList();
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
        public (IDictionary<string, string>, IDictionary<string, IList<string>>) ExtractQueryStringParameters(IQueryCollection query)
        {
            var singleValueParams = new Dictionary<string, string>();
            var multiValueParams = new Dictionary<string, IList<string>>();

            foreach (var param in query)
            {
                singleValueParams[param.Key] = param.Value.Last();
                multiValueParams[param.Key] = [.. param.Value];
            }

            return (singleValueParams, multiValueParams);
        }
    }
}
