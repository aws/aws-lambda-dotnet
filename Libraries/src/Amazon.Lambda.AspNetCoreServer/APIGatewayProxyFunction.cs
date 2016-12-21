using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.Encodings.Web;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;

using Newtonsoft.Json;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// ApiGatewayFunction is the base class that is implemented in a ASP.NET Core Web API. The derived class implements
    /// the Init method similar to Main function in the ASP.NET Core. The function handler for the Lambda function will point
    /// to this base class FunctionHandlerAsync method.
    /// </summary>
    public abstract class APIGatewayProxyFunction
    {
        IWebHost _host;
        APIGatewayServer _server;

        /// <summary>
        /// Default constructor that AWS Lambda will invoke.
        /// </summary>
        public APIGatewayProxyFunction()
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
        /// <param name="request"></param>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        [LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public virtual async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            lambdaContext?.Logger.Log($"Incoming {request.HttpMethod} requests to {request.Path}");

            InvokeFeatures features = new InvokeFeatures();
            MarshallRequest(features, request);

            var context = _server.Application.CreateContext(features);
            try
            {
                await this._server.Application.ProcessRequestAsync(context);
                this._server.Application.DisposeContext(context, null);
            }
            catch (Exception e)
            {
                lambdaContext?.Logger.Log($"Unknown error responding to request: {ErrorReport(e)}");
                this._server.Application.DisposeContext(context, e);
            }

            var response = MarshallResponse(features);

            // ASP.NET Core Web API does not always set the status code if the request was
            // successful
            if (response.StatusCode == 0)
                response.StatusCode = 200;

            return response;
        }

        private string ErrorReport(Exception e)
        {
            StringBuilder sb = new StringBuilder();

            Exception inner = e;
            while(inner != null)
            {
                Console.WriteLine(inner.Message);
                Console.WriteLine(inner.StackTrace);

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
        private void MarshallRequest(IHttpRequestFeature requestFeatures, APIGatewayProxyRequest apiGatewayRequest)
        {
            requestFeatures.Scheme = "https";
            requestFeatures.Path = apiGatewayRequest.Path;
            requestFeatures.Method = apiGatewayRequest.HttpMethod;

            // API Gateway delivers the query string in a dictionary but must be reconstructed into the full query string
            // before passing into ASP.NET Core framework.
            var queryStringParameters = apiGatewayRequest.QueryStringParameters;
            if(queryStringParameters != null)
            {
                StringBuilder sb = new StringBuilder("?");
                var encoder = UrlEncoder.Default;
                foreach(var kvp in queryStringParameters)
                {
                    if(sb.Length > 1)
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

            if(!requestFeatures.Headers.ContainsKey("Host"))
            {
                var apiId = apiGatewayRequest.RequestContext?.ApiId ?? "";
                var stage = apiGatewayRequest.RequestContext?.Stage ?? "";

                requestFeatures.Headers["Host"] = $"apigateway-{apiId}-{stage}";
            }

            if(!string.IsNullOrEmpty(apiGatewayRequest.Body))
            {
                Byte[] binaryBody;
                if(apiGatewayRequest.IsBase64Encoded)
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
        /// <returns></returns>
        private APIGatewayProxyResponse MarshallResponse(IHttpResponseFeature responseFeatures)
        {
            var response = new APIGatewayProxyResponse
            {
                StatusCode = responseFeatures.StatusCode
            };

            if(responseFeatures.Headers != null)
            {
                response.Headers = new Dictionary<string, string>();
                foreach(var kvp in responseFeatures.Headers)
                {
                    if(kvp.Value.Count == 1)
                    {
                        response.Headers[kvp.Key] = kvp.Value[0];
                    }
                    else
                    {
                        response.Headers[kvp.Key] = string.Join(",", kvp.Value);
                    }                    
                }
            }

            if(responseFeatures.Body != null)
            {
                responseFeatures.Body.Position = 0;
                using (StreamReader reader = new StreamReader(responseFeatures.Body, Encoding.UTF8))
                {
                    response.Body =  reader.ReadToEnd();
                }
            }

            return response;
        }
    }
}
