using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features.Authentication;
#if NETCOREAPP_3_0
using Microsoft.Extensions.Hosting;
#endif


namespace Amazon.Lambda.AspNetCoreServer
{
    public abstract class AbstractAspNetCoreFunction
    {
        /// <summary>
        /// Key to access the ILambdaContext object from the HttpContext.Items collection.
        /// </summary>
        public const string LAMBDA_CONTEXT = "LambdaContext";

        /// <summary>
        /// Key to access the Lambda request object from the HttpContext.Items collection. The object
        /// can be either APIGatewayProxyRequest or ApplicationLoadBalancerRequest depending on the source of the event.
        /// </summary>
        public const string LAMBDA_REQUEST_OBJECT = "LambdaRequestObject";
    }

    public abstract class AbstractAspNetCoreFunction<TREQUEST, TRESPONSE> : AbstractAspNetCoreFunction
    {
        private protected IWebHost _host;
        private protected LambdaServer _server;
        private protected ILogger _logger;
        private protected StartupMode _startupMode;

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
            ["image/jpg"] = ResponseContentEncoding.Base64,
            ["application/zip"] = ResponseContentEncoding.Base64,
            ["application/pdf"] = ResponseContentEncoding.Base64,
        };

        /// <summary>
        /// Default Constructor. The ASP.NET Core Framework will be initialized as part of the construction.
        /// </summary>
        protected AbstractAspNetCoreFunction()
            : this(StartupMode.Constructor)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected AbstractAspNetCoreFunction(StartupMode startupMode)
        {
            _startupMode = startupMode;

            if (_startupMode == StartupMode.Constructor)
            {
                Start();
            }
        }

