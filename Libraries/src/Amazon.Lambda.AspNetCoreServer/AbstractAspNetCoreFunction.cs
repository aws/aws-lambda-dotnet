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
using Microsoft.Extensions.Hosting;


namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// Base class for ASP.NET Core Lambda functions.
    /// </summary>
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

    /// <summary>
    /// Base class for ASP.NET Core Lambda functions.
    /// </summary>
    /// <typeparam name="TREQUEST"></typeparam>
    /// <typeparam name="TRESPONSE"></typeparam>
    public abstract class AbstractAspNetCoreFunction<TREQUEST, TRESPONSE> : AbstractAspNetCoreFunction
    {
        private protected IServiceProvider _hostServices;
        private protected LambdaServer _server;
        private protected ILogger _logger;
        private protected StartupMode _startupMode;

        // Defines a mapping from registered content types to the response encoding format
        // which dictates what transformations should be applied before returning response content
        private readonly Dictionary<string, ResponseContentEncoding> _responseContentEncodingForContentType = new Dictionary<string, ResponseContentEncoding>
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
            ["image/x-icon"] = ResponseContentEncoding.Base64,
            ["application/zip"] = ResponseContentEncoding.Base64,
            ["application/pdf"] = ResponseContentEncoding.Base64,
        };

        // Defines a mapping from registered content encodings to the response encoding format
        // which dictates what transformations should be applied before returning response content
        private readonly Dictionary<string, ResponseContentEncoding> _responseContentEncodingForContentEncoding = new Dictionary<string, ResponseContentEncoding>
        {
            ["gzip"] = ResponseContentEncoding.Base64,
            ["deflate"] = ResponseContentEncoding.Base64,
            ["br"] = ResponseContentEncoding.Base64
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
        /// 
        /// </summary>
        /// <param name="hostedServices"></param>
        protected AbstractAspNetCoreFunction(IServiceProvider hostedServices)
        {
            _hostServices = hostedServices;
            _server = this._hostServices.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.IServer)) as LambdaServer;
            _logger = ActivatorUtilities.CreateInstance<Logger<AbstractAspNetCoreFunction<TREQUEST, TRESPONSE>>>(this._hostServices);
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
        /// Registers a mapping from a asp.net content encoding to a lambda response content type to a <see cref="ResponseContentEncoding"/>.
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
        public void RegisterResponseContentEncodingForContentEncoding(string contentEncoding, ResponseContentEncoding encoding)
        {
            _responseContentEncodingForContentEncoding[contentEncoding] = encoding;
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
        protected virtual void Init(IWebHostBuilder builder) { }

        /// <summary>
        /// Creates the IWebHostBuilder similar to WebHost.CreateDefaultBuilder but replacing the registration of the Kestrel web server with a 
        /// registration for Lambda.
        /// </summary>
        /// <returns></returns>
        [Obsolete("Functions should migrate to CreateHostBuilder and use IHostBuilder to setup their ASP.NET Core application. In a future major version update of this library support for IWebHostBuilder will be removed for non .NET Core 2.1 Lambda functions.")]
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

            Init(builder);

            // Swap out Kestrel as the webserver and use our implementation of IServer
            builder.UseLambdaServer();

            return builder;
        }

        /// <summary>
        /// Method to initialize the host builder before starting the host. In a typical Web API this is similar to the main function. 
        /// Setting the Startup class is required in this method.
        /// <para>
        /// It is recommended to not configure the IWebHostBuilder from this method. Instead configure the IWebHostBuilder
        /// in the Init(IWebHostBuilder builder) method. If you configure the IWebHostBuilder in this method the IWebHostBuilder will be
        /// configured twice, here and and as part of CreateHostBuilder.
        /// </para>
        /// </summary>
        /// <example>
        /// <code>
        /// protected override void Init(IHostBuilder builder)
        /// {
        ///     builder
        ///         .UseStartup&lt;Startup&gt;();
        /// }
        /// </code>
        /// </example>
        /// <param name="builder"></param>
        protected virtual void Init(IHostBuilder builder) { }

        /// <summary>
        /// Creates the IHostBuilder similar to Host.CreateDefaultBuilder but replacing the registration of the Kestrel web server with a 
        /// registration for Lambda.
        /// <para>
        /// When overriding this method it is recommended that ConfigureWebHostLambdaDefaults should be called instead of ConfigureWebHostDefaults to ensure the IWebHostBuilder
        /// has the proper services configured for running in Lambda. That includes registering Lambda instead of Kestrel as the IServer implementation
        /// for processing requests.
        /// </para>
        /// </summary>
        /// <returns></returns>
        protected virtual IHostBuilder CreateHostBuilder()
        {
            var builder = Host.CreateDefaultBuilder()
                                .ConfigureWebHostLambdaDefaults(webBuilder =>
                                {
                                    Init(webBuilder);
                                });

            Init(builder);
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
            // For .NET Core 3.1 and above use the IHostBuilder instead of IWebHostBuilder used in .NET Core 2.1. If the user overrode CreateWebHostBuilder
            // then fallback to the original .NET Core 2.1 behavior.
            if (this.GetType().GetMethod("CreateWebHostBuilder", BindingFlags.NonPublic | BindingFlags.Instance).DeclaringType.FullName.StartsWith("Amazon.Lambda.AspNetCoreServer.AbstractAspNetCoreFunction"))
            {
                var builder = CreateHostBuilder();
                builder.ConfigureServices(services =>
                {
                    Utilities.EnsureLambdaServerRegistered(services);
                });                

                var host = builder.Build();
                PostCreateHost(host);

                host.Start();
                this._hostServices = host.Services;
            }
            else
            {
#pragma warning disable 618
                var builder = CreateWebHostBuilder();
#pragma warning restore 618

                var host = builder.Build();
                PostCreateWebHost(host);

                host.Start();
                this._hostServices = host.Services;
            }

            _server = this._hostServices.GetService(typeof(Microsoft.AspNetCore.Hosting.Server.IServer)) as LambdaServer;
            if (_server == null)
            {
                throw new Exception("Failed to find the Lambda implementation for the IServer interface in the IServiceProvider for the Host. This happens if UseLambdaServer was " +
                        "not called when constructing the IWebHostBuilder. If CreateHostBuilder was overridden it is recommended that ConfigureWebHostLambdaDefaults should be used " + 
                        "instead of ConfigureWebHostDefaults to make sure the property Lambda services are registered.");
            }
            _logger = ActivatorUtilities.CreateInstance<Logger<AbstractAspNetCoreFunction<TREQUEST, TRESPONSE>>>(this._hostServices);
        }

        /// <summary>
        /// Creates a context object using the <see cref="LambdaServer"/> field in the class.
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
            if (_responseContentEncodingForContentType.TryGetValue(contentTypeWithoutEncoding, out var encoding))
            {
                return encoding;
            }

            return DefaultResponseContentEncoding;
        }

        /// <summary>
        /// Gets the response content encoding for a content encoding.
        /// </summary>
        /// <param name="contentEncoding"></param>
        /// <returns></returns>
        public ResponseContentEncoding GetResponseContentEncodingForContentEncoding(string contentEncoding)
        {
            if (string.IsNullOrEmpty(contentEncoding))
            {
                return DefaultResponseContentEncoding;
            }

            if (_responseContentEncodingForContentEncoding.TryGetValue(contentEncoding, out var encoding))
            {
                return encoding;
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
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
        public virtual async Task<TRESPONSE> FunctionHandlerAsync(TREQUEST request, ILambdaContext lambdaContext)
        {
            if (!IsStarted)
            {
                Start();
            }

            InvokeFeatures features = new InvokeFeatures();
            MarshallRequest(features, request, lambdaContext);

            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                var httpRequestFeature = (IHttpRequestFeature)features;
                _logger.LogDebug($"ASP.NET Core Request PathBase: {httpRequestFeature.PathBase}, Path: {httpRequestFeature.Path}");
            }

            {
                var itemFeatures = (IItemsFeature)features;
                itemFeatures.Items = new ItemsDictionary();
                itemFeatures.Items[LAMBDA_CONTEXT] = lambdaContext;
                itemFeatures.Items[LAMBDA_REQUEST_OBJECT] = request;
                PostMarshallItemsFeatureFeature(itemFeatures, request, lambdaContext);
            }

            var scope = this._hostServices.CreateScope();
            try
            {
                ((IServiceProvidersFeature)features).RequestServices = scope.ServiceProvider;

                var context = this.CreateContext(features);
                var response = await this.ProcessRequest(lambdaContext, context, features);

                return response;
            }
            finally
            {
                scope.Dispose();
            }
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
                    _logger.LogError(agex, $"Caught AggregateException: '{agex}'");
                    var sb = new StringBuilder();
                    foreach (var newEx in agex.InnerExceptions)
                    {
                        sb.AppendLine(this.ErrorReport(newEx));
                    }

                    _logger.LogError(sb.ToString());
                    ((IHttpResponseFeature)features).StatusCode = 500;
                }
                catch (ReflectionTypeLoadException rex)
                {
                    ex = rex;
                    _logger.LogError(rex, $"Caught ReflectionTypeLoadException: '{rex}'");
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
                    ((IHttpResponseFeature)features).StatusCode = 500;
                }
                catch (Exception e)
                {
                    ex = e;
                    if (rethrowUnhandledError) throw;
                    _logger.LogError(e, $"Unknown error responding to request: {this.ErrorReport(e)}");
                    ((IHttpResponseFeature)features).StatusCode = 500;
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
        /// This method is called after the IWebHost is created from the IWebHostBuilder and the services have been configured. The
        /// WebHost hasn't been started yet.
        /// </summary>
        /// <param name="webHost"></param>
        protected virtual void PostCreateWebHost(IWebHost webHost)
        {

        }

        /// <summary>
        /// This method is called after the IHost is created from the IHostBuilder and the services have been configured. The
        /// Host hasn't been started yet. If the CreateWebHostBuilder method is overloaded then IHostWebBuilder will be used to create
        /// an IWebHost and this method will not be called.
        /// </summary>
        /// <param name="webHost"></param>
        protected virtual void PostCreateHost(IHost webHost)
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
        /// This method is called after marshalling the incoming Lambda request
        /// into ASP.NET Core's ITlsConnectionFeature. Derived classes can overwrite this method to alter
        /// the how the marshalling was done.
        /// </summary>
        /// <param name="aspNetCoreConnectionFeature"></param>
        /// <param name="lambdaRequest"></param>
        /// <param name="lambdaContext"></param>
        protected virtual void PostMarshallTlsConnectionFeature(ITlsConnectionFeature aspNetCoreConnectionFeature, TREQUEST lambdaRequest, ILambdaContext lambdaContext)
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
