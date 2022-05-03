using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Microsoft.AspNetCore.Http.Features.Authentication;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// Base class for ASP.NET Core Lambda functions that are getting request from API Gateway HTTP API V2 payload format.
    /// </summary>
    public abstract class APIGatewayHttpApiV2ProxyFunction : AbstractAspNetCoreFunction<APIGatewayHttpApiV2ProxyRequest, APIGatewayHttpApiV2ProxyResponse>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected APIGatewayHttpApiV2ProxyFunction()
            : base()
        {

        }

        /// <inheritdoc/>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected APIGatewayHttpApiV2ProxyFunction(StartupMode startupMode)
            : base(startupMode)
        {

        }

        /// <summary>
        /// Constructor used by Amazon.Lambda.AspNetCoreServer.Hosting to support ASP.NET Core projects using the Minimal API style.
        /// </summary>
        /// <param name="hostedServices"></param>
        protected APIGatewayHttpApiV2ProxyFunction(IServiceProvider hostedServices)
            : base(hostedServices)
        {
            _hostServices = hostedServices;
        }

        private protected override void InternalCustomResponseExceptionHandling(APIGatewayHttpApiV2ProxyResponse apiGatewayResponse, ILambdaContext lambdaContext, Exception ex)
        {
            apiGatewayResponse.SetHeaderValues("ErrorType", ex.GetType().Name, false);
        }

        /// <summary>
        /// Convert the JSON document received from API Gateway into the InvokeFeatures object.
        /// InvokeFeatures is then passed into IHttpApplication to create the ASP.NET Core request objects.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="apiGatewayRequest"></param>
        /// <param name="lambdaContext"></param>
        protected override void MarshallRequest(InvokeFeatures features, APIGatewayHttpApiV2ProxyRequest apiGatewayRequest, ILambdaContext lambdaContext)
        {
            {
                var authFeatures = (IHttpAuthenticationFeature)features;

                var authorizer = apiGatewayRequest?.RequestContext?.Authorizer;

                if (authorizer != null)
                {
                    // handling claims output from cognito user pool authorizer
                    if (authorizer.Jwt?.Claims != null && authorizer.Jwt.Claims.Count != 0)
                    {
                        var identity = new ClaimsIdentity(authorizer.Jwt.Claims.Select(
                            entry => new Claim(entry.Key, entry.Value.ToString())), "AuthorizerIdentity");

                        _logger.LogDebug(
                            $"Configuring HttpContext.User with {authorizer.Jwt.Claims.Count} claims coming from API Gateway's Request Context");
                        authFeatures.User = new ClaimsPrincipal(identity);
                    }
                    else
                    {
                        // handling claims output from custom lambda authorizer
                        var identity = new ClaimsIdentity(authorizer.Jwt?.Claims.Select(entry => new Claim(entry.Key, entry.Value)), "AuthorizerIdentity");

                        _logger.LogDebug(
                            $"Configuring HttpContext.User with {identity.Claims.Count()} claims coming from API Gateway's Request Context");
                        authFeatures.User = new ClaimsPrincipal(identity);
                    }
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallHttpAuthenticationFeature(authFeatures, apiGatewayRequest, lambdaContext);
            }
            {
                var httpInfo = apiGatewayRequest.RequestContext.Http;
                var requestFeatures = (IHttpRequestFeature)features;
                requestFeatures.Scheme = "https";
                requestFeatures.Method = httpInfo.Method;

                if (string.IsNullOrWhiteSpace(apiGatewayRequest.RequestContext?.DomainName))
                {
                    _logger.LogWarning($"Request does not contain domain name information but is derived from {nameof(APIGatewayProxyFunction)}.");
                }

                requestFeatures.Path = Utilities.DecodeResourcePath(httpInfo.Path);
                if (!requestFeatures.Path.StartsWith("/"))
                {
                    requestFeatures.Path = "/" + requestFeatures.Path;
                }


                // If there is a stage name in the resource path strip it out and set the stage name as the base path.
                // This is required so that ASP.NET Core will route request based on the resource path without the stage name.
                var stageName = apiGatewayRequest.RequestContext.Stage;
                if (!string.IsNullOrWhiteSpace(stageName))
                {
                    if (requestFeatures.Path.StartsWith($"/{stageName}"))
                    {
                        requestFeatures.Path = requestFeatures.Path.Substring(stageName.Length + 1);
                        requestFeatures.PathBase = $"/{stageName}";
                    }
                }

                requestFeatures.QueryString = Utilities.CreateQueryStringParametersFromHttpApiV2(apiGatewayRequest.RawQueryString);

                // API Gateway HTTP API V2 format supports multiple values for headers by comma separating the values.
                if (apiGatewayRequest.Headers != null)
                {
                    foreach(var kvp in apiGatewayRequest.Headers)
                    {
                        requestFeatures.Headers[kvp.Key] = new StringValues(kvp.Value?.Split(','));
                    }
                }

                if (!requestFeatures.Headers.ContainsKey("Host"))
                {
                    requestFeatures.Headers["Host"] = apiGatewayRequest.RequestContext.DomainName;
                }

                if (apiGatewayRequest.Cookies != null)
                {
                    // Add Cookies from the event
                    requestFeatures.Headers["Cookie"] = String.Join("; ", apiGatewayRequest.Cookies);
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

                if (!string.IsNullOrEmpty(apiGatewayRequest.RequestContext?.Http?.SourceIp) &&
                    IPAddress.TryParse(apiGatewayRequest.RequestContext.Http.SourceIp, out var remoteIpAddress))
                {
                    connectionFeatures.RemoteIpAddress = remoteIpAddress;
                }

                if (apiGatewayRequest?.Headers?.TryGetValue("X-Forwarded-Port", out var port) == true)
                {
                    connectionFeatures.RemotePort = int.Parse(port, CultureInfo.InvariantCulture);
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallConnectionFeature(connectionFeatures, apiGatewayRequest, lambdaContext);
            }

            {
                var tlsConnectionFeature = (ITlsConnectionFeature)features;
                var clientCertPem = apiGatewayRequest?.RequestContext?.Authentication?.ClientCert?.ClientCertPem;
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
        protected override APIGatewayHttpApiV2ProxyResponse MarshallResponse(IHttpResponseFeature responseFeatures, ILambdaContext lambdaContext, int statusCodeIfNotSet = 200)
        {
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = responseFeatures.StatusCode != 0 ? responseFeatures.StatusCode : statusCodeIfNotSet
            };

            string contentType = null;
            string contentEncoding = null;
            if (responseFeatures.Headers != null)
            {
                response.Headers = new Dictionary<string, string>();
                foreach (var kvp in responseFeatures.Headers)
                {
                    if (kvp.Key.Equals(HeaderNames.SetCookie, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // Cookies must be passed through the proxy response property and not as a 
                        // header to be able to pass back multiple cookies in a single request.
                        response.Cookies = kvp.Value.ToArray();
                        continue;
                    }

                    response.SetHeaderValues(kvp.Key, kvp.Value.ToArray(), false);

                    // Remember the Content-Type for possible later use
                    if (kvp.Key.Equals("Content-Type", StringComparison.CurrentCultureIgnoreCase) && response.Headers[kvp.Key]?.Length > 0)
                    {
                        contentType = response.Headers[kvp.Key];
                    }
                    else if (kvp.Key.Equals("Content-Encoding", StringComparison.CurrentCultureIgnoreCase) && response.Headers[kvp.Key]?.Length > 0)
                    {
                        contentEncoding = response.Headers[kvp.Key];
                    }
                }
            }

            if (contentType == null)
            {
                response.Headers["Content-Type"] = null;
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
