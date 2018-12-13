using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

using Amazon.Lambda.Core;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using System.Net;
using Microsoft.Extensions.Primitives;
using Microsoft.AspNetCore.Hosting.Internal;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// ApplicationLoadBalancerFunction is the base class for Lambda functions hosting the ASP.NET Core framework and exposed to the web via ELB's Application Load Balancer.
    /// 
    /// The derived class implements the Init method similar to Main function in the ASP.NET Core. The function handler for the Lambda function will point
    /// to this base class FunctionHandlerAsync method.
    /// </summary>
    public abstract class ApplicationLoadBalancerFunction : AbstractAspNetCoreFunction<ApplicationLoadBalancerRequest, ApplicationLoadBalancerResponse>
    {
        private bool _multiHeaderValuesEnabled = true;

        /// <inheritdoc/>
        protected ApplicationLoadBalancerFunction()
            : base()
        {

        }

        /// <inheritdoc/>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected ApplicationLoadBalancerFunction(StartupMode startupMode)
            : base(startupMode)
        {

        }


        /// <inheritdoc/>
        protected override void MarshallRequest(InvokeFeatures features, ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
        {
            // Request coming from Application Load Balancer will always send the headers X-Amzn-Trace-Id, X-Forwarded-For, X-Forwarded-Port, and X-Forwarded-Proto.
            // So this will only happen when writing tests with incomplete sample requests.
            if (lambdaRequest.Headers == null && lambdaRequest.MultiValueHeaders == null)
            {
                throw new Exception("Unable to determine header mode, single or multi value, because both Headers and MultiValueHeaders are null.");
            }

            // Look to see if the request is using mutli value headers or not. This is important when
            // marshalling the response to know whether to fill in the the Headers or MultiValueHeaders collection.
            // Since a Lambda function compute environment is only one processing one event at a time it is safe to store
            // this as a member variable.
            this._multiHeaderValuesEnabled = lambdaRequest.MultiValueHeaders != null;

            {
                var requestFeatures = (IHttpRequestFeature)features;
                requestFeatures.Scheme = GetSingleHeaderValue(lambdaRequest, "x-forwarded-proto");
                requestFeatures.Method = lambdaRequest.HttpMethod;
                requestFeatures.Path = lambdaRequest.Path;


                requestFeatures.QueryString = string.Empty;
                if (this._multiHeaderValuesEnabled)
                {
                    var queryStringParameters = lambdaRequest.MultiValueQueryStringParameters;
                    if (queryStringParameters != null)
                    {
                        StringBuilder sb = new StringBuilder("?");
                        foreach (var kvp in queryStringParameters)
                        {
                            if (sb.Length > 1)
                            {
                                sb.Append("&");
                            }
                            foreach(var value in kvp.Value)
                            {
                                sb.Append(Utilities.CreateQueryStringParameter(kvp.Key, value));
                            }
                        }
                        requestFeatures.QueryString = sb.ToString();
                    }
                }
                else
                {
                    var queryStringParameters = lambdaRequest.QueryStringParameters;
                    if (queryStringParameters != null)
                    {
                        StringBuilder sb = new StringBuilder("?");
                        foreach (var kvp in queryStringParameters)
                        {
                            if (sb.Length > 1)
                            {
                                sb.Append("&");
                            }
                            sb.Append(Utilities.CreateQueryStringParameter(kvp.Key, kvp.Value.ToString()));
                        }
                        requestFeatures.QueryString = sb.ToString();
                    }
                }

                if(this._multiHeaderValuesEnabled)
                {
                    foreach(var kvp in lambdaRequest.MultiValueHeaders)
                    {
                        requestFeatures.Headers[kvp.Key] = new StringValues(kvp.Value.ToArray());
                    }
                }
                else
                {
                    foreach (var kvp in lambdaRequest.Headers)
                    {
                        requestFeatures.Headers[kvp.Key] = new StringValues(kvp.Value);
                    }
                }

                if (!string.IsNullOrEmpty(lambdaRequest.Body))
                {
                    requestFeatures.Body = Utilities.ConvertLambdaRequestBodyToAspNetCoreBody(lambdaRequest.Body, lambdaRequest.IsBase64Encoded);
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallRequestFeature(requestFeatures, lambdaRequest, lambdaContext);
            }


            {
                // set up connection features
                var connectionFeatures = (IHttpConnectionFeature)features;

                var remoteIpAddressStr = GetSingleHeaderValue(lambdaRequest, "x-forwarded-for");
                if (!string.IsNullOrEmpty(remoteIpAddressStr) &&
                    IPAddress.TryParse(remoteIpAddressStr, out var remoteIpAddress))
                {
                    connectionFeatures.RemoteIpAddress = remoteIpAddress;
                }

                var remotePort = GetSingleHeaderValue(lambdaRequest, "x-forwarded-port");
                if (!string.IsNullOrEmpty(remotePort))
                {
                    connectionFeatures.RemotePort = int.Parse(remotePort);
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallConnectionFeature(connectionFeatures, lambdaRequest, lambdaContext);
            }
        }

        /// <inheritdoc/>
        protected override ApplicationLoadBalancerResponse MarshallResponse(IHttpResponseFeature responseFeatures, ILambdaContext lambdaContext, int statusCodeIfNotSet = 200)
        {
            var response = new ApplicationLoadBalancerResponse
            {
                StatusCode = responseFeatures.StatusCode != 0 ? responseFeatures.StatusCode : statusCodeIfNotSet                
            };

            response.StatusDescription = $"{response.StatusCode} {((System.Net.HttpStatusCode)response.StatusCode).ToString()}";


            string contentType = null;
            if (responseFeatures.Headers != null)
            {
                if(this._multiHeaderValuesEnabled)
                    response.MultiValueHeaders = new Dictionary<string, IList<string>>();
                else
                    response.Headers = new Dictionary<string, string>();

                foreach (var kvp in responseFeatures.Headers)
                {
                    if(this._multiHeaderValuesEnabled)
                    {
                        response.MultiValueHeaders[kvp.Key] = kvp.Value.ToList();
                    }
                    else
                    {
                        response.Headers[kvp.Key] = kvp.Value[0];
                    }

                    // Remember the Content-Type for possible later use
                    if (kvp.Key.Equals("Content-Type", StringComparison.CurrentCultureIgnoreCase))
                    {
                        contentType = kvp.Value[0];
                    }
                }
            }

            if (contentType == null)
            {
                response.Headers["Content-Type"] = null;
            }

            if (responseFeatures.Body != null)
            {
                // Figure out how we should treat the response content
                var rcEncoding = GetResponseContentEncodingForContentType(contentType);

                (response.Body, response.IsBase64Encoded) = Utilities.ConvertAspNetCoreBodyToLambdaBody(responseFeatures.Body, rcEncoding);
            }

            PostMarshallResponseFeature(responseFeatures, response, lambdaContext);

            _logger.LogDebug($"Response Base 64 Encoded: {response.IsBase64Encoded}");

            return response;
        }

        private protected override void InternalCustomResponseExceptionHandling(HostingApplication.Context context, ApplicationLoadBalancerResponse lambdaResponse, ILambdaContext lambdaContext, Exception ex)
        {
            var errorName = ex.GetType().Name;

            if(this._multiHeaderValuesEnabled)
            {
                lambdaResponse.MultiValueHeaders.Add(new KeyValuePair<string, IList<string>>("ErrorType", new List<string> { errorName }));
            }
            else
            {
                lambdaResponse.Headers.Add(new KeyValuePair<string, string>("ErrorType", errorName));
            }            
        }

        private string GetSingleHeaderValue(ApplicationLoadBalancerRequest request, string headerName)
        {
            if(this._multiHeaderValuesEnabled)
            {
                var kvp = request.MultiValueHeaders.FirstOrDefault(x => string.Equals(x.Key, headerName, StringComparison.OrdinalIgnoreCase));
                if (!kvp.Equals(default(KeyValuePair<string, IList<string>>)))
                {
                    return kvp.Value.First();
                }
            }
            else
            {
                var kvp = request.Headers.FirstOrDefault(x => string.Equals(x.Key, headerName, StringComparison.OrdinalIgnoreCase));
                if (!kvp.Equals(default(KeyValuePair<string, string>)))
                {
                    return kvp.Value;
                }
            }

            return null;
        }
    }
}
