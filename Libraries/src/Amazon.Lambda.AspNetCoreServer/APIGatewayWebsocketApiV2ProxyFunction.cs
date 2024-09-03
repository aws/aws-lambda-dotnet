using System;

using Microsoft.AspNetCore.Http;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// Base class for ASP.NET Core Lambda functions that are getting request from API Gateway Websocket API V2 payload format.
    ///
    /// The http method is fixed as POST. Requests are handled using the RouteKey, so the same lambda should be referenced by multiple API Gateway routes for the ASP.NET Core IServer to successfully route requests.
    /// </summary>
    public abstract class APIGatewayWebsocketApiV2ProxyFunction : APIGatewayHttpApiV2ProxyFunction
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        protected APIGatewayWebsocketApiV2ProxyFunction()
            : base()
        {

        }

        /// <inheritdoc/>
        /// <param name="startupMode">Configure when the ASP.NET Core framework will be initialized</param>
        protected APIGatewayWebsocketApiV2ProxyFunction(StartupMode startupMode)
            : base(startupMode)
        {

        }

        /// <summary>
        /// Constructor used by Amazon.Lambda.AspNetCoreServer.Hosting to support ASP.NET Core projects using the Minimal API style.
        /// </summary>
        /// <param name="hostedServices"></param>
        protected APIGatewayWebsocketApiV2ProxyFunction(IServiceProvider hostedServices)
            : base(hostedServices)
        {
            _hostServices = hostedServices;
        }

        /// <inheritdoc/>
        /// <param name="apiGatewayRequest"></param>
        /// <returns>string</returns>
        protected override string ParseHttpPath(APIGatewayHttpApiV2ProxyRequest apiGatewayRequest)
        {
            var path = "/" + Utilities.DecodeResourcePath(apiGatewayRequest.RequestContext.RouteKey);
            return path;
        }

        /// <inheritdoc/>
        /// <param name="apiGatewayRequest"></param>
        /// <returns>string</returns>
        protected override string ParseHttpMethod(APIGatewayHttpApiV2ProxyRequest apiGatewayRequest)
        {
            return "POST";
        }

        /// <inheritdoc/>
        /// <returns>IHeaderDictionary</returns>
        protected override IHeaderDictionary AddRequestHeaders(APIGatewayHttpApiV2ProxyRequest apiGatewayRequest, IHeaderDictionary headers)
        {
            headers = base.AddRequestHeaders(apiGatewayRequest, headers);
            headers["Content-Type"] = "application/json";
            return headers;
        }
    }
}
