using System;
using System.Collections.Generic;

namespace Amazon.Lambda.ApplicationLoadBalancerEvents
{
    /// <summary>
    /// For request coming in from Application Load Balancer.
    /// https://docs.aws.amazon.com/elasticloadbalancing/latest/application/lambda-functions.html
    /// </summary>
    public class ApplicationLoadBalancerRequest
    {
        /// <summary>
        /// The request context for the request
        /// </summary>
        public ALBRequestContext RequestContext { get; set; }
        
        /// <summary>
        /// The url path for the caller
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The HTTP method used
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// The headers sent with the request
        /// Note: Use this property when "Multi value headers" is disabled on ELB Target Group.
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The headers sent with the request
        /// Note: Use this property when "Multi value headers" is enabled on ELB Target Group.
        /// </summary>
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

        /// <summary>
        /// The query string parameters that were part of the request
        /// Note: Use this property when "Multi value headers" is disabled on ELB Target Group.
        /// </summary>
        public IDictionary<string, string> QueryStringParameters { get; set; }

        /// <summary>
        /// The query string parameters that were part of the request
        /// Note: Use this property when "Multi value headers" is enabled on ELB Target Group.
        /// </summary>
        public IDictionary<string, IList<string>> MultiValueQueryStringParameters { get; set; }

        /// <summary>
        /// The HTTP request body.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// True if the body of the request is base 64 encoded.
        /// </summary>
        public bool IsBase64Encoded { get; set; }

        /// <summary>
        /// Information from the AWS resources invoke the Lambda function.
        /// </summary>
        public class ALBRequestContext
        {
            /// <summary>
            /// Information about the source Application Load Balancer.
            /// </summary>
            public ElbInfo Elb { get; set; }            
        }
        
        /// <summary>
        /// Information from the source Elastic Load Balancer.
        /// </summary>
        public class ElbInfo
        {
            /// <summary>
            /// The Application Load Balancer target group arn.
            /// </summary>
            public string TargetGroupArn { get; set; }            
        }        
    }
}