        /// <summary>
        /// Defines the default treatment of response content.
        /// </summary>
        public ResponseContentEncoding DefaultResponseContentEncoding { get; set; } = ResponseContentEncoding.Default;

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
        /// registration for Lambda.
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
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));

                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT")))
                    {
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
                });


            return builder;
        }

        private protected bool IsStarted
        {
            get
            {
                return _server != null;
            }
        }

        /// <summary>
        /// Should be called in the derived constructor 
        /// </summary>
        protected void Start()
        {
            var builder = CreateWebHostBuilder();
            Init(builder);

            // Swap out Kestrel as the webserver and use our implementation of IServer
            builder.UseLambdaServer();


            _host = builder.Build();
            PostCreateWebHost(_host);

            _host.Start();

            _server = _host.Services.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.IServer)) as LambdaServer;
            if (_server == null)
            {
                throw new Exception("Failed to find the implementation Lambda for the IServer registration. This can happen if UseLambdaServer was not called.");
            }
            _logger = ActivatorUtilities.CreateInstance<Logger<APIGatewayProxyFunction>>(this._host.Services);
        }

        /// <summary>
        /// Creates a <see cref="HostingApplication.Context"/> object using the <see cref="LambdaServer"/> field in the class.
        /// </summary>
        /// <param name="features"><see cref="IFeatureCollection"/> implementation.</param>
        protected object CreateContext(IFeatureCollection features)
        {
            return _server.Application.CreateContext(features);
        }

        /// <summary>
        /// Gets the response content encoding for a content type.
        /// </summary>
        /// <param name="contentType"></param>
        /// <returns></returns>
        public ResponseContentEncoding GetResponseContentEncodingForContentType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return DefaultResponseContentEncoding;
            }

            // ASP.NET Core will typically return content type with encoding like this "application/json; charset=utf-8"
            // To find the content type in the dictionary we need to strip the encoding off.
            var contentTypeWithoutEncoding = contentType.Split(';')[0].Trim();
            if (_responseContentEncodingForContentType.ContainsKey(contentTypeWithoutEncoding))
            {
                return _responseContentEncodingForContentType[contentTypeWithoutEncoding];
            }

            return DefaultResponseContentEncoding;
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
        /// This method is what the Lambda function handler points to.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="lambdaContext"></param>
        /// <returns></returns>
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public virtual async Task<TRESPONSE> FunctionHandlerAsync(TREQUEST request, ILambdaContext lambdaContext)
        {
            if (!IsStarted)
            {
                Start();
            }

            InvokeFeatures features = new InvokeFeatures();
            MarshallRequest(features, request, lambdaContext);

            _logger.LogDebug($"ASP.NET Core Request PathBase: {((IHttpRequestFeature)features).PathBase}, Path: {((IHttpRequestFeature)features).Path}");

            
            {
                var itemFeatures = (IItemsFeature) features;
                itemFeatures.Items = new ItemsDictionary();
                itemFeatures.Items[LAMBDA_CONTEXT] = lambdaContext;
                itemFeatures.Items[LAMBDA_REQUEST_OBJECT] = request;
                PostMarshallItemsFeatureFeature(itemFeatures, request, lambdaContext);
            }
            
            var context = this.CreateContext(features);
            var response = await this.ProcessRequest(lambdaContext, context, features);

            return response;
        }

        /// <summary>
        /// Processes the current request.
        /// </summary>
        /// <param name="lambdaContext"><see cref="ILambdaContext"/> implementation.</param>
        /// <param name="context">The hosting application request context object.</param>
        /// <param name="features">An <see cref="InvokeFeatures"/> instance.</param>
        /// <param name="rethrowUnhandledError">
        /// If specified, an unhandled exception will be rethrown for custom error handling.
        /// Ensure that the error handling code calls 'this.MarshallResponse(features, 500);' after handling the error to return a the typed Lambda object to the user.
        /// </param>
        protected async Task<TRESPONSE> ProcessRequest(ILambdaContext lambdaContext, object context, InvokeFeatures features, bool rethrowUnhandledError = false)
        {
            var defaultStatusCode = 200;
            Exception ex = null;
            try
            {
                try
                {
                    await this._server.Application.ProcessRequestAsync(context);
                }
                catch (AggregateException agex)
                {
                    ex = agex;
                    _logger.LogError($"Caught AggregateException: '{agex}'");
                    var sb = new StringBuilder();
                    foreach (var newEx in agex.InnerExceptions)
                    {
                        sb.AppendLine(this.ErrorReport(newEx));
                    }

                    _logger.LogError(sb.ToString());
                    defaultStatusCode = 500;
                }
                catch (ReflectionTypeLoadException rex)
                {
                    ex = rex;
                    _logger.LogError($"Caught ReflectionTypeLoadException: '{rex}'");
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

                    _logger.LogError(sb.ToString());
                    defaultStatusCode = 500;
                }
                catch (Exception e)
                {
                    ex = e;
                    if (rethrowUnhandledError) throw;
                    _logger.LogError($"Unknown error responding to request: {this.ErrorReport(e)}");
                    defaultStatusCode = 500;
                }

                if (features.ResponseStartingEvents != null)
                {
                    await features.ResponseStartingEvents.ExecuteAsync();
                }
                var response = this.MarshallResponse(features, lambdaContext, defaultStatusCode);

                if (ex != null)
                {
                    InternalCustomResponseExceptionHandling(response, lambdaContext, ex);
                }

                if (features.ResponseCompletedEvents != null)
                {
                    await features.ResponseCompletedEvents.ExecuteAsync();
                }

                return response;
            }
            finally
            {
                this._server.Application.DisposeContext(context, ex);
            }
        }
        

        private protected virtual void InternalCustomResponseExceptionHandling(TRESPONSE lambdaReponse, ILambdaContext lambdaContext, Exception ex)
        {

        }

        /// <summary>
        /// This methid is called after the IWebHost is created from the IWebHostBuilder and the services have been configured. The
        /// WebHost hasn't been started yet.
        /// </summary>
        /// <param name="webHost"></param>
        protected virtual void PostCreateWebHost(IWebHost webHost)
        {

        }
        
        /// <summary>
        /// This method is called after marshalling the incoming Lambda request
        /// into ASP.NET Core's IItemsFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreItemFeature"></param>
        /// <param name="lambdaRequest"></param>
        /// <param name="lambdaContext"></param>
        protected virtual void PostMarshallItemsFeatureFeature(IItemsFeature aspNetCoreItemFeature, TREQUEST lambdaRequest, ILambdaContext lambdaContext)
        {

        }
        
        /// <summary>
        /// This method is called after marshalling the incoming Lambda request
        /// into ASP.NET Core's IHttpAuthenticationFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreHttpAuthenticationFeature"></param>
        /// <param name="lambdaRequest"></param>
        /// <param name="lambdaContext"></param>
        protected virtual void PostMarshallHttpAuthenticationFeature(IHttpAuthenticationFeature aspNetCoreHttpAuthenticationFeature, TREQUEST lambdaRequest, ILambdaContext lambdaContext)
        {

        }        

        /// <summary>
        /// This method is called after marshalling the incoming Lambda request
        /// into ASP.NET Core's IHttpRequestFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreRequestFeature"></param>
        /// <param name="lambdaRequest"></param>
        /// <param name="lambdaContext"></param>
        protected virtual void PostMarshallRequestFeature(IHttpRequestFeature aspNetCoreRequestFeature, TREQUEST lambdaRequest, ILambdaContext lambdaContext)
        {

        }

        /// <summary>
        /// This method is called after marshalling the incoming Lambda request
        /// into ASP.NET Core's IHttpConnectionFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreConnectionFeature"></param>
        /// <param name="lambdaRequest"></param>
        /// <param name="lambdaContext"></param>
        protected virtual void PostMarshallConnectionFeature(IHttpConnectionFeature aspNetCoreConnectionFeature, TREQUEST lambdaRequest, ILambdaContext lambdaContext)
        {

        }

        /// <summary>
        /// This method is called after marshalling the IHttpResponseFeature that came
        /// back from making the request into ASP.NET Core into the Lamdba response object. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreResponseFeature"></param>
        /// <param name="lambdaResponse"></param>
        /// <param name="lambdaContext"></param>
        protected virtual void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, TRESPONSE lambdaResponse, ILambdaContext lambdaContext)
        {

        }

        /// <summary>
        /// Converts the Lambda request object into ASP.NET Core InvokeFeatures used to create the HostingApplication.Context.
        /// </summary>
        /// <param name="features"></param>
        /// <param name="lambdaRequest"></param>
        /// <param name="lambdaContext"></param>
        protected abstract void MarshallRequest(InvokeFeatures features, TREQUEST lambdaRequest, ILambdaContext lambdaContext);

        /// <summary>
        /// Convert the ASP.NET Core response to the Lambda response object.
        /// </summary>
        /// <param name="responseFeatures"></param>
        /// <param name="lambdaContext"></param>
        /// <param name="statusCodeIfNotSet"></param>
        /// <returns></returns>
        protected abstract TRESPONSE MarshallResponse(IHttpResponseFeature responseFeatures, ILambdaContext lambdaContext, int statusCodeIfNotSet = 200);
    }
}
