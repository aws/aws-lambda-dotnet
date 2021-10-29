using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class Greeter_SayHelloAsync_Generated
    {
        private readonly Greeter greeter;

        public Greeter_SayHelloAsync_Generated()
        {
            greeter = new Greeter();
        }

        public async Task<APIGatewayProxyResponse> SayHelloAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var firstNames = default(System.Collections.Generic.IEnumerable<string>);
            if (request.Headers?.ContainsKey("names") == true)
            {
                firstNames = request.Headers["names"].Split(",").Select(q => (string)Convert.ChangeType(q, typeof(string))).ToList();
            }

            await greeter.SayHelloAsync(firstNames);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200
            };
        }
    }
}