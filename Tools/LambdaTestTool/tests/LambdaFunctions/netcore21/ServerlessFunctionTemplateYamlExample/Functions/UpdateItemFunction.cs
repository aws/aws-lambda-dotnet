using System;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetServerless.Lambda.Functions
{
    public class UpdateItemFunction
    {
        private readonly IServiceProvider _serviceProvider;

        public UpdateItemFunction() : this(Startup
          .BuildContainer()
        .BuildServiceProvider())
        {
        }

        public UpdateItemFunction(IServiceProvider serviceProvider)
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
