using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.RuntimeSupport.Helpers;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly LambdaSnapstartExecuteRequestsBeforeSnapshotHelper _snapstartInitHelper;

        internal ILambdaSerializer Serializer;

        /// <summary>
        /// Creates an instance on the LambdaRuntimeSupportServer
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
        public LambdaRuntimeSupportServer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _snapstartInitHelper = _serviceProvider.GetRequiredService<LambdaSnapstartExecuteRequestsBeforeSnapshotHelper>();
            Serializer = serviceProvider.GetRequiredService<ILambdaSerializer>();
        }

        /// <summary>
        /// Start Amazon.Lambda.RuntimeSupport to listen for Lambda events to be processed.
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="application"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [RequiresUnreferencedCode("_snapstartInitHelper Serializes objects to Json")]
        public override Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            base.StartAsync(application, cancellationToken);

            var handlerWrapper = CreateHandlerWrapper(_serviceProvider);

            #if NET8_0_OR_GREATER

            _snapstartInitHelper.RegisterInitializerRequests(handlerWrapper);

            #endif

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
            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            public APIGatewayHttpApiV2MinimalApi(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
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
            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            public APIGatewayRestApiMinimalApi(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
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
            /// <summary>
            /// Create instances
            /// </summary>
            /// <param name="serviceProvider">The IServiceProvider created for the ASP.NET Core application</param>
            public ApplicationLoadBalancerMinimalApi(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }
        }
    }
}
