using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using System.Threading.Tasks;

namespace TestServerlessApp
{
    public class CustomizeResponseExamples
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/okresponsewithheader/{x}")]
        public IHttpResult OkResponseWithHeader(int x, ILambdaContext context)
        {
            return HttpResults.Ok("All Good")
                                .AddHeader("Single-Header", "Value")
                                .AddHeader("Multi-Header", "Foo")
                                .AddHeader("Multi-Header", "Bar");
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/okresponsewithheaderasync/{x}")]
        public Task<IHttpResult> OkResponseWithHeaderAsync(int x, ILambdaContext context)
        {
            return Task.FromResult(HttpResults.Ok("All Good")
                                .AddHeader("Single-Header", "Value")
                                .AddHeader("Multi-Header", "Foo")
                                .AddHeader("Multi-Header", "Bar"));
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/notfoundwithheaderv2/{x}")]
        public IHttpResult NotFoundResponseWithHeaderV2(int x, ILambdaContext context)
        {
            return HttpResults.NotFound("Not Found")
                                .AddHeader("Single-Header", "Value")
                                .AddHeader("Multi-Header", "Foo")
                                .AddHeader("Multi-Header", "Bar");
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/notfoundwithheaderv2async/{x}")]
        public Task<IHttpResult> NotFoundResponseWithHeaderV2Async(int x, ILambdaContext context)
        {
            return Task.FromResult(HttpResults.NotFound("Not Found")
                                .AddHeader("Single-Header", "Value")
                                .AddHeader("Multi-Header", "Foo")
                                .AddHeader("Multi-Header", "Bar"));
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/notfoundwithheaderv1/{x}", Version = HttpApiVersion.V1)]
        public IHttpResult NotFoundResponseWithHeaderV1(int x, ILambdaContext context)
        {
            return HttpResults.NotFound("Not Found")
                                .AddHeader("Single-Header", "Value")
                                .AddHeader("Multi-Header", "Foo")
                                .AddHeader("Multi-Header", "Bar");
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Get, "/notfoundwithheaderv1async/{x}", Version = HttpApiVersion.V1)]
        public Task<IHttpResult> NotFoundResponseWithHeaderV1Async(int x, ILambdaContext context)
        {
            return Task.FromResult(HttpResults.NotFound("Not Found")
                                .AddHeader("Single-Header", "Value")
                                .AddHeader("Multi-Header", "Foo")
                                .AddHeader("Multi-Header", "Bar"));
        }
    }
}
