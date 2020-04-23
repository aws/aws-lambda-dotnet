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
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("statusCode")]
#endif
        public int StatusCode { get; set; }
        
        /// <summary>
        /// The HTTP status description for the request
        /// </summary>
        [DataMember(Name = "statusDescription")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("statusDescription")]
#endif
        public string StatusDescription { get; set; }

        /// <summary>
        /// The Http headers return in the response
        /// Note: Use this property when "Multi value headers" is disabled on ELB Target Group.
        /// </summary>
        [DataMember(Name = "headers")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("headers")]
#endif
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The Http headers return in the response
        /// Note: Use this property when "Multi value headers" is enabled on ELB Target Group.
        /// </summary>
        [DataMember(Name = "multiValueHeaders")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("multiValueHeaders")]
#endif
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

        /// <summary>
        /// The response body
        /// </summary>
        [DataMember(Name = "body")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("body")]
#endif
        public string Body { get; set; }

        /// <summary>
        /// Flag indicating whether the body should be treated as a base64-encoded string
        /// </summary>
        [DataMember(Name = "isBase64Encoded")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("isBase64Encoded")]
#endif
        public bool IsBase64Encoded { get; set; }        
    }
}