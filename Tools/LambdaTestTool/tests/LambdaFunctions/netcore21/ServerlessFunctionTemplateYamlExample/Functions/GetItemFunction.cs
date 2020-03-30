using System;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetServerless.Lambda.Functions
{
    public class GetItemFunction
    {
        private readonly IServiceProvider _serviceProvider;

        public GetItemFunction() : this(Startup
          .BuildContainer()
        .BuildServiceProvider())
        {
        }

        public GetItemFunction(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public Task<APIGatewayProxyResponse> Run(APIGatewayProxyRequest request)
        {
            return Task.FromResult(new APIGatewayProxyResponse());

        }
    }
}
