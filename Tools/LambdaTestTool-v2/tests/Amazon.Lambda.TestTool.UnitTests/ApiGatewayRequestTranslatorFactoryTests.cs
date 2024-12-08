using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.UnitTests
{
    using System;
    using Xunit;
    using Microsoft.Extensions.DependencyInjection;
    using Amazon.Lambda.TestTool;

    public class ApiGatewayRequestTranslatorFactoryTests
    {
        [Fact]
        public void Create_WithRESTMode_ReturnsApiGatewayProxyRequestTranslator()
        {
            var serviceProvider = CreateMockServiceProvider();
            var factory = new ApiGatewayRequestTranslatorFactory(serviceProvider);

            var result = factory.Create(ApiGatewayMode.REST);

            Assert.IsType<ApiGatewayProxyRequestTranslator>(result);
        }

        [Fact]
        public void Create_WithHTTPV1Mode_ReturnsApiGatewayProxyRequestTranslator()
        {
            var serviceProvider = CreateMockServiceProvider();
            var factory = new ApiGatewayRequestTranslatorFactory(serviceProvider);

            var result = factory.Create(ApiGatewayMode.HTTPV1);

            Assert.IsType<ApiGatewayProxyRequestTranslator>(result);
        }

        [Fact]
        public void Create_WithHTTPV2Mode_ReturnsApiGatewayHttpApiV2ProxyRequestTranslator()
        {
            var serviceProvider = CreateMockServiceProvider();
            var factory = new ApiGatewayRequestTranslatorFactory(serviceProvider);

            var result = factory.Create(ApiGatewayMode.HTTPV2);

            Assert.IsType<ApiGatewayHttpApiV2ProxyRequestTranslator>(result);
        }

        private IServiceProvider CreateMockServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddTransient<IHttpRequestUtility, HttpRequestUtility>();
            services.AddTransient<ApiGatewayProxyRequestTranslator>();
            services.AddTransient<ApiGatewayHttpApiV2ProxyRequestTranslator>();
            return services.BuildServiceProvider();
        }
    }

}
