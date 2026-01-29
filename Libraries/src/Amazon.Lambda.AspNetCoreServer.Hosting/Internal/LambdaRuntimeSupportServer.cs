using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
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
        private readonly IServiceProvider _serviceProvider;

        internal ILambdaSerializer Serializer;

        /// <summary>
        /// Creates an instance on the LambdaRuntimeSupportServer
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
        public LambdaRuntimeSupportServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            Serializer = serviceProvider.GetRequiredService<ILambdaSerializer>();
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
            var handler = new APIGatewayHttpApiV2MinimalApi(serviceProvider).FunctionHandlerAsync;
            return HandlerWrapper.GetHandlerWrapper(handler, this.Serializer);
        }

        /// <summary>
        /// Create the APIGatewayHttpApiV2ProxyFunction passing in the ASP.NET Core application's IServiceProvider
        /// </summary>
        public class APIGatewayHttpApiV2MinimalApi : APIGatewayHttpApiV2ProxyFunction
        {
            #if NET8_0_OR_GREATER
            private readonly IEnumerable<GetBeforeSnapshotRequestsCollector> _beforeSnapshotRequestsCollectors;
            #endif
            private readonly HostingOptions? _hostingOptions;

            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            public APIGatewayHttpApiV2MinimalApi(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
                #if NET8_0_OR_GREATER
                _beforeSnapshotRequestsCollectors = serviceProvider.GetServices<GetBeforeSnapshotRequestsCollector>();
                #endif

                // Retrieve HostingOptions from service provider (may be null for backward compatibility)
                _hostingOptions = serviceProvider.GetService<HostingOptions>();

                // Apply configuration from HostingOptions if available
                if (_hostingOptions != null)
                {
                    // Apply binary response configuration
                    foreach (var kvp in _hostingOptions.ContentTypeEncodings)
                    {
                        RegisterResponseContentEncodingForContentType(kvp.Key, kvp.Value);
                    }

                    foreach (var kvp in _hostingOptions.ContentEncodingEncodings)
                    {
                        RegisterResponseContentEncodingForContentEncoding(kvp.Key, kvp.Value);
                    }

                    DefaultResponseContentEncoding = _hostingOptions.DefaultResponseContentEncoding;

                    // Apply exception handling configuration
                    IncludeUnhandledExceptionDetailInResponse = _hostingOptions.IncludeUnhandledExceptionDetailInResponse;
                }
            }

            #if NET8_0_OR_GREATER
            protected override IEnumerable<HttpRequestMessage> GetBeforeSnapshotRequests()
            {
                foreach (var collector in _beforeSnapshotRequestsCollectors)
                    if (collector.Request != null)
                        yield return collector.Request;
            }
            #endif

            protected override void PostMarshallRequestFeature(IHttpRequestFeature aspNetCoreRequestFeature, APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallRequestFeature(aspNetCoreRequestFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallRequestFeature?.Invoke(aspNetCoreRequestFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse lambdaResponse, ILambdaContext lambdaContext)
            {
                base.PostMarshallResponseFeature(aspNetCoreResponseFeature, lambdaResponse, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallResponseFeature?.Invoke(aspNetCoreResponseFeature, lambdaResponse, lambdaContext);
            }

            protected override void PostMarshallConnectionFeature(IHttpConnectionFeature aspNetCoreConnectionFeature, APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallConnectionFeature(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallConnectionFeature?.Invoke(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallHttpAuthenticationFeature(IHttpAuthenticationFeature aspNetCoreHttpAuthenticationFeature, APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallHttpAuthenticationFeature(aspNetCoreHttpAuthenticationFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallHttpAuthenticationFeature?.Invoke(aspNetCoreHttpAuthenticationFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallTlsConnectionFeature(ITlsConnectionFeature aspNetCoreConnectionFeature, APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallTlsConnectionFeature(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallTlsConnectionFeature?.Invoke(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallItemsFeatureFeature(IItemsFeature aspNetCoreItemFeature, APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                // Note: LAMBDA_CONTEXT and LAMBDA_REQUEST_OBJECT are preserved by the base implementation
                _hostingOptions?.PostMarshallItemsFeature?.Invoke(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
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
            var handler = new APIGatewayRestApiMinimalApi(serviceProvider).FunctionHandlerAsync;
            return HandlerWrapper.GetHandlerWrapper(handler, this.Serializer);
        }

        /// <summary>
        /// Create the APIGatewayProxyFunction passing in the ASP.NET Core application's IServiceProvider
        /// </summary>
        public class APIGatewayRestApiMinimalApi : APIGatewayProxyFunction
        {
            #if NET8_0_OR_GREATER
            private readonly IEnumerable<GetBeforeSnapshotRequestsCollector> _beforeSnapshotRequestsCollectors;
            #endif
            private readonly HostingOptions? _hostingOptions;

            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            public APIGatewayRestApiMinimalApi(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
                #if NET8_0_OR_GREATER
                _beforeSnapshotRequestsCollectors = serviceProvider.GetServices<GetBeforeSnapshotRequestsCollector>();
                #endif

                // Retrieve HostingOptions from service provider (may be null for backward compatibility)
                _hostingOptions = serviceProvider.GetService<HostingOptions>();

                // Apply configuration from HostingOptions if available
                if (_hostingOptions != null)
                {
                    // Apply binary response configuration
                    foreach (var kvp in _hostingOptions.ContentTypeEncodings)
                    {
                        RegisterResponseContentEncodingForContentType(kvp.Key, kvp.Value);
                    }

                    foreach (var kvp in _hostingOptions.ContentEncodingEncodings)
                    {
                        RegisterResponseContentEncodingForContentEncoding(kvp.Key, kvp.Value);
                    }

                    DefaultResponseContentEncoding = _hostingOptions.DefaultResponseContentEncoding;

                    // Apply exception handling configuration
                    IncludeUnhandledExceptionDetailInResponse = _hostingOptions.IncludeUnhandledExceptionDetailInResponse;
                }
            }

            #if NET8_0_OR_GREATER
            protected override IEnumerable<HttpRequestMessage> GetBeforeSnapshotRequests()
            {
                foreach (var collector in _beforeSnapshotRequestsCollectors)
                    if (collector.Request != null)
                        yield return collector.Request;
            }
            #endif

            protected override void PostMarshallRequestFeature(IHttpRequestFeature aspNetCoreRequestFeature, APIGatewayEvents.APIGatewayProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallRequestFeature(aspNetCoreRequestFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallRequestFeature?.Invoke(aspNetCoreRequestFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, APIGatewayEvents.APIGatewayProxyResponse lambdaResponse, ILambdaContext lambdaContext)
            {
                base.PostMarshallResponseFeature(aspNetCoreResponseFeature, lambdaResponse, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallResponseFeature?.Invoke(aspNetCoreResponseFeature, lambdaResponse, lambdaContext);
            }

            protected override void PostMarshallConnectionFeature(IHttpConnectionFeature aspNetCoreConnectionFeature, APIGatewayEvents.APIGatewayProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallConnectionFeature(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallConnectionFeature?.Invoke(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallHttpAuthenticationFeature(IHttpAuthenticationFeature aspNetCoreHttpAuthenticationFeature, APIGatewayEvents.APIGatewayProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallHttpAuthenticationFeature(aspNetCoreHttpAuthenticationFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallHttpAuthenticationFeature?.Invoke(aspNetCoreHttpAuthenticationFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallTlsConnectionFeature(ITlsConnectionFeature aspNetCoreConnectionFeature, APIGatewayEvents.APIGatewayProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallTlsConnectionFeature(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallTlsConnectionFeature?.Invoke(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallItemsFeatureFeature(IItemsFeature aspNetCoreItemFeature, APIGatewayEvents.APIGatewayProxyRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                // Note: LAMBDA_CONTEXT and LAMBDA_REQUEST_OBJECT are preserved by the base implementation
                _hostingOptions?.PostMarshallItemsFeature?.Invoke(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
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
            var handler = new ApplicationLoadBalancerMinimalApi(serviceProvider).FunctionHandlerAsync;
            return HandlerWrapper.GetHandlerWrapper(handler, this.Serializer);
        }

        /// <summary>
        /// Create the ApplicationLoadBalancerFunction passing in the ASP.NET Core application's IServiceProvider
        /// </summary>
        public class ApplicationLoadBalancerMinimalApi : ApplicationLoadBalancerFunction
        {
            #if NET8_0_OR_GREATER
            private readonly IEnumerable<GetBeforeSnapshotRequestsCollector> _beforeSnapshotRequestsCollectors;
            #endif
            private readonly HostingOptions? _hostingOptions;

            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            public ApplicationLoadBalancerMinimalApi(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
                #if NET8_0_OR_GREATER
                _beforeSnapshotRequestsCollectors = serviceProvider.GetServices<GetBeforeSnapshotRequestsCollector>();
                #endif

                // Retrieve HostingOptions from service provider (may be null for backward compatibility)
                _hostingOptions = serviceProvider.GetService<HostingOptions>();

                // Apply configuration from HostingOptions if available
                if (_hostingOptions != null)
                {
                    // Apply binary response configuration
                    foreach (var kvp in _hostingOptions.ContentTypeEncodings)
                    {
                        RegisterResponseContentEncodingForContentType(kvp.Key, kvp.Value);
                    }

                    foreach (var kvp in _hostingOptions.ContentEncodingEncodings)
                    {
                        RegisterResponseContentEncodingForContentEncoding(kvp.Key, kvp.Value);
                    }

                    DefaultResponseContentEncoding = _hostingOptions.DefaultResponseContentEncoding;

                    // Apply exception handling configuration
                    IncludeUnhandledExceptionDetailInResponse = _hostingOptions.IncludeUnhandledExceptionDetailInResponse;
                }
            }

            #if NET8_0_OR_GREATER
            protected override IEnumerable<HttpRequestMessage> GetBeforeSnapshotRequests()
            {
                foreach (var collector in _beforeSnapshotRequestsCollectors)
                    if (collector.Request != null)
                        yield return collector.Request;
            }
            #endif

            protected override void PostMarshallRequestFeature(IHttpRequestFeature aspNetCoreRequestFeature, ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallRequestFeature(aspNetCoreRequestFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallRequestFeature?.Invoke(aspNetCoreRequestFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallResponseFeature(IHttpResponseFeature aspNetCoreResponseFeature, ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse lambdaResponse, ILambdaContext lambdaContext)
            {
                base.PostMarshallResponseFeature(aspNetCoreResponseFeature, lambdaResponse, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallResponseFeature?.Invoke(aspNetCoreResponseFeature, lambdaResponse, lambdaContext);
            }

            protected override void PostMarshallConnectionFeature(IHttpConnectionFeature aspNetCoreConnectionFeature, ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallConnectionFeature(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallConnectionFeature?.Invoke(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallHttpAuthenticationFeature(IHttpAuthenticationFeature aspNetCoreHttpAuthenticationFeature, ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallHttpAuthenticationFeature(aspNetCoreHttpAuthenticationFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallHttpAuthenticationFeature?.Invoke(aspNetCoreHttpAuthenticationFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallTlsConnectionFeature(ITlsConnectionFeature aspNetCoreConnectionFeature, ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallTlsConnectionFeature(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                _hostingOptions?.PostMarshallTlsConnectionFeature?.Invoke(aspNetCoreConnectionFeature, lambdaRequest, lambdaContext);
            }

            protected override void PostMarshallItemsFeatureFeature(IItemsFeature aspNetCoreItemFeature, ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest lambdaRequest, ILambdaContext lambdaContext)
            {
                base.PostMarshallItemsFeatureFeature(aspNetCoreItemFeature, lambdaRequest, lambdaContext);

                // Invoke configured callback if available
                // Note: LAMBDA_CONTEXT and LAMBDA_REQUEST_OBJECT are preserved by the base implementation
                _hostingOptions?.PostMarshallItemsFeature?.Invoke(aspNetCoreItemFeature, lambdaRequest, lambdaContext);
            }
        }
    }
}
