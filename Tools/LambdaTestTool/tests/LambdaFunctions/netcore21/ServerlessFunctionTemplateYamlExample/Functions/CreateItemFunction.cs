using System;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetServerless.Lambda.Functions
{
    public class CreateItemFunction
    {
        private readonly IServiceProvider _serviceProvider;

        public CreateItemFunction() : this(Startup
          .BuildContainer()
          .BuildServiceProvider())
        {
        }

        public CreateItemFunction(IServiceProvider serviceProvider)
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
