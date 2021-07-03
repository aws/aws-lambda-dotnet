using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.S3Events;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

using Moq;
using Moq.Protected;


namespace BlueprintBaseName._1.Tests.Tests
{
    public class FunctionTest
    {
        [Fact]
        public async Task TestS3EventLambdaFunction()
        {
            // Setup the S3 event object that S3 notifications would create with the fields used by the Lambda function.
            var s3Event = new S3ObjectLambdaEvent
            {
                GetObjectContext = new S3ObjectLambdaEvent.GetObjectContextType
                {
                    InputS3Url = "http://fakeendpoint.com/test.txt",
                    OutputRoute = "the-route",
                    OutputToken = "the-token"
                }
            };

            Mock<IAmazonS3> s3ClientMock = new Mock<IAmazonS3>();

            s3ClientMock.Setup(client => client.WriteGetObjectResponseAsync(It.IsAny<WriteGetObjectResponseRequest>(), It.IsAny<CancellationToken>()))
                .Callback<WriteGetObjectResponseRequest, CancellationToken>((request, token) =>
                {
                    Assert.Equal(s3Event.GetObjectContext.OutputRoute, request.RequestRoute);
                    Assert.Equal(s3Event.GetObjectContext.OutputToken, request.RequestToken);

                    var transformedContent = new StreamReader(request.Body).ReadToEnd();
                    Assert.Equal("HELLO WORLD", transformedContent);
                })
                .Returns((WriteGetObjectResponseRequest r, CancellationToken token) =>
                {
                    return Task.FromResult(new WriteGetObjectResponseResponse());
                });

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock.Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent("hello world"),
               });

            ILambdaContext lambdaContext = new TestLambdaContext();

            // Invoke the lambda function and confirm the content type was returned.
            var function = new Function(s3ClientMock.Object, new HttpClient(handlerMock.Object));
            await function.FunctionHandler(s3Event, lambdaContext);

            s3ClientMock.Verify(mock => mock.WriteGetObjectResponseAsync(It.IsAny<WriteGetObjectResponseRequest>(), It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}
