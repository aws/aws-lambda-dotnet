
#pragma warning disable 618
namespace Amazon.Lambda.Tests;

using Amazon.Lambda.AppSyncEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

public class AppSyncEventTests
{
    // This utility method takes care of removing the BOM that System.Text.Json doesn't like.
    public MemoryStream LoadJsonTestFile(string filename)
    {
        var json = File.ReadAllText(filename);
        return new MemoryStream(UTF8Encoding.UTF8.GetBytes(json));
    }

    public string SerializeJson<T>(ILambdaSerializer serializer, T response)
    {
        string serializedJson;
        using (MemoryStream stream = new MemoryStream())
        {
            serializer.Serialize(response, stream);

            stream.Position = 0;
            serializedJson = Encoding.UTF8.GetString(stream.ToArray());
        }
        return serializedJson;
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTest(Type serializerType)
    {
        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        using (var fileStream = LoadJsonTestFile("appsync-event.json"))
        {
            var appSyncEvent = serializer.Deserialize<AppSyncResolverEvent<Dictionary<string, object>>>(fileStream);
            Assert.NotNull(appSyncEvent);
            Assert.NotNull(appSyncEvent.Arguments);
            Assert.NotNull(appSyncEvent.Arguments["input"]);

            Assert.NotNull(appSyncEvent.Request);
            Assert.NotNull(appSyncEvent.Request.Headers);
            var headers = appSyncEvent.Request.Headers;
            Assert.Equal("value1", headers["key1"]);
            Assert.Equal("value2", headers["key2"]);

            Assert.NotNull(appSyncEvent.Info);
            Assert.Equal("openSupportTicket", appSyncEvent.Info.FieldName);
            Assert.Equal("Mutation", appSyncEvent.Info.ParentTypeName);

            Assert.NotNull(appSyncEvent.Info.SelectionSetList);
            Assert.Equal(6, appSyncEvent.Info.SelectionSetList.Count);
            Assert.Contains("ticketId", appSyncEvent.Info.SelectionSetList);
            Assert.Contains("status", appSyncEvent.Info.SelectionSetList);
            Assert.Contains("title", appSyncEvent.Info.SelectionSetList);
            Assert.Contains("description", appSyncEvent.Info.SelectionSetList);
            Assert.Contains("createdAt", appSyncEvent.Info.SelectionSetList);
            Assert.Contains("updatedAt", appSyncEvent.Info.SelectionSetList);

            Assert.NotNull(appSyncEvent.Info.SelectionSetGraphQL);
            Assert.NotNull(appSyncEvent.Info.Variables);
            Assert.NotNull(appSyncEvent.Info.Variables["input"]);

            Assert.NotNull(appSyncEvent.Stash);
            Assert.Empty(appSyncEvent.Stash);
        }
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTestCognitoAuthorizer(Type serializerType)
    {
        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        using (var fileStream = LoadJsonTestFile("appsync-event-cognito-authorizer.json"))
        {
            var request = serializer.Deserialize<AppSyncResolverEvent<Dictionary<string, object>>>(fileStream);

            Assert.NotNull(request.Identity);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Identity.ToString())))
            {
                var identity = serializer.Deserialize<AppSyncCognitoIdentity>(stream);
                Assert.NotNull(identity);

                // Claims
                Assert.NotNull(identity.Claims);
                Assert.True(identity.Claims.ContainsKey("client_id"));
                Assert.True(identity.Claims.ContainsKey("scope"));
                Assert.True(identity.Claims.ContainsKey("sub"));
                Assert.True(identity.Claims.ContainsKey("token_use"));

                // DefaultAuthStrategy
                Assert.NotEmpty(identity.DefaultAuthStrategy);

                // Groups
                Assert.NotNull(identity.Groups);
                Assert.NotEmpty(identity.Groups);

                // Issuer
                Assert.NotEmpty(identity.Issuer);

                // SourceIp
                Assert.NotNull(identity.SourceIp);
                Assert.NotEmpty(identity.SourceIp);

                // Sub
                Assert.NotEmpty(identity.Sub);

                // Username
                Assert.NotEmpty(identity.Username);
            }
        }
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTestIAMAuthorizer(Type serializerType)
    {
        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        using (var fileStream = LoadJsonTestFile("appsync-event-iam-authorizer.json"))
        {
            var request = serializer.Deserialize<AppSyncResolverEvent<Dictionary<string, object>>>(fileStream);

            Assert.NotNull(request.Identity);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Identity.ToString())))
            {
                var identity = serializer.Deserialize<AppSyncIamIdentity>(stream);
                Assert.NotNull(identity);

                // AccountId
                Assert.NotEmpty(identity.AccountId);

                // CognitoIdentityAuthProvider
                Assert.NotEmpty(identity.CognitoIdentityAuthProvider);

                // CognitoIdentityAuthType
                Assert.NotEmpty(identity.CognitoIdentityAuthType);

                // CognitoIdentityId
                Assert.NotEmpty(identity.CognitoIdentityId);

                // CognitoIdentityPoolId
                Assert.NotEmpty(identity.CognitoIdentityPoolId);

                // SourceIp
                Assert.NotNull(identity.SourceIp);
                Assert.NotEmpty(identity.SourceIp);

                // UserArn
                Assert.NotEmpty(identity.UserArn);

                // Username
                Assert.NotEmpty(identity.Username);
            }
        }
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTestLambdaAuthorizer(Type serializerType)
    {
        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        using (var fileStream = LoadJsonTestFile("appsync-event-lambda-authorizer.json"))
        {
            var request = serializer.Deserialize<AppSyncResolverEvent<Dictionary<string, object>>>(fileStream);

            Assert.NotNull(request.Identity);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Identity.ToString())))
            {
                var identity = serializer.Deserialize<AppSyncLambdaIdentity>(stream);
                Assert.NotNull(identity);

                // ResolverContext
                Assert.NotNull(identity.ResolverContext);
                Assert.NotEmpty(identity.ResolverContext["userid"]);
                Assert.NotEmpty(identity.ResolverContext["info"]);
                Assert.NotEmpty(identity.ResolverContext["more_info"]);
            }
        }
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTestOidcAuthorizer(Type serializerType)
    {
        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        using (var fileStream = LoadJsonTestFile("appsync-event-oidc-authorizer.json"))
        {
            var request = serializer.Deserialize<AppSyncResolverEvent<Dictionary<string, object>>>(fileStream);

            Assert.NotNull(request.Identity);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.Identity.ToString())))
            {
                var identity = serializer.Deserialize<AppSyncOidcIdentity>(stream);
                Assert.NotNull(identity);

                // Claims
                Assert.NotNull(identity.Claims);
                Assert.True(identity.Claims.ContainsKey("client_id"));

                // Issuer
                Assert.NotEmpty(identity.Issuer);

                // Sub
                Assert.NotEmpty(identity.Sub);
            }
        }
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTestLambdaAuthorizerRequestEvent(Type serializerType)
    {
        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        using (var fileStream = LoadJsonTestFile("appsync-event-lambda-authorizer-request.json"))
        {
            var request = serializer.Deserialize<AppSyncAuthorizerEvent>(fileStream);

            // Assert Authorization Token
            Assert.Equal("custom-token", request.AuthorizationToken);

            // Assert Request Context
            Assert.NotNull(request.RequestContext);
            Assert.Equal("xxxxxxxx", request.RequestContext.ApiId);
            Assert.Equal("112233445566", request.RequestContext.AccountId);
            Assert.Equal("36307622-97fe-4dfa-bd71-b15b1d03ce97", request.RequestContext.RequestId);
            Assert.Equal("MyQuery", request.RequestContext.OperationName);
            Assert.NotNull(request.RequestContext.Variables);
            Assert.Empty(request.RequestContext.Variables);
            Assert.Contains("listTodos", request.RequestContext.QueryString);

            // Assert Request Headers
            Assert.NotNull(request.RequestHeaders);
            Assert.Equal("This is test token", request.RequestHeaders["authorization"]);
            Assert.Equal("application/json", request.RequestHeaders["content-type"]);
            Assert.Equal("https://ap-south-1.console.aws.amazon.com", request.RequestHeaders["origin"]);
        }
    }

    [Theory]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]
    [InlineData(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
    public void AppSyncTestLambdaAuthorizerResponseEvent(Type serializerType)
    {
        var response = new AppSyncAuthorizerResult
        {
            IsAuthorized = true,
            ResolverContext = new Dictionary<string, string>
                {
                    { "userid", "test-user-id" },
                    { "info", "contextual information A" },
                    { "more_info", "contextual information B" }
                },
            DeniedFields = new List<string>
                {
                    "arn:aws:appsync:us-east-1:1234567890:apis/xxxxxx/types/Event/fields/comments",
                    "Mutation.createEvent"
                },
            TtlOverride = 10
        };

        var serializer = Activator.CreateInstance(serializerType) as ILambdaSerializer;
        var json = SerializeJson(serializer, response);
        var actualObject = JObject.Parse(json);
        var expectedJObject = JObject.Parse(File.ReadAllText("appsync-event-lambda-authorizer-response.json"));

        Assert.True(JToken.DeepEquals(actualObject, expectedJObject));
    }
}

#pragma warning restore 618
