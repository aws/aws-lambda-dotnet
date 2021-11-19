using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using TestWebApp;

using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test
{

    public class TestWhichBuilderIsUsed
    {
        [Theory]
        [InlineData(typeof(HostBuilderUsingGenericClass), true)]
        [InlineData(typeof(HostBuilderOverridingInit), true)]
        [InlineData(typeof(HostBuilderOverridingCreateWebHostBuilder), false)]
        [InlineData(typeof(HostBuilderOverridingCreateHostBuilder), true)]
        [InlineData(typeof(HostBuilderOverridingInitHostBuilderAndCallsConfigureWebHostDefaults), true)]
        [InlineData(typeof(HostBuilderOverridingInitHostBuilderAndCallsConfigureWebHostLambdaDefaults), true)]
        public async Task TestUsingGenericBaseClass(Type functionType, bool initHostCalled)
        {
            var methodsCalled = await InvokeAPIGatewayRequestWithContent(functionType);
            Assert.Equal(initHostCalled, methodsCalled.InitHostBuilder);

            Assert.True(methodsCalled.InitHostWebBuilder);
        }

        private async Task<IMethodsCalled> InvokeAPIGatewayRequestWithContent(Type functionType)
        {
            var context = new TestLambdaContext();

            var filePath = Path.Combine(Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location), "values-get-all-apigateway-request.json");
            var requestContent = File.ReadAllText(filePath);

            var lambdaFunction = Activator.CreateInstance(functionType) as APIGatewayProxyFunction;
            var requestStream = new MemoryStream(System.Text.UTF8Encoding.UTF8.GetBytes(requestContent));
            var request = new Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer().Deserialize<APIGatewayProxyRequest>(requestStream);
            var response = await lambdaFunction.FunctionHandlerAsync(request, context);
            Assert.Equal(200, response.StatusCode);
            return lambdaFunction as IMethodsCalled;
        }
    }
}
