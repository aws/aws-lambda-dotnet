using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting.Internal;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// Implements the ASP.NET Core IServer interface and exposes the application object for the Lambda function
    /// to initiate a web request.
    /// </summary>
    internal class APIGatewayServer : IServer
    {
        /// <summary>
        /// The application is used by the Lambda function to initiate a web request through the ASP.NET Core framework.
        /// </summary>
        public IHttpApplication<HostingApplication.Context> Application { get; set; }
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose()
        {
        }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
            this.Application = application as IHttpApplication<HostingApplication.Context>;
        }
    }
}
