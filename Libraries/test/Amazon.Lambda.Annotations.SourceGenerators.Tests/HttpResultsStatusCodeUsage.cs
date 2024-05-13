using System.Net;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    public class HttpResultsStatusCodeUsage
    {
        [Fact]
        public void UsageOfIHttpResultStatusCode()
        {
            var sut = new Functions();

            var result = sut.GetResponse("good", null);
            
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            result = sut.GetResponse("not good", null);
            
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
    }
    
    public class Functions
    {
        [LambdaFunction]
        [HttpApi(LambdaHttpMethod.Get, "/resource/{type}")]
        public IHttpResult GetResponse(string type, ILambdaContext context)
        {
            return type == "good" ?
                HttpResults.Ok() :
                HttpResults.BadRequest();
        }
    }
}
