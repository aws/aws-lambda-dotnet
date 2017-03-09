﻿using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Linq;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// ApiGatewayFunction is the base class that is implemented in a ASP.NET Core Web API. The derived class implements
    /// the Init method similar to Main function in the ASP.NET Core. The function handler for the Lambda function will point
    /// to this base class FunctionHandlerAsync method.
    /// </summary>
    public abstract class APIGatewayProxyFunction
    {
        private readonly IWebHost _host;
        private readonly APIGatewayServer _server;

        // Manage the serialization so the raw requests and responses can be logged.
        ILambdaSerializer _serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();

        /// <summary>
        /// If true the request JSON coming from API Gateway will be logged. This is used to help debugging and not meant to be enabled for production.
        /// </summary>
        public bool EnableRequestLogging { get; set; }
        /// <summary>
        /// If true the response JSON coming sent to API Gateway will be logged. This is used to help debugging and not meant to be enabled for production.
        /// </summary>
        public bool EnableResponseLogging { get; set; }

        /// <summary>
        /// If true then all response content will be Base64-encoded before being returned.
        /// </summary>
        public bool EnableBinaryResponseContent { get; set; }

        /// <summary>
        /// An optional set of Content-Types that will trigger encoding the response content in Base64.
        /// </summary>
        public IEnumerable<string> BinaryResponseContentTypes { get; set; }

        /// <summary>
        /// Default constructor that AWS Lambda will invoke.
        /// </summary>
        protected APIGatewayProxyFunction()
        {
            var builder = new WebHostBuilder();
            Init(builder);

            // Add the API Gateway services in case the override Init method didn't add it. UseApiGateway will
            // not add anything if API Gateway has already been added.
            builder.UseApiGateway();

            _host = builder.Build();
            _host.Start();

            _server = _host.Services.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.IServer)) as APIGatewayServer;
        }

        /// <summary>
        /// Method to initialize the web builder before starting the web host. In a typical Web API this is similar to the main function. 
        /// </summary>
        /// <example>
        /// <code>
        /// protected override void Init(IWebHostBuilder builder)
        /// {
        ///     builder
        ///         .UseApiGateway()
        ///         .UseContentRoot(Directory.GetCurrentDirectory())
        ///         .UseStartup&lt;Startup&gt;();
        /// }
        /// </code>
        /// </example>
        /// <param name="builder"></param>
        protected abstract void Init(IWebHostBuilder builder);

        /// <summary>
        /// This method is what the Lambda function handler points to.
        /// </summary>
        /// <param name="requestStream">Takes in a Stream instead of APIGatewayProxyRequest to get access to the raw event for logging purposes.</param>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        public virtual async Task<Stream> FunctionHandlerAsync(Stream requestStream, ILambdaContext lambdaContext)
        {
            if (this.EnableRequestLogging)
            {
                StreamReader reader = new StreamReader(requestStream);
                string json = reader.ReadToEnd();
                lambdaContext.Logger.LogLine(json);
                requestStream.Position = 0;
            }

            var request = this._serializer.Deserialize<APIGatewayProxyRequest>(requestStream);

            lambdaContext.Logger.Log($"Incoming {request.HttpMethod} requests to {request.Path}");
            InvokeFeatures features = new InvokeFeatures();
            MarshallRequest(features, request);
            var context = this.CreateContext(features);

            var response = await this.ProcessRequest(lambdaContext, context, features);

            var responseStream = new MemoryStream();
            this._serializer.Serialize<APIGatewayProxyResponse>(response, responseStream);
            responseStream.Position = 0;

            if (this.EnableResponseLogging)
            {
                StreamReader reader = new StreamReader(responseStream);
                string json = reader.ReadToEnd();
                lambdaContext.Logger.LogLine(json);
                responseStream.Position = 0;
            }


            return responseStream;
        }

        /// <summary>
        /// Creates a <see cref="HostingApplication.Context"/> object using the <see cref="APIGatewayServer"/> field in the class.
        /// </summary>
        /// <param name="features"><see cref="IFeatureCollection"/> implementation.</param>
        protected HostingApplication.Context CreateContext(IFeatureCollection features)
        {
            return _server.Application.CreateContext(features);
        }

        /// <summary>
        /// Processes the current request.
        /// </summary>
        /// <param name="lambdaContext"><see cref="ILambdaContext"/> implementation.</param>
        /// <param name="context">The hosting application request context object.</param>
        /// <param name="features">An <see cref="InvokeFeatures"/> instance.</param>
        /// <param name="rethrowUnhandledError">
        /// If specified, an unhandled exception will be rethrown for custom error handling.
        /// Ensure that the error handling code calls 'this.MarshallResponse(features, 500);' after handling the error to return a <see cref="APIGatewayProxyResponse"/> to the user.
        /// </param>
        protected async Task<APIGatewayProxyResponse> ProcessRequest(ILambdaContext lambdaContext, HostingApplication.Context context, InvokeFeatures features, bool rethrowUnhandledError = false)
        {
            var defaultStatusCode = 200;
            Exception ex = null;
            try
            {
                await this._server.Application.ProcessRequestAsync(context);
            }
            catch (AggregateException agex)
            {
                ex = agex;
                lambdaContext.Logger.Log($"Caught AggregateException: '{agex}'");
                var sb = new StringBuilder();
                foreach (var newEx in agex.InnerExceptions)
                {
                    sb.AppendLine(this.ErrorReport(newEx));
                }

                lambdaContext.Logger.Log(sb.ToString());
                defaultStatusCode = 500;
            }
            catch (ReflectionTypeLoadException rex)
            {
                ex = rex;
                lambdaContext.Logger.Log($"Caught ReflectionTypeLoadException: '{rex}'");
                var sb = new StringBuilder();
                foreach (var loaderException in rex.LoaderExceptions)
                {
                    var fileNotFoundException = loaderException as FileNotFoundException;
                    if (fileNotFoundException != null && !string.IsNullOrEmpty(fileNotFoundException.FileName))
                    {
                        sb.AppendLine($"Missing file: {fileNotFoundException.FileName}");
                    }
                    else
                    {
                        sb.AppendLine(this.ErrorReport(loaderException));
                    }
                }

                lambdaContext.Logger.Log(sb.ToString());
                defaultStatusCode = 500;
            }
            catch (Exception e)
            {
                ex = e;
                if (rethrowUnhandledError) throw;
                lambdaContext.Logger.Log($"Unknown error responding to request: {this.ErrorReport(e)}");
                defaultStatusCode = 500;
            }
            finally
            {
                this._server.Application.DisposeContext(context, ex);
            }

            var response = this.MarshallResponse(features, defaultStatusCode);

            if (ex != null)
                response.Headers.Add(new KeyValuePair<string, string>("ErrorType", ex.GetType().Name));

            return response;
        }

        /// <summary>
        /// Formats an Exception into a string, including all inner exceptions.
        /// </summary>
        /// <param name="e"><see cref="Exception"/> instance.</param>
        protected string ErrorReport(Exception e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{e.GetType().Name}:\n{e}");

            Exception inner = e;
            while (inner != null)
            {
                // Append the messages to the StringBuilder.
                sb.AppendLine($"{inner.GetType().Name}:\n{inner}");
                inner = inner.InnerException;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert the JSON document received from API Gateway in the ASP.NET Core IHttpRequestFeature object.
        /// IHttpRequestFeature is then passed into IHttpApplication to create the ASP.NET Core request objects.
        /// </summary>
        /// <param name="requestFeatures"></param>
        /// <param name="apiGatewayRequest"></param>
        protected void MarshallRequest(IHttpRequestFeature requestFeatures, APIGatewayProxyRequest apiGatewayRequest)
        {
            requestFeatures.Scheme = "https";
            requestFeatures.Path = apiGatewayRequest.Path;
            requestFeatures.Method = apiGatewayRequest.HttpMethod;

            // API Gateway delivers the query string in a dictionary but must be reconstructed into the full query string
            // before passing into ASP.NET Core framework.
            var queryStringParameters = apiGatewayRequest.QueryStringParameters;
            if (queryStringParameters != null)
            {
                StringBuilder sb = new StringBuilder("?");
                var encoder = UrlEncoder.Default;
                foreach (var kvp in queryStringParameters)
                {
                    if (sb.Length > 1)
                    {
                        sb.Append("&");
                    }
                    sb.Append($"{encoder.Encode(kvp.Key)}={encoder.Encode(kvp.Value.ToString())}");
                }
                requestFeatures.QueryString = sb.ToString();
            }

            var headers = apiGatewayRequest.Headers;
            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    requestFeatures.Headers[kvp.Key] = kvp.Value?.ToString();
                }
            }

            if (!requestFeatures.Headers.ContainsKey("Host"))
            {
                var apiId = apiGatewayRequest.RequestContext?.ApiId ?? "";
                var stage = apiGatewayRequest.RequestContext?.Stage ?? "";

                requestFeatures.Headers["Host"] = $"apigateway-{apiId}-{stage}";
            }

            if (!string.IsNullOrEmpty(apiGatewayRequest.Body))
            {
                Byte[] binaryBody;
                if (apiGatewayRequest.IsBase64Encoded)
                {
                    binaryBody = Convert.FromBase64String(apiGatewayRequest.Body);
                }
                else
                {
                    binaryBody = UTF8Encoding.UTF8.GetBytes(apiGatewayRequest.Body);
                }
                requestFeatures.Body = new MemoryStream(binaryBody);
            }
        }

        /// <summary>
        /// Convert the response coming from ASP.NET Core into APIGatewayProxyResponse which is
        /// serialized into the JSON object that API Gateway expects.
        /// </summary>
        /// <param name="responseFeatures"></param>
        /// <param name="statusCodeIfNotSet">Sometimes the ASP.NET server doesn't set the status code correctly when successful, so this parameter will be used when the value is 0.</param>
        /// <returns><see cref="APIGatewayProxyResponse"/></returns>
        protected APIGatewayProxyResponse MarshallResponse(IHttpResponseFeature responseFeatures, int statusCodeIfNotSet = 200)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = responseFeatures.StatusCode != 0 ? responseFeatures.StatusCode : statusCodeIfNotSet
            };

            string contentType = null;
            if (responseFeatures.Headers != null)
            {
                response.Headers = new Dictionary<string, string>();
                foreach (var kvp in responseFeatures.Headers)
                {
                    if (kvp.Value.Count == 1)
                    {
                        response.Headers[kvp.Key] = kvp.Value[0];
                    }
                    else
                    {
                        response.Headers[kvp.Key] = string.Join(",", kvp.Value);
                    }

                    // Remember the Content-Type for possible later use
                    if (kvp.Key.Equals("Content-Type", StringComparison.CurrentCultureIgnoreCase))
                        contentType = response.Headers[kvp.Key];
                }
            }

            if (responseFeatures.Body != null)
            {
                // Figure out if we need to Base64-encode the literal response content
                var b64Encode = EnableBinaryResponseContent;
                if (!b64Encode && BinaryResponseContentTypes != null && contentType != null)
                {
                    b64Encode = BinaryResponseContentTypes.Contains(contentType);
                }
                
                if (b64Encode)
                {
                    // We want to read the response content "raw" and then Base64 encode it
                    byte[] bodyBytes;
                    if (responseFeatures.Body is MemoryStream)
                    {
                        bodyBytes = ((MemoryStream)responseFeatures.Body).ToArray();
                    }
                    else
                    {
                        using (var ms = new MemoryStream())
                        {
                            responseFeatures.Body.CopyTo(ms);
                            bodyBytes = ms.ToArray();
                        }
                    }
                    response.Body = Convert.ToBase64String(bodyBytes);
                    response.IsBase64Encoded = true;
                }
                else if (responseFeatures.Body is MemoryStream)
                {
                    response.Body = UTF8Encoding.UTF8.GetString(((MemoryStream)responseFeatures.Body).ToArray());
                }
                else
                {
                    responseFeatures.Body.Position = 0;
                    using (StreamReader reader = new StreamReader(responseFeatures.Body, Encoding.UTF8))
                    {
                        response.Body = reader.ReadToEnd();
                    }
                }
            }

            return response;
        }
    }
}
