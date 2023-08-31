using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Amazon.Lambda.AspNetCoreServer.Hosting.Internal
{
    /// <summary>
    /// Subclass of Amazon.Lambda.AspNetCoreServer.LambdaServer that also starts
    /// up Amazon.Lambda.RuntimeSupport as part of the IServer startup.
    /// 
    /// This is an abstract class with subclasses for each of the possible Lambda event sources.
    /// </summary>
    public abstract class LambdaRuntimeSupportServer : LambdaServer
    {
        IServiceProvider _serviceProvider;
        internal ILambdaSerializer Serializer;
        internal IEncodingOptions EncodingOptions;

        /// <summary>
        /// Creates an instance on the LambdaRuntimeSupportServer
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
        public LambdaRuntimeSupportServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Serializer = serviceProvider.GetRequiredService<ILambdaSerializer>();
            EncodingOptions = serviceProvider.GetRequiredService<IEncodingOptions>();
        }

        /// <summary>
        /// Start Amazon.Lambda.RuntimeSupport to listen for Lambda events to be processed.
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="application"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            base.StartAsync(application, cancellationToken);

            var handlerWrapper = CreateHandlerWrapper(_serviceProvider);
            var bootStrap = new LambdaBootstrap(handlerWrapper);
            return bootStrap.RunAsync();
        }

        /// <summary>
        /// Abstract method that creates the HandlerWrapper that will be invoked for each Lambda event.
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        protected abstract HandlerWrapper CreateHandlerWrapper(IServiceProvider serviceProvider);
    }

    /// <summary>
    /// IServer for handlying Lambda events from an API Gateway HTTP API.
    /// </summary>
    public class APIGatewayHttpApiV2LambdaRuntimeSupportServer : LambdaRuntimeSupportServer
    {
        /// <summary>
        /// Create instances
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
        public APIGatewayHttpApiV2LambdaRuntimeSupportServer(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        /// <summary>
        /// Creates HandlerWrapper for processing events from API Gateway HTTP API
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        protected override HandlerWrapper CreateHandlerWrapper(IServiceProvider serviceProvider)
        {
            var handler = new APIGatewayHttpApiV2MinimalApi(serviceProvider, this.EncodingOptions).FunctionHandlerAsync;
            return HandlerWrapper.GetHandlerWrapper(handler, this.Serializer);
        }

        /// <summary>
        /// Create the APIGatewayHttpApiV2ProxyFunction passing in the ASP.NET Core application's IServiceProvider
        /// </summary>
        public class APIGatewayHttpApiV2MinimalApi : APIGatewayHttpApiV2ProxyFunction
        {
            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            /// <param name="encodingOptions">The encoding options to register</param>
            public APIGatewayHttpApiV2MinimalApi(IServiceProvider serviceProvider, IEncodingOptions? encodingOptions = null)
                : base(serviceProvider)
            {
                if(encodingOptions != null)
                {
                    if (encodingOptions.ResponseContentEncodingForContentType != null)
                    {
                        foreach (var responseContentEncodingForContentType in encodingOptions.ResponseContentEncodingForContentType)
                        {
                            this.RegisterResponseContentEncodingForContentType(responseContentEncodingForContentType.Key, responseContentEncodingForContentType.Value);
                        }
                    }

                    if (encodingOptions.ResponseContentEncodingForContentEncoding != null)
                    {
                        foreach (var responseContentEncodingForContentEncoding in encodingOptions.ResponseContentEncodingForContentEncoding)
                        {
                            this.RegisterResponseContentEncodingForContentEncoding(responseContentEncodingForContentEncoding.Key, responseContentEncodingForContentEncoding.Value);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// IServer for handlying Lambda events from an API Gateway REST API.
    /// </summary>
    public class APIGatewayRestApiLambdaRuntimeSupportServer : LambdaRuntimeSupportServer
    {
        /// <summary>
        /// Create instances
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
        public APIGatewayRestApiLambdaRuntimeSupportServer(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        /// <summary>
        /// Creates HandlerWrapper for processing events from API Gateway REST API
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        protected override HandlerWrapper CreateHandlerWrapper(IServiceProvider serviceProvider)
        {
            var handler = new APIGatewayRestApiMinimalApi(serviceProvider, this.EncodingOptions).FunctionHandlerAsync;
            return HandlerWrapper.GetHandlerWrapper(handler, this.Serializer);
        }

        /// <summary>
        /// Create the APIGatewayProxyFunction passing in the ASP.NET Core application's IServiceProvider
        /// </summary>
        public class APIGatewayRestApiMinimalApi : APIGatewayProxyFunction
        {
            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            /// /// <param name="encodingOptions">The encoding options to register</param>
            public APIGatewayRestApiMinimalApi(IServiceProvider serviceProvider, IEncodingOptions? encodingOptions = null)
                : base(serviceProvider)
            {
                if (encodingOptions != null)
                {
                    if (encodingOptions.ResponseContentEncodingForContentType != null)
                    {
                        foreach (var responseContentEncodingForContentType in encodingOptions.ResponseContentEncodingForContentType)
                        {
                            this.RegisterResponseContentEncodingForContentType(responseContentEncodingForContentType.Key, responseContentEncodingForContentType.Value);
                        }
                    }

                    if (encodingOptions.ResponseContentEncodingForContentEncoding != null)
                    {
                        foreach (var responseContentEncodingForContentEncoding in encodingOptions.ResponseContentEncodingForContentEncoding)
                        {
                            this.RegisterResponseContentEncodingForContentEncoding(responseContentEncodingForContentEncoding.Key, responseContentEncodingForContentEncoding.Value);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// IServer for handlying Lambda events from an Application Load Balancer.
    /// </summary>
    public class ApplicationLoadBalancerLambdaRuntimeSupportServer : LambdaRuntimeSupportServer
    {
        /// <summary>
        /// Create instances
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
        public ApplicationLoadBalancerLambdaRuntimeSupportServer(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        /// <summary>
        /// Creates HandlerWrapper for processing events from API Gateway REST API
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        protected override HandlerWrapper CreateHandlerWrapper(IServiceProvider serviceProvider)
        {
            var handler = new ApplicationLoadBalancerMinimalApi(serviceProvider, this.EncodingOptions).FunctionHandlerAsync;
            return HandlerWrapper.GetHandlerWrapper(handler, this.Serializer);
        }

        /// <summary>
        /// Create the ApplicationLoadBalancerFunction passing in the ASP.NET Core application's IServiceProvider
        /// </summary>
        public class ApplicationLoadBalancerMinimalApi : ApplicationLoadBalancerFunction
        {
            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            /// <param name="encodingOptions">The encoding options to register</param>
            public ApplicationLoadBalancerMinimalApi(IServiceProvider serviceProvider, IEncodingOptions? encodingOptions = null)
                : base(serviceProvider)
            {
                if (encodingOptions != null)
                {
                    if (encodingOptions.ResponseContentEncodingForContentType != null)
                    {
                        foreach (var responseContentEncodingForContentType in encodingOptions.ResponseContentEncodingForContentType)
                        {
                            this.RegisterResponseContentEncodingForContentType(responseContentEncodingForContentType.Key, responseContentEncodingForContentType.Value);
                        }
                    }

                    if (encodingOptions.ResponseContentEncodingForContentEncoding != null)
                    {
                        foreach (var responseContentEncodingForContentEncoding in encodingOptions.ResponseContentEncodingForContentEncoding)
                        {
                            this.RegisterResponseContentEncodingForContentEncoding(responseContentEncodingForContentEncoding.Key, responseContentEncodingForContentEncoding.Value);
                        }
                    }
                }
            }
        }
    }
}