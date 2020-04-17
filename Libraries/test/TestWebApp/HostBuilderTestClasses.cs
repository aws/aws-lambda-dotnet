using Amazon.Lambda.AspNetCoreServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

#if !NETCOREAPP_2_1
namespace TestWebApp
{
    public interface IMethodsCalled
    {
        bool InitHostBuilder { get; set; }
        bool InitHostWebBuilder { get; set; }
    }

    public class HostBuilderUsingGenericClass : APIGatewayProxyFunction<Startup>, IMethodsCalled
    {
        public bool InitHostBuilder { get; set; }
        public bool InitHostWebBuilder { get; set; }

        protected override void Init(IWebHostBuilder builder)
        {
            base.Init(builder);
            InitHostWebBuilder = true;
        }

        protected override void Init(IHostBuilder builder)
        {
            base.Init(builder);
            InitHostBuilder = true;
        }
    }

    public class HostBuilderOverridingInit : APIGatewayProxyFunction, IMethodsCalled
    {
        public bool InitHostBuilder { get; set; }
        public bool InitHostWebBuilder { get; set; }

        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<Startup>();

            InitHostWebBuilder = true;
        }

        protected override void Init(IHostBuilder builder)
        {
            base.Init(builder);
            InitHostBuilder = true;
        }
    }

    public class HostBuilderOverridingCreateWebHostBuilder : APIGatewayProxyFunction, IMethodsCalled
    {
        public bool InitHostBuilder { get; set; }
        public bool InitHostWebBuilder { get; set; }

        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            return base.CreateWebHostBuilder();
        }

        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<Startup>();
            InitHostWebBuilder = true;
        }

        protected override void Init(IHostBuilder builder)
        {
            base.Init(builder);
            InitHostBuilder = true;
        }
    }

    public class HostBuilderOverridingCreateHostBuilder : APIGatewayProxyFunction, IMethodsCalled
    {
        public bool InitHostBuilder { get; set; }
        public bool InitHostWebBuilder { get; set; }

        protected override IHostBuilder CreateHostBuilder()
        {
            return base.CreateHostBuilder();
        }

        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<Startup>();
            InitHostWebBuilder = true;
        }

        protected override void Init(IHostBuilder builder)
        {
            base.Init(builder);
            InitHostBuilder = true;
        }
    }
}
#endif