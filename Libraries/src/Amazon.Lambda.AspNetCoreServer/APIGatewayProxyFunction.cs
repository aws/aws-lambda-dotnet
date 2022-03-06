using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features.Authentication;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// APIGatewayProxyFunction is the base class for Lambda functions hosting the ASP.NET Core framework and exposed to the web via API Gateway.
    /// 
    /// The derived class implements the Init method similar to Main function in the ASP.NET Core. The function handler for the Lambda function will point
    /// to this base class FunctionHandlerAsync method.
    /// </summary>
    public abstract class APIGatewayProxyFunction : AbstractAspNetCoreFunction<APIGatewayProxyRequest, APIGatewayProxyResponse>
    {
        /// <summary>
        /// Key to access the ILambdaContext object from the HttpContext.Items collection.
        /// </summary>
        [Obsolete("References should be updated to Amazon.Lambda.AspNetCoreServer.AbstractAspNetCoreFunction.LAMBDA_CONTEXT")]
        public new const string LAMBDA_CONTEXT = AbstractAspNetCoreFunction.LAMBDA_CONTEXT;

        /// <summary>
        /// Key to access the APIGatewayProxyRequest object from the HttpContext.Items collection.
        /// </summary>
        [Obsolete("References should be updated to Amazon.Lambda.AspNetCoreServer.AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT")]
        public const string APIGATEWAY_REQUEST = AbstractAspNetCoreFunction.LAMBDA_REQUEST_OBJECT;



        /// <summary>
        /// The modes for when the ASP.NET Core framework will be initialized.
        /// </summary>
        [Obsolete("References should be updated to Amazon.Lambda.AspNetCoreServer.StartupMode")]
        public enum AspNetCoreStartupMode
        {
            /// <summary>
            /// Initialize during the construction of APIGatewayProxyFunction
            /// </summary>
            Constructor = StartupMode.Constructor,

            /// <summary>
            /// Initialize during the first incoming request
            /// </summary>
            FirstRequest = StartupMode.FirstRequest
        }

        /// <summary>
        /// Default Constructor. The ASP.NET Core Framework will be initialized as part of the construction.
        /// </summary>
        protected APIGatewayProxyFunction()
            : base()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        [Obsolete("Calls to the constructor should be replaced with the constructor that takes a Amazon.Lambda.AspNetCoreServer.StartupMode as the parameter.")]
        protected APIGatewayProxyFunction(AspNetCoreStartupMode startupMode)
            : base((StartupMode)startupMode)
        {

        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected APIGatewayProxyFunction(StartupMode startupMode)
            : base(startupMode)
        {

        }


        /// <summary>
        /// Constructor used by Amazon.Lambda.AspNetCoreServer.Hosting to support ASP.NET Core projects using the Minimal API style.
        /// </summary>
        /// <param name="hostedServices"></param>
        protected APIGatewayProxyFunction(IServiceProvider hostedServices)
            : base(hostedServices)
        {
            _hostServices = hostedServices;
        }

        private protected override void InternalCustomResponseExceptionHandling(APIGatewayProxyResponse apiGatewayResponse, ILambdaContext lambdaContext, Exception ex)
        {
            apiGatewayResponse.MultiValueHeaders["ErrorType"] = new List<string> { ex.GetType().Name };
        }


        /// <summary>
        /// Convert the JSON document received from API Gateway into the InvokeFeatures object.
        /// InvokeFeatures is then passed into IHttpApplication to create the ASP.NET Core request objects.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="apiGatewayRequest"></param>
        /// <param name="lambdaContext"></param>
        protected override void MarshallRequest(InvokeFeatures features, APIGatewayProxyRequest apiGatewayRequest, ILambdaContext lambdaContext)
        {
            {
                var authFeatures = (IHttpAuthenticationFeature)features;

                var authorizer = apiGatewayRequest?.RequestContext?.Authorizer;

                if (authorizer != null)
                {
                    // handling claims output from cognito user pool authorizer
                    if (authorizer.Claims != null && authorizer.Claims.Count != 0)
                    {
                        var identity = new ClaimsIdentity(authorizer.Claims.Select(
                            entry => new Claim(entry.Key, entry.Value.ToString())), "AuthorizerIdentity");

                        _logger.LogDebug(
                            $"Configuring HttpContext.User with {authorizer.Claims.Count} claims coming from API Gateway's Request Context");
                        authFeatures.User = new ClaimsPrincipal(identity);
                    }
                    else
                    {
                        // handling claims output from custom lambda authorizer
                        var identity = new ClaimsIdentity(
                            authorizer.Where(x => x.Value != null && !string.Equals(x.Key, "claims", StringComparison.OrdinalIgnoreCase))
                                .Select(entry => new Claim(entry.Key, entry.Value.ToString())), "AuthorizerIdentity");

                        _logger.LogDebug(
                            $"Configuring HttpContext.User with {authorizer.Count} claims coming from API Gateway's Request Context");
                        authFeatures.User = new ClaimsPrincipal(identity);
                    }
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallHttpAuthenticationFeature(authFeatures, apiGatewayRequest, lambdaContext);
            }
            {
                var requestFeatures = (IHttpRequestFeature)features;
                requestFeatures.Scheme = "https";
                requestFeatures.Method = apiGatewayRequest.HttpMethod;

                string path = null;
                if (apiGatewayRequest.PathParameters != null && apiGatewayRequest.PathParameters.TryGetValue("proxy", out var proxy) &&
                    !string.IsNullOrEmpty(apiGatewayRequest.Resource))
                {
                    var proxyPath = proxy;
                    path = apiGatewayRequest.Resource.Replace("{proxy+}", proxyPath);
                }

                if (string.IsNullOrEmpty(path))
                {
                    path = apiGatewayRequest.Path;
                }

                if (!path.StartsWith("/"))
                {
                    path = "/" + path;
                }

                requestFeatures.Path = path;

                requestFeatures.PathBase = string.Empty;
                if (!string.IsNullOrEmpty(apiGatewayRequest?.RequestContext?.Path))
                {
                    // This is to cover the case where the request coming in is https://myapigatewayid.execute-api.us-west-2.amazonaws.com/Prod where
                    // Prod is the stage name and there is no ending '/'. Path will be set to '/' so to make sure we detect the correct base path
                    // append '/' on the end to make the later EndsWith and substring work correctly.
                    var requestContextPath = apiGatewayRequest.RequestContext.Path;
                    if (path.EndsWith("/") && !requestContextPath.EndsWith("/"))
                    {
                        requestContextPath += "/";
                    }
                    else if (!path.EndsWith("/") && requestContextPath.EndsWith("/"))
                    {
                        // Handle a trailing slash in the request path: e.g. https://myapigatewayid.execute-api.us-west-2.amazonaws.com/Prod/foo/
                        requestFeatures.Path = path += "/";
                    }

                    if (requestContextPath.EndsWith(path))
                    {
                        requestFeatures.PathBase = requestContextPath.Substring(0,
                            requestContextPath.Length - requestFeatures.Path.Length);
                    }
                }

                requestFeatures.Path = Utilities.DecodeResourcePath(requestFeatures.Path);

                requestFeatures.QueryString = Utilities.CreateQueryStringParameters(
                    apiGatewayRequest.QueryStringParameters, apiGatewayRequest.MultiValueQueryStringParameters, true);

                Utilities.SetHeadersCollection(requestFeatures.Headers, apiGatewayRequest.Headers, apiGatewayRequest.MultiValueHeaders);

                if (!requestFeatures.Headers.ContainsKey("Host"))
                {
                    var apiId = apiGatewayRequest?.RequestContext?.ApiId ?? "";
                    var stage = apiGatewayRequest?.RequestContext?.Stage ?? "";

                    requestFeatures.Headers["Host"] = $"apigateway-{apiId}-{stage}";
                }


                if (!string.IsNullOrEmpty(apiGatewayRequest.Body))
                {
                    requestFeatures.Body = Utilities.ConvertLambdaRequestBodyToAspNetCoreBody(apiGatewayRequest.Body, apiGatewayRequest.IsBase64Encoded);
                }

                // Make sure the content-length header is set if header was not present.
                const string contentLengthHeaderName = "Content-Length";
                if (!requestFeatures.Headers.ContainsKey(contentLengthHeaderName))
                {
                    requestFeatures.Headers[contentLengthHeaderName] = requestFeatures.Body == null ? "0" : requestFeatures.Body.Length.ToString(CultureInfo.InvariantCulture);
                }


                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallRequestFeature(requestFeatures, apiGatewayRequest, lambdaContext);
            }


            {
                // set up connection features
                var connectionFeatures = (IHttpConnectionFeature)features;

                if (!string.IsNullOrEmpty(apiGatewayRequest?.RequestContext?.Identity?.SourceIp) &&
                    IPAddress.TryParse(apiGatewayRequest.RequestContext.Identity.SourceIp, out var remoteIpAddress))
                {
                    connectionFeatures.RemoteIpAddress = remoteIpAddress;
                }

                if (apiGatewayRequest?.Headers?.TryGetValue("X-Forwarded-Port", out var forwardedPort) == true)
                {
                    connectionFeatures.RemotePort = int.Parse(forwardedPort, CultureInfo.InvariantCulture);
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallConnectionFeature(connectionFeatures, apiGatewayRequest, lambdaContext);
            }

            {
                var tlsConnectionFeature = (ITlsConnectionFeature) features;
                var clientCertPem = apiGatewayRequest?.RequestContext?.Identity?.ClientCert?.ClientCertPem;
                if (clientCertPem != null)
                {
                    tlsConnectionFeature.ClientCertificate = Utilities.GetX509Certificate2FromPem(clientCertPem);
                }
                PostMarshallTlsConnectionFeature(tlsConnectionFeature, apiGatewayRequest, lambdaContext);
            }
        }

        /// <summary>
        /// Convert the response coming from ASP.NET Core into APIGatewayProxyResponse which is
        /// serialized into the JSON object that API Gateway expects.
        /// </summary>
        /// <param name="responseFeatures"></param>
        /// <param name="statusCodeIfNotSet">Sometimes the ASP.NET server doesn't set the status code correctly when successful, so this parameter will be used when the value is 0.</param>
        /// <param name="lambdaContext"></param>
        /// <returns><see cref="APIGatewayProxyResponse"/></returns>
        protected override APIGatewayProxyResponse MarshallResponse(IHttpResponseFeature responseFeatures, ILambdaContext lambdaContext, int statusCodeIfNotSet = 200)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = responseFeatures.StatusCode != 0 ? responseFeatures.StatusCode : statusCodeIfNotSet
            };

            string contentType = null;
            string contentEncoding = null;
            if (responseFeatures.Headers != null)
            {
                response.MultiValueHeaders = new Dictionary<string, IList<string>>();

                response.Headers = new Dictionary<string, string>();
                foreach (var kvp in responseFeatures.Headers)
                {
                    response.MultiValueHeaders[kvp.Key] = kvp.Value.ToList();

                    // Remember the Content-Type for possible later use
                    if (kvp.Key.Equals("Content-Type", StringComparison.CurrentCultureIgnoreCase) && response.MultiValueHeaders[kvp.Key].Count > 0)
                    {
                        contentType = response.MultiValueHeaders[kvp.Key][0];
                    }
                    else if (kvp.Key.Equals("Content-Encoding", StringComparison.CurrentCultureIgnoreCase) && response.MultiValueHeaders[kvp.Key].Count > 0)
                    {
                        contentEncoding = response.MultiValueHeaders[kvp.Key][0];
                    }
                }
            }

            if (contentType == null)
            {
                response.MultiValueHeaders["Content-Type"] = new List<string>() { null };
            }

            if (responseFeatures.Body != null)
            {
                // Figure out how we should treat the response content, check encoding first to see if body is compressed, then check content type
                var rcEncoding = GetResponseContentEncodingForContentEncoding(contentEncoding);
                if (rcEncoding != ResponseContentEncoding.Base64)
                {
                    rcEncoding = GetResponseContentEncodingForContentType(contentType);
                }

                (response.Body, response.IsBase64Encoded) = Utilities.ConvertAspNetCoreBodyToLambdaBody(responseFeatures.Body, rcEncoding);

            }

            PostMarshallResponseFeature(responseFeatures, response, lambdaContext);

            _logger.LogDebug($"Response Base 64 Encoded: {response.IsBase64Encoded}");

            return response;
        }
    }
}
