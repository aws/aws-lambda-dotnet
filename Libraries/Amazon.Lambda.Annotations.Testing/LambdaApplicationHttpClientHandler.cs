using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using System.Net;
using System.Reflection;

namespace Amazon.Lambda.Annotations.Testing
{
    public class LambdaApplicationHttpClientHandler : HttpMessageHandler
    {
        private readonly ICollection<Assembly> _assemblies;
        private readonly ICollection<LambdaRouteConfig> _configs;

        public LambdaApplicationHttpClientHandler(ICollection<LambdaRouteConfig> configs, ICollection<Assembly> assemblies)
        {
            _configs = configs;
            _assemblies = assemblies;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Dictionary<string, string> pathParameters = null;
            var lambda = _configs.FirstOrDefault(c => c.Match(request.Method, request.RequestUri, out pathParameters));

            if (lambda == null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var lambdaRequest = await ToLambdaRequest(request, pathParameters, lambda.PayloadFormat);
            var lambdaResponse = await InvokeLambda(lambda, lambdaRequest);
            var httpResponse = ToHttpResponse(lambdaResponse, request);

            return httpResponse;
        }

        private async Task<object> InvokeLambda(LambdaRouteConfig config, object request)
        {
            var assembly = _assemblies.FirstOrDefault(a => a.GetName().Name == config.AssemblyName)
                ?? throw new InvalidOperationException($"Assembly '{config.AssemblyName}' is not found.");

            var type = assembly.GetType(config.TypeName)
                ?? throw new InvalidOperationException($"Type '{config.TypeName}' is not found.");

            var method = type.GetMethod(config.MethodName)
                ?? throw new InvalidOperationException($"Method '{config.MethodName}' is not found.");

            var lambdaInstance = Activator.CreateInstance(type);
            var lambdaContext = new TestLambdaContext();
            var lambdaResult = method.Invoke(lambdaInstance, new object[] { request, lambdaContext });

            if (lambdaResult is Task taskResult)
            {
                await taskResult;
                return ((dynamic)taskResult).Result;
            }
            else
            {
                return lambdaResult;
            }
        }

        private static async Task<object> ToLambdaRequest(HttpRequestMessage request,
            Dictionary<string, string> pathParameters,
            PayloadFormat payloadFormat)
        {
            return payloadFormat switch
            {
                PayloadFormat.HttpApiV2 => await APIGatewayRequests.ToHttpApiV2Request(request, pathParameters),
                PayloadFormat.RestApi => await APIGatewayRequests.ToRestApiRequest(request, pathParameters),
                _ => throw new NotSupportedException()
            };
        }

        private static HttpResponseMessage ToHttpResponse(object lambdaResponse, HttpRequestMessage request)
        {
            return lambdaResponse switch
            {
                APIGatewayProxyResponse restApiResponse => APIGatewayRequests.ToHttpResponse(restApiResponse, request),
                APIGatewayHttpApiV2ProxyResponse httpApiV2Response => APIGatewayRequests.ToHttpResponse(httpApiV2Response, request),
                _ => throw new NotSupportedException()
            };
        }
    }
}
