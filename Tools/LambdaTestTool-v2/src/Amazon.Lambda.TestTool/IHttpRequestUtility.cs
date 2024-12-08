namespace Amazon.Lambda.TestTool
{
    /// <summary>
    /// Defines the contract for a utility class that handles HTTP request processing in the context of API Gateway emulation.
    /// </summary>
    public interface IHttpRequestUtility
    {
        /// <summary>
        /// Determines whether the specified content type represents binary content.
        /// </summary>
        /// <param name="contentType">The content type to check.</param>
        /// <returns>True if the content type represents binary content; otherwise, false.</returns>
        bool IsBinaryContent(string? contentType);

        /// <summary>
        /// Reads the body of the HTTP request as a string.
        /// </summary>
        /// <param name="request">The HTTP request.</param>
        /// <returns>The body of the request as a string.</returns>
        string ReadRequestBody(HttpRequest request);

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
        (IDictionary<string, string>, IDictionary<string, IList<string>>) ExtractHeaders(IHeaderDictionary headers);

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
        (IDictionary<string, string>, IDictionary<string, IList<string>>) ExtractQueryStringParameters(IQueryCollection query);
    }
}
