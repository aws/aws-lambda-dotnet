using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.ApplicationLoadBalancerEvents
{    
    /// <summary>
    /// For response object for Lambda functions handling request from Application Load Balancer.
    /// https://docs.aws.amazon.com/elasticloadbalancing/latest/application/lambda-functions.html
    /// </summary>
    [DataContract]
    public class ApplicationLoadBalancerResponse
    {
        /// <summary>
        /// The HTTP status code for the request
        /// </summary>
        [DataMember(Name = "statusCode")]
        [System.Text.Json.Serialization.JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }
        
        /// <summary>
        /// The HTTP status description for the request
        /// </summary>
        [DataMember(Name = "statusDescription")]
        [System.Text.Json.Serialization.JsonPropertyName("statusDescription")]
        public string StatusDescription { get; set; }

        /// <summary>
        /// The Http headers return in the response
        /// Note: Use this property when "Multi value headers" is disabled on ELB Target Group.
        /// </summary>
        [DataMember(Name = "headers")]
        [System.Text.Json.Serialization.JsonPropertyName("headers")]
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The Http headers return in the response
        /// Note: Use this property when "Multi value headers" is enabled on ELB Target Group.
        /// </summary>
        [DataMember(Name = "multiValueHeaders")]
        [System.Text.Json.Serialization.JsonPropertyName("multiValueHeaders")]
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

        /// <summary>
        /// The response body
        /// </summary>
        [DataMember(Name = "body")]
        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>
        /// Flag indicating whether the body should be treated as a base64-encoded string
        /// </summary>
        [DataMember(Name = "isBase64Encoded")]
        [System.Text.Json.Serialization.JsonPropertyName("isBase64Encoded")]
        public bool IsBase64Encoded { get; set; }        
    }
}
