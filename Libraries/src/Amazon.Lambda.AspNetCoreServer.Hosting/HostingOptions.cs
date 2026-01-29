using Amazon.Lambda.Core;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;

namespace Amazon.Lambda.AspNetCoreServer.Hosting;

/// <summary>
/// Options for configuring AWS Lambda hosting for ASP.NET Core
/// </summary>
public class HostingOptions
{
    /// <summary>
    /// The ILambdaSerializer used by Lambda to convert the incoming event JSON into the .NET event type and serialize the .NET response type
    /// back to JSON to return to Lambda.
    /// </summary>
    public ILambdaSerializer Serializer { get; set; }

    /// <summary>
    /// The default response content encoding to use when no explicit content type or content encoding mapping is registered.
    /// Defaults to ResponseContentEncoding.Default (UTF-8 text).
    /// </summary>
    public ResponseContentEncoding DefaultResponseContentEncoding { get; set; } = ResponseContentEncoding.Default;

    /// <summary>
    /// Controls whether unhandled exception details are included in responses.
    /// Defaults to false for security.
    /// </summary>
    public bool IncludeUnhandledExceptionDetailInResponse { get; set; } = false;

    /// <summary>
    /// Callback invoked after request marshalling to customize the HTTP request feature.
    /// Receives the IHttpRequestFeature, Lambda request object, and ILambdaContext.
    /// The Lambda request object will need to be cast to the appropriate type based on the event source.
    /// <para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>API Type</term>
    ///     <description>Event Type</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.HttpApi">HttpApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest">APIGatewayHttpApiV2ProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.RestApi">RestApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest">APIGatewayProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.ApplicationLoadBalancer">ApplicationLoadBalancer</see></term>
    ///     <description><see cref="Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest">ApplicationLoadBalancerRequest</see></description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public Action<IHttpRequestFeature, object, ILambdaContext>? PostMarshallRequestFeature { get; set; }

    /// <summary>
    /// Callback invoked after response marshalling to customize the HTTP response feature.
    /// Receives the IHttpResponseFeature, Lambda response object, and ILambdaContext.
    /// The Lambda response object object will need to be cast to the appropriate type based on the event source.
    /// <para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>API Type</term>
    ///     <description>Event Type</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.HttpApi">HttpApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse">APIGatewayHttpApiV2ProxyResponse</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.RestApi">RestApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse">APIGatewayProxyResponse</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.ApplicationLoadBalancer">ApplicationLoadBalancer</see></term>
    ///     <description><see cref="Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse">ApplicationLoadBalancerResponse</see></description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public Action<IHttpResponseFeature, object, ILambdaContext>? PostMarshallResponseFeature { get; set; }

    /// <summary>
    /// Callback invoked after connection marshalling to customize the HTTP connection feature.
    /// Receives the IHttpConnectionFeature, Lambda request object, and ILambdaContext.
    /// The Lambda request object will need to be cast to the appropriate type based on the event source.
    /// <para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>API Type</term>
    ///     <description>Event Type</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.HttpApi">HttpApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest">APIGatewayHttpApiV2ProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.RestApi">RestApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest">APIGatewayProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.ApplicationLoadBalancer">ApplicationLoadBalancer</see></term>
    ///     <description><see cref="Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest">ApplicationLoadBalancerRequest</see></description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public Action<IHttpConnectionFeature, object, ILambdaContext>? PostMarshallConnectionFeature { get; set; }

    /// <summary>
    /// Callback invoked after authentication marshalling to customize the HTTP authentication feature.
    /// Receives the IHttpAuthenticationFeature, Lambda request object, and ILambdaContext.
    /// The Lambda request object will need to be cast to the appropriate type based on the event source.
    /// <para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>API Type</term>
    ///     <description>Event Type</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.HttpApi">HttpApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest">APIGatewayHttpApiV2ProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.RestApi">RestApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest">APIGatewayProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.ApplicationLoadBalancer">ApplicationLoadBalancer</see></term>
    ///     <description><see cref="Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest">ApplicationLoadBalancerRequest</see></description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public Action<IHttpAuthenticationFeature, object, ILambdaContext>? PostMarshallHttpAuthenticationFeature { get; set; }

    /// <summary>
    /// Callback invoked after TLS connection marshalling to customize the TLS connection feature.
    /// Receives the ITlsConnectionFeature, Lambda request object, and ILambdaContext.
    /// The Lambda request object will need to be cast to the appropriate type based on the event source.
    /// <para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>API Type</term>
    ///     <description>Event Type</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.HttpApi">HttpApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest">APIGatewayHttpApiV2ProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.RestApi">RestApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest">APIGatewayProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.ApplicationLoadBalancer">ApplicationLoadBalancer</see></term>
    ///     <description><see cref="Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest">ApplicationLoadBalancerRequest</see></description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public Action<ITlsConnectionFeature, object, ILambdaContext>? PostMarshallTlsConnectionFeature { get; set; }

    /// <summary>
    /// Callback invoked after items marshalling to customize the items feature.
    /// Receives the IItemsFeature, Lambda request object, and ILambdaContext.
    /// The Lambda request object will need to be cast to the appropriate type based on the event source.
    /// <para>
    /// <list type="table">
    ///   <listheader>
    ///     <term>API Type</term>
    ///     <description>Event Type</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.HttpApi">HttpApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest">APIGatewayHttpApiV2ProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.RestApi">RestApi</see></term>
    ///     <description><see cref="Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest">APIGatewayProxyRequest</see></description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="Microsoft.Extensions.DependencyInjection.LambdaEventSource.ApplicationLoadBalancer">ApplicationLoadBalancer</see></term>
    ///     <description><see cref="Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerRequest">ApplicationLoadBalancerRequest</see></description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public Action<IItemsFeature, object, ILambdaContext>? PostMarshallItemsFeature { get; set; }

    /// <summary>
    /// Internal storage for content type to response content encoding mappings.
    /// </summary>
    internal Dictionary<string, ResponseContentEncoding> ContentTypeEncodings { get; } = new();

    /// <summary>
    /// Internal storage for content encoding to response content encoding mappings.
    /// </summary>
    internal Dictionary<string, ResponseContentEncoding> ContentEncodingEncodings { get; } = new();

    /// <summary>
    /// Registers a response content encoding for a specific content type.
    /// </summary>
    /// <param name="contentType">The content type (e.g., "application/json", "image/png")</param>
    /// <param name="encoding">The response content encoding to use for this content type</param>
    public void RegisterResponseContentEncodingForContentType(string contentType, ResponseContentEncoding encoding)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return;
        }

        ContentTypeEncodings[contentType] = encoding;
    }

    /// <summary>
    /// Registers a response content encoding for a specific content encoding.
    /// </summary>
    /// <param name="contentEncoding">The content encoding (e.g., "gzip", "deflate", "br")</param>
    /// <param name="encoding">The response content encoding to use for this content encoding</param>
    public void RegisterResponseContentEncodingForContentEncoding(string contentEncoding, ResponseContentEncoding encoding)
    {
        if (string.IsNullOrEmpty(contentEncoding))
        {
            return;
        }

        ContentEncodingEncodings[contentEncoding] = encoding;
    }
}
