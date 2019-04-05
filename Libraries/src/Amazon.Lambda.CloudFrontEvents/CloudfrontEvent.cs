namespace Amazon.Lambda.CloudFrontEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The event object for Lambda functions handling request from CloudFront
    /// https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/lambda-event-structure.html
    /// </summary>
    public class CloudFrontEvent
    {
        /// <summary>
        /// Event Records
        /// </summary>
        public List<RecordEntity> Records { get; set; }

        /// <summary>
        /// Event Record
        /// </summary>
        public class RecordEntity
        {
            /// <summary>
            /// CloudFront Entity
            /// </summary>
            public CfEntity Cf { get; set; }
        }

        /// <summary>
        /// CloudFront Entity
        /// </summary>
        public class CfEntity
        {
            /// <summary>
            /// CloudFront Config
            /// </summary>
            public ConfigEntity Config { get; set; }

            /// <summary>
            /// request – One of the following:: Viewer request – The request that CloudFront received from the viewer and that might have been modified by the Lambda function that was triggered by a viewer request event Origin request – The request that CloudFront forwarded to the origin and that might have been modified by the Lambda function that was triggered by an origin request event
            /// </summary>
            public RequestEntity Request { get; set; }

            /// <summary>
            /// response – One of the following:: Viewer response – The response that CloudFront will return to the viewer for viewer response events. Origin response – The response that CloudFront received from the origin for origin response events.
            /// </summary>
            public ResponseEntity Response { get; set; }
        }

        /// <summary>
        /// CloudFront Config
        /// </summary>
        public class ConfigEntity
        {
            /// <summary>
            /// distributionDomainName (read-only): The domain name of the distribution that's associated with the request.
            /// </summary>
            public string DistributionDomainName { get; set; }

            /// <summary>
            /// distributionID (read-only): The ID of the distribution that's associated with the request.
            /// </summary>
            public string DistributionId { get; set; }

            /// <summary>
            /// eventType (read-only): The type of trigger that's associated with the request.
            /// </summary>
            public string EventType { get; set; }

            /// <summary>
            /// requestId (read-only, viewer request events only): An encrypted string that uniquely identifies a request. The requestId value also appears in CloudFront access logs as x-edge-request-id. For more information, see Configuring and Using Access Logs and Web Distribution Log File Format.
            /// </summary>
            public string RequestId { get; set; }
        }

        /// <summary>
        /// CloudFront Request
        /// </summary>
        public class RequestEntity
        {
            /// <summary>
            /// clientIp (read-only): The IP address of the viewer that made the request. If the viewer used an HTTP proxy or a load balancer to send the request, the value is the IP address of the proxy or load balancer.
            /// </summary>
            public string ClientIp { get; set; }

            /// <summary>
            /// headers (read/write): The headers in the request. Note the following:
            /// </summary>
            public Dictionary<string, List<HeaderEntity>> Headers { get; set; }

            /// <summary>
            /// method (read-only): The HTTP method of the viewer request.
            /// </summary>
            public string Method { get; set; }

            /// <summary>
            /// querystring: The query string, if any, that CloudFront received in the viewer request. If the viewer request doesn't include a query string, the event structure still includes querystring with an empty value.
            /// </summary>
            public string Querystring { get; set; }

            /// <summary>
            /// uri (read/write): The relative path of the requested object.
            /// </summary>
            public string Uri { get; set; }

            /// <summary>
            /// body
            /// </summary>
            public BodyEntity Body { get; set; }

            /// <summary>
            /// origin
            /// </summary>
            public OriginEntity Origin { get; set; }
        }

        /// <summary>
        /// CloudFront Response
        /// </summary>
        public class ResponseEntity
        {
            /// <summary>
            /// status: The HTTP status code that CloudFront returns to the viewer.
            /// </summary>
            public string Status { get; set; }

            /// <summary>
            /// statusDescription: The HTTP status description that CloudFront returns to the viewer.
            /// </summary>
            public string StatusDescription { get; set; }

            /// <summary>
            /// headers: Headers that you want CloudFront to return in the generated response.
            /// </summary>
            public Dictionary<string, List<HeaderEntity>> Headers { get; set; }
        }

        /// <summary>
        /// CloudFront Generatted Response
        /// </summary>
        public class GeneratedResponseEntity
        {
            /// <summary>
            /// status: The HTTP status code that CloudFront returns to the viewer.
            /// </summary>
            public string Status { get; set; }

            /// <summary>
            /// statusDescription: The HTTP status description that CloudFront returns to the viewer.
            /// </summary>
            public string StatusDescription { get; set; }

            /// <summary>
            /// body (read/write): The request body content.
            /// </summary>
            public string Body { get; set; }

            /// <summary>
            /// bodyEncoding (read/write): The encoding for the body. When Lambda@Edge exposes the body to the Lambda function, it first converts the body to base64 encoding. If you choose replace for the action to replace the body, you can opt to use text or base64 (the default) encoding.
            /// </summary>
            public string BodyEncoding { get; set; }

            /// <summary>
            /// headers: Headers that you want CloudFront to return in the generated response.
            /// </summary>
            public Dictionary<string, List<HeaderEntity>> Headers { get; set; }
        }

        /// <summary>
        /// CloudFront Header
        /// </summary>
        public class HeaderEntity
        {
            /// <summary>
            /// key (optional) is the case-sensitive name of the header as it appears in an HTTP request
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// value as a header value
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// CloudFront Body
        /// </summary>
        public class BodyEntity
        {
            /// <summary>
            /// inputTruncated (read-only): A Boolean flag that indicates if the body was truncated by Lambda@Edge. For more information, see Size Limits for Body with the Include Body Option.
            /// </summary>
            public bool InputTruncated { get; set; }

            /// <summary>
            /// action (read/write): The action that you intend to take with the body. The options for action are the following:
            /// </summary>
            public string Action { get; set; }

            /// <summary>
            /// encoding (read/write): The encoding for the body. When Lambda@Edge exposes the body to the Lambda function, it first converts the body to base64 encoding. If you choose replace for the action to replace the body, you can opt to use text or base64 (the default) encoding.
            /// </summary>
            public string Encoding { get; set; }

            /// <summary>
            /// data (read/write): The request body content.
            /// </summary>
            public string Data { get; set; }
        }

        /// <summary>
        /// CloudFront Origin
        /// </summary>
        public class OriginEntity
        {
            /// <summary>
            /// CloudFront Custom Origin
            /// </summary>
            public CustomOriginEntity Custom { get; set; }

            /// <summary>
            /// CloudFront S3 Origin
            /// </summary>
            public S3OriginEntity S3 { get; set; }
        }

        /// <summary>
        /// CloudFront Custom Origin
        /// </summary>
        public class CustomOriginEntity
        {
            /// <summary>
            /// customHeaders: You can include custom headers with the request by specifying a header name and value pair for each custom header. You can't add headers that are blacklisted for origin custom headers or hooks, and a header with the same name can't be present in request.headers or in request.origin.custom.customHeaders. The restrictions for request.headers also apply to custom headers. For more information, see Custom Headers that CloudFront Can't Forward to Your Origin and Blacklisted Headers.
            /// </summary>
            public Dictionary<string, List<HeaderEntity>> CustomHeaders { get; set; }

            /// <summary>
            /// domainName: The domain name of the origin server, like www.example.com. The domain name can't be empty, can't include a colon (:), and can't use the IPV4 address format. The domain name can be up to 253 characters.
            /// </summary>
            public string DomainName { get; set; }

            /// <summary>
            /// keepaliveTimeout: How long, in seconds, that CloudFront should try to maintain the connection to your origin after receiving the last packet of a response. The value must be a number in the range of 1 to 60 seconds.
            /// </summary>
            public int KeepaliveTimeout { get; set; }

            /// <summary>
            /// path: The directory path at the server where the request should locate content. The path should start with a slash (/) but should have no trailing / (like path/). The path should be URL encoded, with a maximum length of 255 characters.
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// port: The port at your custom origin. The port must be 80 or 443, or a number in the range of 1024 to 65535.
            /// </summary>
            public int Port { get; set; }

            /// <summary>
            /// protocol (origin requests only): The origin protocol policy that CloudFront should use when fetching objects from your origin. The value can be http or https.
            /// </summary>
            public string Protocol { get; set; }

            /// <summary>
            /// readTimeout: How long, in seconds, CloudFront should wait for a response after forwarding a request to the origin, and how long CloudFront should wait after receiving a packet of a response before receiving the next packet. The value must be a number in the range of 4 to 60 seconds.
            /// </summary>
            public int ReadTimeout { get; set; }

            /// <summary>
            /// sslProtocols: The SSL protocols that CloudFront can use when establishing an HTTPS connection with your origin. Values can be the following: TLSv1.2, TLSv1.1, TLSv1, SSLv3.
            /// </summary>
            public List<string> SslProtocols { get; set; }
        }

        /// <summary>
        /// CloudFront S3 Origin
        /// </summary>
        public class S3OriginEntity
        {
            /// <summary>
            /// authMethod: Set to origin-access-identity if your Amazon S3 bucket has an origin access identity (OAI) set up, or none if you aren’t using OAI. If you set authMethod to origin-access-identity, there are several requirements:
            /// </summary>
            public string AuthMethod { get; set; }

            /// <summary>
            /// customHeaders: You can include custom headers with the request by specifying a header name and value pair for each custom header. You can't add headers that are blacklisted for origin custom headers or hooks, and a header with the same name can't be present in request.headers or in request.origin.custom.customHeaders. The restrictions for request.headers also apply to custom headers. For more information, see Custom Headers that CloudFront Can't Forward to Your Origin and Blacklisted Headers.
            /// </summary>
            public Dictionary<string, List<HeaderEntity>> CustomHeaders { get; set; }

            /// <summary>
            /// domainName: The domain name of the Amazon S3 origin server, like my-bucket.s3.amazonaws.com. The domain name can't be empty, and must be an allowed bucket name (as defined by Amazon S3). Do not use a Region-specific endpoint, like my-bucket.s3-eu-west-1.amazonaws.com. The name can be up to 128 characters, and must be all lowercase.
            /// </summary>
            public string DomainName { get; set; }

            /// <summary>
            /// path: The directory path at the server where the request should locate content. The path should start with a slash (/) but should have no trailing / (like path/).
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// region: The Region for your Amazon S3 bucket. This is required only if you use OAI.
            /// </summary>
            public string Region { get; set; }
        }
    }
}
