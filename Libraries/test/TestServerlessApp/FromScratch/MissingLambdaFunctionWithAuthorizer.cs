using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace TestServerlessApp.FromScratch
{
    public class MissingLambdaFunctionWithAuthorizer
    {
        [HttpApiAuthorizer(
            Name = "MyAuthorizer",
            AuthorizerPayloadFormatVersion = AuthorizerPayloadFormatVersion.V2)]
        public APIGatewayCustomAuthorizerV2SimpleResponse Authorize(
            APIGatewayCustomAuthorizerV2Request request,
            ILambdaContext context)
        {
            return new APIGatewayCustomAuthorizerV2SimpleResponse
            {
                IsAuthorized = true
            };
        }
    }
}