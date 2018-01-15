using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// ApiGatewayFunction is the base class that is implemented in a ASP.NET Core Web API. The derived class implements
    /// the Init method similar to Main function in the ASP.NET Core. The function handler for the Lambda function will point
    /// to this base class FunctionHandlerAsync method.
    /// </summary>
    public abstract class APIGatewayProxyFunction
    {
        /// <summary>
        /// Key to access the ILambdaContext object from the HttpContext.Items collection.
        /// </summary>
        public const string LAMBDA_CONTEXT = "LambdaContext";

        /// <summary>
        /// Key to access the APIGatewayProxyRequest object from the HttpContext.Items collection.
        /// </summary>
        public const string APIGATEWAY_REQUEST = "APIGatewayRequest";

        private readonly IWebHost _host;
        private readonly APIGatewayServer _server;

        // Defines a mapping from registered content types to the response encoding format
        // which dictates what transformations should be applied before returning response content
        private Dictionary<string, ResponseContentEncoding> _responseContentEncodingForContentType = new Dictionary<string, ResponseContentEncoding>
        {
            // The complete list of registered MIME content-types can be found at:
            //    http://www.iana.org/assignments/media-types/media-types.xhtml

            // Here we just include a few commonly used content types found in
            // Web API responses and allow users to add more as needed below

            ["text/plain"] = ResponseContentEncoding.Default,
            ["text/xml"] = ResponseContentEncoding.Default,
            ["application/xml"] = ResponseContentEncoding.Default,
            ["application/json"] = ResponseContentEncoding.Default,
            ["text/html"] = ResponseContentEncoding.Default,
            ["text/css"] = ResponseContentEncoding.Default,
            ["text/javascript"] = ResponseContentEncoding.Default,
            ["text/ecmascript"] = ResponseContentEncoding.Default,
            ["text/markdown"] = ResponseContentEncoding.Default,
            ["text/csv"] = ResponseContentEncoding.Default,

            ["application/octet-stream"] = ResponseContentEncoding.Base64,
            ["image/png"] = ResponseContentEncoding.Base64,
            ["image/gif"] = ResponseContentEncoding.Base64,
            ["image/jpeg"] = ResponseContentEncoding.Base64,
            ["application/zip"] = ResponseContentEncoding.Base64,
            ["application/pdf"] = ResponseContentEncoding.Base64,
        };

        // Manage the serialization so the raw requests and responses can be logged.
        ILambdaSerializer _serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();

        /// <summary>
        /// Defines the default treatment of response content.
        /// </summary>
        public ResponseContentEncoding DefaultResponseContentEncoding { get; set; } = ResponseContentEncoding.Default;

        /// <summary>
        /// Default constructor that AWS Lambda will invoke.
        /// </summary>
        protected APIGatewayProxyFunction()
        {
            var builder = CreateWebHostBuilder();
            Init(builder);


            _host = builder.Build();
            _host.Start();

            _server = _host.Services.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.IServer)) as APIGatewayServer;
            if(_server == null)
            {
                throw new Exception("Failed to find the implementation APIGatewayServer for the IServer registration. This can happen if UseApiGateway was not called.");
            }
        }

        /// <summary>
        /// Method to initialize the web builder before starting the web host. In a typical Web API this is similar to the main function. 
        /// Setting the Startup class is required in this method.
        /// </summary>
        /// <example>
        /// <code>
        /// protected override void Init(IWebHostBuilder builder)
        /// {
        ///     builder
        ///         .UseStartup&lt;Startup&gt;();
        /// }
        /// </code>
        /// </example>
        /// <param name="builder"></param>
        protected abstract void Init(IWebHostBuilder builder);

        /// <summary>
        /// Creates the IWebHostBuilder similar to WebHost.CreateDefaultBuilder but replacing the registration of the Kestrel web server with a 
        /// registration for ApiGateway.
        /// </summary>
        /// <returns></returns>
        protected virtual IWebHostBuilder CreateWebHostBuilder()
        {
            var builder = new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

                    if (env.IsDevelopment())
                    {
                        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                        if (appAssembly != null)
                        {
                            config.AddUserSecrets(appAssembly, optional: true);
                        }
                    }

                    config.AddEnvironmentVariables();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    if (hostingContext.HostingEnvironment.IsDevelopment())
                    {
                        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                        logging.AddConsole();
                        logging.AddDebug();
                    }
                    else
                    {
                        logging.AddLambdaLogger(hostingContext.Configuration, "Logging");
                    }
                })
                .UseDefaultServiceProvider((hostingContext, options) =>
                {
                    options.ValidateScopes = hostingContext.HostingEnvironment.IsDevelopment();
                })
                .UseApiGateway();


            return builder;
        }

        /// <summary>
        /// This method is what the Lambda function handler points to.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public virtual async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext lambdaContext)
        {
            lambdaContext.Logger.LogLine($"Incoming {request.HttpMethod} requests to {request.Path}");

            InvokeFeatures features = new InvokeFeatures();
            MarshallRequest(features, request);
            lambdaContext.Logger.LogLine($"ASP.NET Core Request PathBase: {((IHttpRequestFeature)features).PathBase}, Path: {((IHttpRequestFeature)features).Path}");

            var context = this.CreateContext(features);

            if (request?.RequestContext?.Authorizer?.Claims != null)
            {
                var identity = new ClaimsIdentity(request.RequestContext.Authorizer.Claims.Select(
                    entry => new Claim(entry.Key, entry.Value.ToString())), "AuthorizerIdentity");

                lambdaContext.Logger.LogLine($"Configuring HttpContext.User with {request.RequestContext.Authorizer.Claims.Count} claims coming from API Gateway's Request Context");
                context.HttpContext.User = new ClaimsPrincipal(identity);
            }

            // Add along the Lambda objects to the HttpContext to give access to Lambda to them in the ASP.NET Core application
            context.HttpContext.Items[LAMBDA_CONTEXT] = lambdaContext;
            context.HttpContext.Items[APIGATEWAY_REQUEST] = request;

            var response = await this.ProcessRequest(lambdaContext, context, features);

            return response;
        }

        /// <summary>
        /// Registers a mapping from a MIME content type to a <see cref="ResponseContentEncoding"/>.
        /// </summary>
        /// <remarks>
        /// The mappings in combination with the <see cref="DefaultResponseContentEncoding"/>
        /// setting will dictate if and how response content should be transformed before being
        /// returned to the calling API Gateway instance.
        /// <para>
        /// The interface between the API Gateway and Lambda provides for repsonse content to
        /// be returned as a UTF-8 string.  In order to return binary content without incurring
        /// any loss or corruption due to transformations to the UTF-8 encoding, it is necessary
        /// to encode the raw response content in Base64 and to annotate the response that it is
        /// Base64-encoded.
        /// </para><para>
        /// <b>NOTE:</b>  In order to use this mechanism to return binary response content, in
        /// addition to registering here any binary MIME content types that will be returned by
        /// your application, it also necessary to register those same content types with the API
        /// Gateway using either the console or the REST interface. Check the developer guide for
        /// further information.
        /// http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-console.html
        /// http://docs.aws.amazon.com/apigateway/latest/developerguide/api-gateway-payload-encodings-configure-with-control-service-api.html
        /// </para>
        /// </remarks>
        public void RegisterResponseContentEncodingForContentType(string contentType, ResponseContentEncoding encoding)
        {
            _responseContentEncodingForContentType[contentType] = encoding;
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
                lambdaContext.Logger.LogLine($"Caught AggregateException: '{agex}'");
                var sb = new StringBuilder();
                foreach (var newEx in agex.InnerExceptions)
                {
                    sb.AppendLine(this.ErrorReport(newEx));
                }

                lambdaContext.Logger.LogLine(sb.ToString());
                defaultStatusCode = 500;
            }
            catch (ReflectionTypeLoadException rex)
            {
                ex = rex;
                lambdaContext.Logger.LogLine($"Caught ReflectionTypeLoadException: '{rex}'");
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

                lambdaContext.Logger.LogLine(sb.ToString());
                defaultStatusCode = 500;
            }
            catch (Exception e)
            {
                ex = e;
                if (rethrowUnhandledError) throw;
                lambdaContext.Logger.LogLine($"Unknown error responding to request: {this.ErrorReport(e)}");
                defaultStatusCode = 500;
            }
            finally
            {
                this._server.Application.DisposeContext(context, ex);
            }

            var response = this.MarshallResponse(features, defaultStatusCode);
            lambdaContext.Logger.LogLine($"Response Base 64 Encoded: {response.IsBase64Encoded}");

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
        /// This method is called after the APIGatewayProxyFunction has marshalled the incoming API Gateway request
        /// into ASP.NET Core's IHttpRequestFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreRequestFeature"></param>
        /// <param name="apiGatewayRequest"></param>
        protected virtual void PostMarshallRequestFeature(IHttpRequestFeature aspNetCoreRequestFeature, APIGatewayProxyRequest apiGatewayRequest)
        {

        }

        /// <summary>
        /// This method is called after the APIGatewayProxyFunction has marshalled the incoming API Gateway request
        /// into ASP.NET Core's IHttpConnectionFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreConnectionFeature"></param>
        /// <param name="apiGatewayRequest"></param>
        protected virtual void PostMarshallConnectionFeature(IHttpConnectionFeature aspNetCoreConnectionFeature, APIGatewayProxyRequest apiGatewayRequest)
        {

        }

        /// <summary>
        /// This method is called after the APIGatewayProxyFunction has marshalled IHttpResponseFeature that came
        /// back from making the request into ASP.NET Core into API Gateway's response object APIGatewayProxyResponse. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreResponseFeature"></param>
        /// <param name="apiGatewayResponse"></param>
        protected virtual void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, APIGatewayProxyResponse apiGatewayResponse)
        {

        }

        /// <summary>
        /// Convert the JSON document received from API Gateway into the InvokeFeatures object.
        /// InvokeFeatures is then passed into IHttpApplication to create the ASP.NET Core request objects.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="apiGatewayRequest"></param>
        protected void MarshallRequest(InvokeFeatures features, APIGatewayProxyRequest apiGatewayRequest)
        {
            {
                var requestFeatures = (IHttpRequestFeature)features;
                requestFeatures.Scheme = "https";
                requestFeatures.Method = apiGatewayRequest.HttpMethod;

                string path = null;
                if (apiGatewayRequest.PathParameters != null && apiGatewayRequest.PathParameters.ContainsKey("proxy"))
                {
                    var proxyPath = apiGatewayRequest.PathParameters["proxy"];
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

                requestFeatures.Path = WebUtility.UrlDecode(path);

                requestFeatures.PathBase = string.Empty;
                if (!string.IsNullOrEmpty(apiGatewayRequest?.RequestContext?.Path))
                {
                    // This is to cover the case where the request coming in is https://myapigatewayid.execute-api.us-west-2.amazonaws.com/Prod where
                    // Prod is the stage name and there is no ending '/'. Path will be set to '/' so to make sure we detect the correct base path
                    // append '/' on the end to make the later EndsWith and substring work correctly.
                    var decodedRequestContextPath = WebUtility.UrlDecode(apiGatewayRequest.RequestContext.Path);
                    if (path.EndsWith("/") && !decodedRequestContextPath.EndsWith("/"))
                    {
                        decodedRequestContextPath += "/";
                    }

                    if (decodedRequestContextPath.EndsWith(path))
                    {
                        requestFeatures.PathBase = decodedRequestContextPath.Substring(0, decodedRequestContextPath.Length - requestFeatures.Path.Length);
                    }
                }


                // API Gateway delivers the query string in a dictionary but must be reconstructed into the full query string
                // before passing into ASP.NET Core framework.
                var queryStringParameters = apiGatewayRequest.QueryStringParameters;
                if (queryStringParameters != null)
                {
                    StringBuilder sb = new StringBuilder("?");
                    foreach (var kvp in queryStringParameters)
                    {
                        if (sb.Length > 1)
                        {
                            sb.Append("&");
                        }
                        sb.Append($"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value.ToString())}");
                    }
                    requestFeatures.QueryString = sb.ToString();
                }
                else
                {
                    requestFeatures.QueryString = string.Empty;
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

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallRequestFeature(requestFeatures, apiGatewayRequest);
            }


            {
                // set up connection features
                var connectionFeatures = (IHttpConnectionFeature)features;

                IPAddress remoteIpAddress;
                if (!string.IsNullOrEmpty(apiGatewayRequest?.RequestContext?.Identity?.SourceIp) &&
                    IPAddress.TryParse(apiGatewayRequest.RequestContext.Identity.SourceIp, out remoteIpAddress))
                {
                    connectionFeatures.RemoteIpAddress = remoteIpAddress;
                }

                if (apiGatewayRequest?.Headers?.ContainsKey("X-Forwarded-Port") == true)
                {
                    connectionFeatures.RemotePort = int.Parse(apiGatewayRequest.Headers["X-Forwarded-Port"]);
                }

                // Call consumers customize method in case they want to change how API Gateway's request
                // was marshalled into ASP.NET Core request.
                PostMarshallConnectionFeature(connectionFeatures, apiGatewayRequest);
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
                // Figure out how we should treat the response content
                var rcEncoding = DefaultResponseContentEncoding;
                if (contentType != null)
                {
                    // ASP.NET Core will typically return content type with encoding like this "application/json; charset=utf-8"
                    // To find the content type in the dictionary we need to strip the encoding off.
                    var contentTypeWithoutEncoding = contentType.Split(';')[0].Trim();
                    if (_responseContentEncodingForContentType.ContainsKey(contentTypeWithoutEncoding))
                    {
                        rcEncoding = _responseContentEncodingForContentType[contentTypeWithoutEncoding];
                    }
                }

                // Do we encode the response content in Base64 or treat it as UTF-8
                if (rcEncoding == ResponseContentEncoding.Base64)
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

            PostMarshallResponseFeature(responseFeatures, response);

            return response;
        }
    }
}
