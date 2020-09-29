namespace Amazon.Lambda.APIGatewayEvents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// For request coming in from API Gateway proxy
    /// http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-set-up-simple-proxy.html
    /// </summary>
    public class APIGatewayProxyRequest
    {
        /// <summary>
        /// The resource path defined in API Gateway
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public string Resource { get; set; }

        /// <summary>
        /// The url path for the caller
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The HTTP method used
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// The headers sent with the request. This collection will only contain a single value for a header. 
        /// 
        /// API Gateway will populate both the Headers and MultiValueHeaders collection for every request. If multiple values
        /// are set for a header then the Headers collection will just contain the last value.
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The headers sent with the request. This collection supports multiple values for a single header.
        /// 
        /// API Gateway will populate both the Headers and MultiValueHeaders collection for every request. If multiple values
        /// are set for a header then the Headers collection will just contain the last value.
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public IDictionary<string, IList<string>> MultiValueHeaders { get; set; }

        /// <summary>
        /// The query string parameters that were part of the request. This collection will only contain a single value for a query parameter.
        /// 
        /// API Gateway will populate both the QueryStringParameters and MultiValueQueryStringParameters collection for every request. If multiple values
        /// are set for a query parameter then the QueryStringParameters collection will just contain the last value.
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public IDictionary<string, string> QueryStringParameters { get; set; }

        /// <summary>
        /// The query string parameters that were part of the request. This collection supports multiple values for a single query parameter.
        /// 
        /// API Gateway will populate both the QueryStringParameters and MultiValueQueryStringParameters collection for every request. If multiple values
        /// are set for a query parameter then the QueryStringParameters collection will just contain the last value.
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public IDictionary<string, IList<string>> MultiValueQueryStringParameters { get; set; }

        /// <summary>
        /// The path parameters that were part of the request
        /// <para>
        /// This field is only set for REST API requests.
        /// </para>
        /// </summary>
        public IDictionary<string, string> PathParameters { get; set; }

        /// <summary>
        /// The stage variables defined for the stage in API Gateway
        /// </summary>
        public IDictionary<string, string> StageVariables { get; set; }

        /// <summary>
        /// The request context for the request
        /// </summary>
        public ProxyRequestContext RequestContext { get; set; }

        /// <summary>
        /// The HTTP request body.
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// True if the body of the request is base 64 encoded.
        /// </summary>
        public bool IsBase64Encoded { get; set; }

        /// <summary>
        /// The ProxyRequestContext contains the information to identify the AWS account and resources invoking the 
        /// Lambda function. It also includes Cognito identity information for the caller.
        /// </summary>
        public class ProxyRequestContext
        {
            /// <summary>
            /// The resource full path including the API Gateway stage
            /// <para>
            /// This field is only set for REST API requests.
            /// </para>
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// The account id that owns the executing Lambda function
            /// </summary>
            public string AccountId { get; set; }

            /// <summary>
            /// The resource id.
            /// </summary>
            public string ResourceId { get; set; }


            /// <summary>
            /// The API Gateway stage name
            /// </summary>
            public string Stage { get; set; }

            /// <summary>
            /// The unique request id
            /// </summary>
            public string RequestId { get; set; }

            /// <summary>
            /// The identity information for the request caller
            /// </summary>
            public RequestIdentity Identity { get; set; }

            /// <summary>
            /// The resource path defined in API Gateway
            /// <para>
            /// This field is only set for REST API requests.
            /// </para>
            /// </summary>
            public string ResourcePath { get; set; }

            /// <summary>
            /// The HTTP method used
            /// <para>
            /// This field is only set for REST API requests.
            /// </para>
            /// </summary>
            public string HttpMethod { get; set; }

            /// <summary>
            /// The API Gateway rest API Id.
            /// </summary>
            public string ApiId { get; set; }

            /// <summary>
            /// An automatically generated ID for the API call, which contains more useful information for debugging/troubleshooting.
            /// </summary>
            public string ExtendedRequestId { get; set; }

            /// <summary>
            /// The connectionId identifies a unique client connection in a WebSocket API.
            /// <para>
            /// This field is only set for WebSocket API requests.
            /// </para>
            /// </summary>
            public string ConnectionId { get; set; }

            /// <summary>
            /// The Epoch-formatted connection time in a WebSocket API.
            /// <para>
            /// This field is only set for WebSocket API requests.
            /// </para>
            /// </summary>
            public long ConnectionAt { get; set; }

            /// <summary>
            /// A domain name for the WebSocket API. This can be used to make a callback to the client (instead of a hard-coded value).
            /// <para>
            /// This field is only set for WebSocket API requests.
            /// </para>
            /// </summary>
            public string DomainName { get; set; }

            /// <summary>
            /// The first label of the DomainName. This is often used as a caller/customer identifier.
            /// </summary>
            public string DomainPrefix { get; set; }

            /// <summary>
            /// The event type: CONNECT, MESSAGE, or DISCONNECT.
            /// <para>
            /// This field is only set for WebSocket API requests.
            /// </para>
            /// </summary>
            public string EventType { get; set; }

            /// <summary>
            /// A unique server-side ID for a message. Available only when the $context.eventType is MESSAGE.
            /// <para>
            /// This field is only set for WebSocket API requests.
            /// </para>
            /// </summary>
            public string MessageId { get; set; }

            /// <summary>
            /// The selected route key.
            /// <para>
            /// This field is only set for WebSocket API requests.
            /// </para>
            /// </summary>
            public string RouteKey { get; set; }


            /// <summary>
            /// The APIGatewayCustomAuthorizerContext containing the custom properties set by a custom authorizer.
            /// </summary>
            public APIGatewayCustomAuthorizerContext Authorizer { get; set; }
            
            /// <summary>
            /// Gets and sets the operation name.
            /// </summary>
            public string OperationName { get; set; }
            
            /// <summary>
            /// Gets and sets the error.
            /// </summary>
            public string Error { get; set; }
            
            /// <summary>
            /// Gets and sets the integration latency.
            /// </summary>
            public string IntegrationLatency { get; set; }
            
            /// <summary>
            /// Gets and sets the message direction.
            /// </summary>
            public string MessageDirection { get; set; }
            
            /// <summary>
            /// Gets and sets the request time.
            /// </summary>
            public string RequestTime { get; set; }
            
            /// <summary>
            /// Gets and sets the request time as an epoch.
            /// </summary>
            public long RequestTimeEpoch { get; set; }
            
            /// <summary>
            /// Gets and sets the status.
            /// </summary>
            public string Status { get; set; }

        }

        /// <summary>
        /// The RequestIdentity contains identity information for the request caller.
        /// </summary>
        public class RequestIdentity
        {

            /// <summary>
            /// The Cognito identity pool id.
            /// </summary>
            public string CognitoIdentityPoolId { get; set; }

            /// <summary>
            /// The account id of the caller.
            /// </summary>
            public string AccountId { get; set; }

            /// <summary>
            /// The cognito identity id.
            /// </summary>
            public string CognitoIdentityId { get; set; }

            /// <summary>
            /// The caller
            /// </summary>
            public string Caller { get; set; }

            /// <summary>
            /// The API Key
            /// </summary>
            public string ApiKey { get; set; }

            /// <summary>
            /// The API Key ID
            /// </summary>
            public string ApiKeyId { get; set; }
            
            /// <summary>
            /// The Access Key
            /// </summary>
            public string AccessKey { get; set; }

            /// <summary>
            /// The source IP of the request
            /// </summary>
            public string SourceIp { get; set; }

            /// <summary>
            /// The Cognito authentication type used for authentication
            /// </summary>
            public string CognitoAuthenticationType { get; set; }

            /// <summary>
            /// The Cognito authentication provider
            /// </summary>
            public string CognitoAuthenticationProvider { get; set; }

            /// <summary>
            /// The user arn
            /// </summary>
            public string UserArn { get; set; }

            /// <summary>
            /// The user agent
            /// </summary>
            public string UserAgent { get; set; }

            /// <summary>
            /// The user
            /// </summary>
            public string User { get; set; }


            /// <summary>
            /// Properties for a client certificate.
            /// </summary>
            public ProxyRequestClientCert ClientCert { get; set; }
        }

        /// <summary>
        /// Container for the properties of the client certificate.
        /// </summary>
        public class ProxyRequestClientCert
        {
            /// <summary>
            /// The PEM-encoded client certificate that the client presented during mutual TLS authentication. 
            /// Present when a client accesses an API by using a custom domain name that has mutual 
            /// TLS enabled. Present only in access logs if mutual TLS authentication fails.
            /// </summary>
            public string ClientCertPem { get; set; }

            /// <summary>
            /// The distinguished name of the subject of the certificate that a client presents. 
            /// Present when a client accesses an API by using a custom domain name that has 
            /// mutual TLS enabled. Present only in access logs if mutual TLS authentication fails.
            /// </summary>
            public string SubjectDN { get; set; }

            /// <summary>
            /// The distinguished name of the issuer of the certificate that a client presents. 
            /// Present when a client accesses an API by using a custom domain name that has 
            /// mutual TLS enabled. Present only in access logs if mutual TLS authentication fails.
            /// </summary>
            public string IssuerDN { get; set; }

            /// <summary>
            /// The serial number of the certificate. Present when a client accesses an API by 
            /// using a custom domain name that has mutual TLS enabled. 
            /// Present only in access logs if mutual TLS authentication fails.
            /// </summary>
            public string SerialNumber { get; set; }

            /// <summary>
            /// The rules for when the client cert is valid.
            /// </summary>
            public ClientCertValidity Validity { get; set; }
        }

        /// <summary>
        /// Container for the validation properties of a client cert.
        /// </summary>
        public class ClientCertValidity
        {
            /// <summary>
            /// The date before which the certificate is invalid. Present when a client accesses an API by using a custom domain name 
            /// that has mutual TLS enabled. Present only in access logs if mutual TLS authentication fails.
            /// </summary>
            public string NotBefore { get; set; }

            /// <summary>
            /// The date after which the certificate is invalid. Present when a client accesses an API by using a custom domain name that 
            /// has mutual TLS enabled. Present only in access logs if mutual TLS authentication fails.
            /// </summary>
            public string NotAfter { get; set; }
        }
    }
}
