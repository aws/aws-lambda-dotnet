using System;
using System.Collections.Generic;
using System.Text;

using System.Runtime.Serialization;

namespace Amazon.Lambda.APIGatewayEvents
{
    /// <summary>
    /// The response object for Lambda functions handling request from API Gateway HTTP API v2 proxy format
    /// https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-integrations-lambda.html
    /// </summary>
    [DataContract]
    public class APIGatewayHttpApiV2ProxyResponse
    {
        /// <summary>
        /// The HTTP status code for the request
        /// </summary>
        [DataMember(Name = "statusCode")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("statusCode")]
#endif
        public int StatusCode { get; set; }

        /// <summary>
        /// The Http headers returned in the response. Multiple header values set for the the same header should be separate by a comma.
        /// </summary>
        [DataMember(Name = "headers")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("headers")]
#endif
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Utility method to set a single or multiple values for a header to the Headers collection. 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="value"></param>
        /// <param name="append">If true it will append the values to the existing value in the Headers collection.</param>
        public void SetHeaderValues(string headerName, string value, bool append)
        {
            SetHeaderValues(headerName, new string[] { value }, append);
        }

        /// <summary>
        /// Utility method to set a single or multiple values for a header to the Headers collection. 
        /// </summary>
        /// <param name="headerName"></param>
        /// <param name="values"></param>
        /// <param name="append">If true it will append the values to the existing value in the Headers collection.</param>
        public void SetHeaderValues(string headerName, IEnumerable<string> values, bool append)
        {
            if (this.Headers == null)
                this.Headers = new Dictionary<string, string>();

            if(this.Headers.ContainsKey(headerName) && append)
            {
                this.Headers[headerName] = this.Headers[headerName] + "," + string.Join(",", values);
            }
            else
            {
                this.Headers[headerName] = string.Join(",", values);
            }
        }

        /// <summary>
        /// The cookies returned in the response.
        /// </summary>
        [DataMember(Name = "cookies")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("cookies")]
#endif
        public string[] Cookies { get; set; }

        /// <summary>
        /// The response body
        /// </summary>
        [DataMember(Name = "body")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("body")]
#endif
        public string Body { get; set; }

        /// <summary>
        /// Flag indicating whether the body should be treated as a base64-encoded string
        /// </summary>
        [DataMember(Name = "isBase64Encoded")]
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("isBase64Encoded")]
#endif
        public bool IsBase64Encoded { get; set; }
    }
}
