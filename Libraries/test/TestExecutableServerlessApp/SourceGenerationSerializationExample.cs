using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TestExecutableServerlessApp
{
    public class SourceGenerationSerializationExample
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/")]
        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.SourceGeneratorLambdaJsonSerializer<HttpApiJsonSerializerContext>))]
        public IHttpResult GetPerson(ILambdaContext context)
        {
            return HttpResults.Ok(new Person { Name = "Foobar" });
        }
    }

    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
    [JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
    [JsonSerializable(typeof(Person))]
    public partial class HttpApiJsonSerializerContext : JsonSerializerContext
    {

    }

    public class Person
    {
        public string Name { get; set; }
    }
}
