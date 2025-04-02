using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Enum for the possible event sources that will send HTTP request into the ASP.NET Core Lambda function.
    /// </summary>
    public enum LambdaEventSource 
    {
        /// <summary>
        /// API Gateway REST API
        /// </summary>
        RestApi, 

        /// <summary>
        /// API Gateway HTTP API
        /// </summary>
        HttpApi, 

        /// <summary>
        /// ELB Application Load Balancer
        /// </summary>
        ApplicationLoadBalancer
    }

    /// <summary>
    /// Extension methods to IServiceCollection. 
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add the ability to run the ASP.NET Core Lambda function in AWS Lambda. If the project is not running in Lambda 
        /// this method will do nothing allowing the normal Kestrel webserver to host the application.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="eventSource"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("For Native AOT the overload passing in a SourceGeneratorLambdaJsonSerializer instance must be used to avoid reflection with JSON serialization.")]
        public static IServiceCollection AddAWSLambdaHosting(this IServiceCollection services, LambdaEventSource eventSource)
        {
            // Not running in Lambda so exit and let Kestrel be the web server
            return services.AddAWSLambdaHosting(eventSource, (Action<HostingOptions>?)null);
        }

        /// <summary>
        /// Add the ability to run the ASP.NET Core Lambda function in AWS Lambda. If the project is not running in Lambda 
        /// this method will do nothing allowing the normal Kestrel webserver to host the application.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="eventSource"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("For Native AOT the overload passing in a SourceGeneratorLambdaJsonSerializer instance must be used to avoid reflection with JSON serialization.")]
        public static IServiceCollection AddAWSLambdaHosting(this IServiceCollection services, LambdaEventSource eventSource, Action<HostingOptions>? configure = null)
        {
            if (TryLambdaSetup(services, eventSource, configure, out var hostingOptions))
            {
                services.TryAddSingleton<ILambdaSerializer>(hostingOptions!.Serializer ?? new DefaultLambdaJsonSerializer());
            }

            return services;
        }

        /// <summary>
        /// Add the ability to run the ASP.NET Core Lambda function in AWS Lambda. If the project is not running in Lambda 
        /// this method will do nothing allowing the normal Kestrel webserver to host the application.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="eventSource"></param>
        /// <param name="serializer"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IServiceCollection AddAWSLambdaHosting<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection services, LambdaEventSource eventSource, SourceGeneratorLambdaJsonSerializer<T> serializer, Action<HostingOptions>? configure = null)
            where T : JsonSerializerContext
        {
            if(TryLambdaSetup(services, eventSource, configure, out var hostingOptions))
            {
                services.TryAddSingleton<ILambdaSerializer>(serializer ?? hostingOptions!.Serializer);
            }

            return services;
        }

        #if NET8_0_OR_GREATER
        /// <summary>
        /// Adds a function meant to initialize the asp.net and lambda pipelines during <see cref="SnapshotRestore.RegisterBeforeSnapshot"/>
        /// improving the performance gains offered by SnapStart.
        /// <para />
        /// Pass a function with one or more <see cref="HttpRequestMessage"/>s that will be used to invoke
        /// Routes in your lambda function.  The returned <see cref="HttpRequestMessage"/> must have a relative
        /// <see cref="HttpRequestMessage.RequestUri"/>.
        /// <para />.
        /// Be aware that this will invoke your applications function handler code
        /// multiple times.  Additionally, tt uses a mock <see cref="ILambdaContext"/>
        /// which may not be fully populated.
        /// <para />
        /// This method automatically registers with <see cref="SnapshotRestore.RegisterBeforeSnapshot"/>.
        /// <para />
        /// If SnapStart is not enabled, then this method is ignored and <paramref name="beforeSnapStartRequests"/> is never invoked.
        /// <para />
        /// Example:
        /// <para />
        /// <code>
        /// <![CDATA[
        /// // Example Minimal Api
        /// var builder = WebApplication.CreateSlimBuilder(args);
        ///
        /// builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
        /// 
        /// // Initialize asp.net pipeline before Snapshot
        /// builder.Services.AddAWSLambdaBeforeSnapshotRequest(() => [
        ///     new HttpRequestMessage(HttpMethod.Get, "/test")
        /// });
        /// 
        /// var app = builder.Build();
        /// 
        /// app.MapGet("/test", () => "Success");
        /// 
        /// app.Run(); 
        /// ]]>
        /// </code>
        /// </summary>
        #else
        /// <summary>
        /// Snapstart requires your application to target .NET 8 or above.
        /// </summary>
        #endif
        public static IServiceCollection AddAWSLambdaBeforeSnapshotRequest(this IServiceCollection services, Func<IEnumerable<HttpRequestMessage>> beforeSnapStartRequests)
        {
            #if NET8_0_OR_GREATER
            LambdaSnapstartExecuteRequestsBeforeSnapshotHelper.Registrar.Register(beforeSnapStartRequests);
            #endif

            return services;
        }

        /// <inheritdoc cref="AddAWSLambdaBeforeSnapshotRequest(IServiceCollection,Func{IEnumerable{HttpRequestMessage}})"/>
        public static IServiceCollection AddAWSLambdaBeforeSnapshotRequest(this IServiceCollection services, Func<HttpRequestMessage> beforeSnapStartRequest)
        {
            return AddAWSLambdaBeforeSnapshotRequest(services,
                () => new List<HttpRequestMessage>
                {
                    beforeSnapStartRequest()
                });
        }

        private static bool TryLambdaSetup(IServiceCollection services, LambdaEventSource eventSource, Action<HostingOptions>? configure, out HostingOptions? hostingOptions)
        {
            hostingOptions = null;

            // Not running in Lambda so exit and let Kestrel be the web server
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
                return false;

            hostingOptions = new HostingOptions();

            if (configure != null)
                configure.Invoke(hostingOptions);

            var serverType = eventSource switch
            {
                LambdaEventSource.HttpApi => typeof(APIGatewayHttpApiV2LambdaRuntimeSupportServer),
                LambdaEventSource.RestApi => typeof(APIGatewayRestApiLambdaRuntimeSupportServer),
                LambdaEventSource.ApplicationLoadBalancer => typeof(ApplicationLoadBalancerLambdaRuntimeSupportServer),
                _ => throw new ArgumentException($"Event source type {eventSource} unknown")
            };

            Utilities.EnsureLambdaServerRegistered(services, serverType);

            #if NET8_0_OR_GREATER
            // register a LambdaSnapStartInitializerHttpMessageHandler
            services.AddSingleton<LambdaSnapstartExecuteRequestsBeforeSnapshotHelper>(new LambdaSnapstartExecuteRequestsBeforeSnapshotHelper(eventSource));
            #endif

            return true;
        }
    }
}
