using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    /// <summary>
    /// Implements the ASP.NET Core IServer interface and exposes the application object for the Lambda function
    /// to initiate a web request.
    /// </summary>
    public class LambdaServer : IServer
    {
        /// <summary>
        /// The application is used by the Lambda function to initiate a web request through the ASP.NET Core framework.
        /// </summary>
        public ApplicationWrapper Application { get; set; }
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose()
        {
        }

        public virtual Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            this.Application = new ApplicationWrapper<TContext>(application);
            return Task.CompletedTask;
        }

        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public abstract class ApplicationWrapper
        {
            internal abstract object CreateContext(IFeatureCollection features);

            internal abstract Task ProcessRequestAsync(object context);

            internal abstract void DisposeContext(object context, Exception exception);
        }

        public class ApplicationWrapper<TContext> : ApplicationWrapper, IHttpApplication<TContext>
        {
            private readonly IHttpApplication<TContext> _application;

            public ApplicationWrapper(IHttpApplication<TContext> application)
            {
                _application = application;
            }

            internal override object CreateContext(IFeatureCollection features)
            {
                return ((IHttpApplication<TContext>)this).CreateContext(features);
            }

            TContext IHttpApplication<TContext>.CreateContext(IFeatureCollection features)
            {
                return _application.CreateContext(features);
            }

            internal override void DisposeContext(object context, Exception exception)
            {
                ((IHttpApplication<TContext>)this).DisposeContext((TContext)context, exception);
            }

            void IHttpApplication<TContext>.DisposeContext(TContext context, Exception exception)
            {
                _application.DisposeContext(context, exception);
            }

            internal override Task ProcessRequestAsync(object context)
            {
                return ((IHttpApplication<TContext>)this).ProcessRequestAsync((TContext)context);
            }

            Task IHttpApplication<TContext>.ProcessRequestAsync(TContext context)
            {
                return _application.ProcessRequestAsync(context);
            }
        }
    }
}
