using System;

using Microsoft.AspNetCore.Http;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.AspNetCoreServer.Internal;

namespace Amazon.Lambda.AspNetCoreServer
{
    /// <summary>
    /// Base class for ASP.NET Core Lambda functions invoked by API Gateway Websocket APIs.
    ///
    /// Websocket events are surfaced as POST requests whose path is the RouteKey, so the same Lambda
    /// should be referenced by every websocket route that has a matching ASP.NET Core controller route
    /// (e.g. <c>[HttpPost("$default")]</c>, <c>[HttpPost("$connect")]</c>) for the ASP.NET Core IServer
    /// to successfully dispatch requests.
    /// </summary>
    public abstract class APIGatewayWebsocketApiProxyFunction : APIGatewayProxyFunction
    {
        /// <summary>
        /// Default constructor. The ASP.NET Core framework is initialized as part of construction.
        /// </summary>
        protected APIGatewayWebsocketApiProxyFunction()
            : base()
        {
        }

        /// <summary>
        /// Constructor that lets the caller defer ASP.NET Core framework initialization until the first request.
        /// </summary>
        /// <param name="startupMode">Configures when the ASP.NET Core framework is initialized.</param>
        protected APIGatewayWebsocketApiProxyFunction(StartupMode startupMode)
            : base(startupMode)
        {
        }

        /// <summary>
        /// Constructor used by Amazon.Lambda.AspNetCoreServer.Hosting to support ASP.NET Core projects using the Minimal API style.
        /// </summary>
        /// <param name="hostedServices">The service provider built by the ASP.NET Core host.</param>
        protected APIGatewayWebsocketApiProxyFunction(IServiceProvider hostedServices)
            : base(hostedServices)
        {
        }

        /// <summary>
        /// Maps the websocket event to a request path of <c>/{RouteKey}</c> so ASP.NET Core can dispatch to a controller
        /// route declared with <c>[HttpPost("{RouteKey}")]</c>.
        /// </summary>
        protected override string ParseHttpPath(APIGatewayProxyRequest apiGatewayRequest)
        {
            return "/" + Utilities.DecodeResourcePath(apiGatewayRequest.RequestContext.RouteKey);
        }

        /// <summary>
        /// Always returns <c>POST</c> for websocket events. Combined with <see cref="ParseHttpPath"/>, this lets the same
        /// Lambda route every websocket event into an ASP.NET Core controller action.
        /// </summary>
        protected override string ParseHttpMethod(APIGatewayProxyRequest apiGatewayRequest)
        {
            return "POST";
        }

        /// <summary>
        /// Adds a default <c>Content-Type</c> of <c>application/json</c> when API Gateway did not supply one.
        /// Websocket message payloads are typically JSON, but the gateway does not set headers automatically.
        /// </summary>
        protected override IHeaderDictionary AddMissingRequestHeaders(APIGatewayProxyRequest apiGatewayRequest, IHeaderDictionary headers)
        {
            headers = base.AddMissingRequestHeaders(apiGatewayRequest, headers);
            if (!headers.ContainsKey("Content-Type"))
            {
                headers["Content-Type"] = "application/json";
            }
            return headers;
        }
    }
}
