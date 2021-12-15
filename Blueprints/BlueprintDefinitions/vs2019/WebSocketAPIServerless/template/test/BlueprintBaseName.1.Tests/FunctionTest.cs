using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.APIGatewayEvents;

using Moq;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;


namespace BlueprintBaseName._1.Tests
{
    public class FunctionTest
    {
        public FunctionTest()
        {
        }

        [Fact]
        public async Task TestConnect()
        {
            Mock<IAmazonDynamoDB> _mockDDBClient = new Mock<IAmazonDynamoDB>();
            Mock<IAmazonApiGatewayManagementApi> _mockApiGatewayClient = new Mock<IAmazonApiGatewayManagementApi>();
            string tableName = "mocktable";
            string connectionId = "test-id";

            _mockDDBClient.Setup(client => client.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PutItemRequest, CancellationToken>((request, token) =>
                {
                    Assert.Equal(tableName, request.TableName);
                    Assert.Equal(connectionId, request.Item[Functions.ConnectionIdField].S);
                });

            var functions = new Functions(_mockDDBClient.Object, (endpoint) => _mockApiGatewayClient.Object, tableName);

            var lambdaContext = new TestLambdaContext();

            var request = new APIGatewayProxyRequest
            {
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                {
                    ConnectionId = connectionId
                }
            };
            var response = await functions.OnConnectHandler(request, lambdaContext);
            Assert.Equal(200, response.StatusCode);
        }


        [Fact]
        public async Task TestDisconnect()
        {
            Mock<IAmazonDynamoDB> _mockDDBClient = new Mock<IAmazonDynamoDB>();
            Mock<IAmazonApiGatewayManagementApi> _mockApiGatewayClient = new Mock<IAmazonApiGatewayManagementApi>();
            string tableName = "mocktable";
            string connectionId = "test-id";

            _mockDDBClient.Setup(client => client.DeleteItemAsync(It.IsAny<DeleteItemRequest>(), It.IsAny<CancellationToken>()))
                .Callback<DeleteItemRequest, CancellationToken>((request, token) =>
                {
                    Assert.Equal(tableName, request.TableName);
                    Assert.Equal(connectionId, request.Key[Functions.ConnectionIdField].S);
                });

            var functions = new Functions(_mockDDBClient.Object, (endpoint) => _mockApiGatewayClient.Object, tableName);

            var lambdaContext = new TestLambdaContext();

            var request = new APIGatewayProxyRequest
            {
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                {
                    ConnectionId = connectionId
                }
            };
            var response = await functions.OnDisconnectHandler(request, lambdaContext);
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task TestSendMessage()
        {
            Mock<IAmazonDynamoDB> _mockDDBClient = new Mock<IAmazonDynamoDB>();
            Mock<IAmazonApiGatewayManagementApi> _mockApiGatewayClient = new Mock<IAmazonApiGatewayManagementApi>();
            string tableName = "mocktable";
            string connectionId = "test-id";
            string message = "hello world";

            _mockDDBClient.Setup(client => client.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
                .Callback<ScanRequest, CancellationToken>((request, token) =>
                {
                    Assert.Equal(tableName, request.TableName);
                    Assert.Equal(Functions.ConnectionIdField, request.ProjectionExpression);
                })
                .Returns((ScanRequest r, CancellationToken token) =>
                {
                    return Task.FromResult(new ScanResponse
                    {
                        Items = new List<Dictionary<string, AttributeValue>>
                        {
                            { new Dictionary<string, AttributeValue>{ {Functions.ConnectionIdField, new AttributeValue { S = connectionId } } } }
                        }
                    });
                });

            Func<string, IAmazonApiGatewayManagementApi> apiGatewayFactory = ((endpoint) =>
            {
                Assert.Equal("https://test-domain/test-stage", endpoint);
                return _mockApiGatewayClient.Object;
            });

            _mockApiGatewayClient.Setup(client => client.PostToConnectionAsync(It.IsAny<PostToConnectionRequest>(), It.IsAny<CancellationToken>()))
                .Callback<PostToConnectionRequest, CancellationToken>((request, token) =>
                {
                    var actualMessage = new StreamReader(request.Data).ReadToEnd();
                    Assert.Equal(message, actualMessage);
                });

            var functions = new Functions(_mockDDBClient.Object, apiGatewayFactory, tableName);

            var lambdaContext = new TestLambdaContext();

            var request = new APIGatewayProxyRequest
            {
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                {
                    ConnectionId = connectionId,
                    DomainName = "test-domain",
                    Stage = "test-stage"
                },
                Body = "{\"message\":\"sendmessage\", \"data\":\"" + message + "\"}"
            };
            var response = await functions.SendMessageHandler(request, lambdaContext);
            Assert.Equal(200, response.StatusCode);
        }
    }
}
