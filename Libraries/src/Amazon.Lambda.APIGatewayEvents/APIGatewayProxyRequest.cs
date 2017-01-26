﻿namespace Amazon.Lambda.APIGatewayEvents
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
        /// </summary>
        public string Resource { get; set; }

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
        /// </summary>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The query string parameters that were part of the request
        /// </summary>
        public IDictionary<string, string> QueryStringParameters { get; set; }

        /// <summary>
        /// The path parameters that were part of the request
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
        /// True of the body of the request is base 64 encoded.
        /// </summary>
        public bool IsBase64Encoded { get; set; }

        /// <summary>
        /// The ProxyRequestContext contains the information to identify the AWS account and resources invoking the 
        /// Lambda function. It also includes Cognito identity information for the caller.
        /// </summary>
        public class ProxyRequestContext
        {
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
            /// </summary>
            public string ResourcePath { get; set; }

            /// <summary>
            /// The HTTP method used
            /// </summary>
            public string HttpMethod { get; set; }

            /// <summary>
            /// The API Gateway rest API Id.
            /// </summary>
            public string ApiId { get; set; }

            /// <summary>
            /// The APIGatewayCustomAuthorizerContext containing the custom properties set by a custom authorizer.
            /// </summary>
            public APIGatewayCustomAuthorizerContext Authorizer { get; set; }
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
        }
    }
}
