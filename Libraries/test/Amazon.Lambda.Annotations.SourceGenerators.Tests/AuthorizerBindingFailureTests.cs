// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Amazon.Lambda.Annotations.SourceGenerators.Tests
{
    /// <summary>
    /// Runs the source generator over authorizers with value typed parameters, compiles the generated handlers and
    /// invokes them. A client supplied value that fails to convert must deny the request without invoking the user's
    /// authorizer method, otherwise the user's method would evaluate default(T) in place of the client supplied value.
    /// </summary>
    public class AuthorizerBindingFailureTests
    {
        private const string UserSource = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestApp
{
    // Every authorizer allows the request so a fail open binding failure is observable as an allow.
    public class Authorizers
    {
        public static List<string> Invocations = new List<string>();

        [LambdaFunction]
        [HttpApiAuthorizer(EnableSimpleResponses = true)]
        public IAuthorizerResult HttpApiSimple([FromHeader(Name = ""X-Org-Id"")] long orgId, ILambdaContext context)
        {
            Invocations.Add(nameof(HttpApiSimple) + "":"" + orgId);
            return AuthorizerResults.Allow();
        }

        [LambdaFunction]
        [HttpApiAuthorizer(EnableSimpleResponses = false)]
        public async Task<IAuthorizerResult> HttpApiIamPolicy([FromQuery] bool admin, ILambdaContext context)
        {
            await Task.Yield();
            Invocations.Add(nameof(HttpApiIamPolicy) + "":"" + admin);
            return AuthorizerResults.Allow();
        }

        [LambdaFunction]
        [RestApiAuthorizer(Type = RestApiAuthorizerType.Token)]
        public IAuthorizerResult RestApiToken([FromHeader(Name = ""Authorization"")] int token, ILambdaContext context)
        {
            Invocations.Add(nameof(RestApiToken) + "":"" + token);
            return AuthorizerResults.Allow();
        }

        [LambdaFunction]
        [RestApiAuthorizer(Type = RestApiAuthorizerType.Request)]
        public APIGatewayCustomAuthorizerResponse RestApiRequestRaw([FromRoute] int tenantId, APIGatewayCustomAuthorizerRequest request, ILambdaContext context)
        {
            Invocations.Add(nameof(RestApiRequestRaw) + "":"" + tenantId);
            return Allow(request.MethodArn);
        }

        [LambdaFunction]
        [HttpApiAuthorizer(EnableSimpleResponses = true)]
        public async Task<APIGatewayCustomAuthorizerV2SimpleResponse> HttpApiSimpleRaw([FromHeader(Name = ""X-Org-Id"")] int orgId, ILambdaContext context)
        {
            await Task.Yield();
            Invocations.Add(nameof(HttpApiSimpleRaw) + "":"" + orgId);
            return new APIGatewayCustomAuthorizerV2SimpleResponse { IsAuthorized = true };
        }

        [LambdaFunction]
        [HttpApiAuthorizer(EnableSimpleResponses = false)]
        public APIGatewayCustomAuthorizerV2IamResponse HttpApiIamRaw([FromHeader(Name = ""X-Org-Id"")] long orgId, [FromQuery] string name, APIGatewayCustomAuthorizerV2Request request, ILambdaContext context)
        {
            Invocations.Add(nameof(HttpApiIamRaw) + "":"" + orgId);
            return new APIGatewayCustomAuthorizerV2IamResponse { PolicyDocument = Allow(request.RouteArn).PolicyDocument };
        }

        [LambdaFunction]
        [HttpApiAuthorizer(EnableSimpleResponses = true)]
        public Dictionary<string, object> HttpApiCustomRaw([FromHeader(Name = ""X-Org-Id"")] long orgId, ILambdaContext context)
        {
            Invocations.Add(nameof(HttpApiCustomRaw) + "":"" + orgId);
            return new Dictionary<string, object> { { ""isAuthorized"", true } };
        }

        private static APIGatewayCustomAuthorizerResponse Allow(string arn)
        {
            return new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = ""user"",
                PolicyDocument = new APIGatewayCustomAuthorizerPolicy
                {
                    Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                    {
                        new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                        {
                            Effect = ""Allow"",
                            Action = new HashSet<string> { ""execute-api:Invoke"" },
                            Resource = new HashSet<string> { arn }
                        }
                    }
                }
            };
        }
    }
}
";

        private const string MethodArn = "arn:aws:execute-api:us-west-2:123456789012:abcdef/prod/GET/data";

        private static readonly Lazy<Assembly> GeneratedAssembly = new Lazy<Assembly>(CompileWithGenerator);

        public AuthorizerBindingFailureTests()
        {
            // Tests within a class run sequentially so the static invocation record can be reset per test.
            Invocations().Clear();
        }

        public static IEnumerable<object[]> MalformedValues => new[]
        {
            new object[] { "abc" },
            new object[] { "99999999999999999999" },
            new object[] { "" },
        };

        [Theory]
        [MemberData(nameof(MalformedValues))]
        public async Task HttpApiSimple_IAuthorizerResult_DeniesOnMalformedHeader(string value)
        {
            var request = new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                Headers = new Dictionary<string, string> { { "X-Org-Id", value } }
            };

            var response = await InvokeAsync("HttpApiSimple", request);

            Assert.False(ReadJson((Stream)response).RootElement.GetProperty("isAuthorized").GetBoolean());
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("HttpApiSimple:"));
        }

        [Fact]
        public async Task HttpApiIamPolicy_IAuthorizerResult_DeniesOnMalformedQuery()
        {
            var request = new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                QueryStringParameters = new Dictionary<string, string> { { "admin", "yes" } }
            };

            var response = await InvokeAsync("HttpApiIamPolicy", request);

            AssertDenyPolicyJson((Stream)response);
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("HttpApiIamPolicy:"));
        }

        [Fact]
        public async Task RestApiToken_IAuthorizerResult_DeniesOnMalformedToken()
        {
            var request = new APIGatewayCustomAuthorizerRequest
            {
                Type = "TOKEN",
                MethodArn = MethodArn,
                AuthorizationToken = "Bearer abc"
            };

            var response = await InvokeAsync("RestApiToken", request);

            AssertDenyPolicyJson((Stream)response);
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("RestApiToken:"));
        }

        [Fact]
        public async Task RestApiRequest_RawResponse_DeniesOnMalformedRoute()
        {
            var request = new APIGatewayCustomAuthorizerRequest
            {
                Type = "REQUEST",
                MethodArn = MethodArn,
                PathParameters = new Dictionary<string, string> { { "tenantId", "1.5" } }
            };

            var response = (APIGatewayCustomAuthorizerResponse)await InvokeAsync("RestApiRequestRaw", request);

            AssertDenyPolicy(response.PolicyDocument);
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("RestApiRequestRaw:"));
        }

        [Fact]
        public async Task HttpApiSimple_RawResponse_DeniesOnMalformedHeader()
        {
            var request = new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                Headers = new Dictionary<string, string> { { "x-org-id", "abc" } }
            };

            var response = (APIGatewayCustomAuthorizerV2SimpleResponse)await InvokeAsync("HttpApiSimpleRaw", request);

            Assert.False(response.IsAuthorized);
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("HttpApiSimpleRaw:"));
        }

        [Fact]
        public async Task HttpApiIam_RawResponse_DeniesOnMalformedHeader()
        {
            var request = new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                Headers = new Dictionary<string, string> { { "X-Org-Id", "abc" } }
            };

            var response = (APIGatewayCustomAuthorizerV2IamResponse)await InvokeAsync("HttpApiIamRaw", request);

            AssertDenyPolicy(response.PolicyDocument);
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("HttpApiIamRaw:"));
        }

        [Fact]
        public async Task CustomRawResponse_ThrowsUnauthorizedOnMalformedHeader()
        {
            var request = new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                Headers = new Dictionary<string, string> { { "X-Org-Id", "abc" } }
            };

            var exception = await Assert.ThrowsAsync<Exception>(() => InvokeAsync("HttpApiCustomRaw", request));

            Assert.Equal("Unauthorized", exception.Message);
            Assert.DoesNotContain(Invocations(), i => i.StartsWith("HttpApiCustomRaw:"));
        }

        [Fact]
        public async Task WellFormedValues_InvokeUserMethod()
        {
            var httpSimple = await InvokeAsync("HttpApiSimple", new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                Headers = new Dictionary<string, string> { { "X-Org-Id", "42" } }
            });
            Assert.True(ReadJson((Stream)httpSimple).RootElement.GetProperty("isAuthorized").GetBoolean());
            Assert.Contains("HttpApiSimple:42", Invocations());

            var restRaw = (APIGatewayCustomAuthorizerResponse)await InvokeAsync("RestApiRequestRaw", new APIGatewayCustomAuthorizerRequest
            {
                Type = "REQUEST",
                MethodArn = MethodArn,
                PathParameters = new Dictionary<string, string> { { "tenantId", "7" } }
            });
            Assert.Equal("Allow", restRaw.PolicyDocument.Statement.Single().Effect);
            Assert.Contains("RestApiRequestRaw:7", Invocations());

            // A string parameter cannot fail conversion so it never triggers the deny path.
            var httpIamRaw = (APIGatewayCustomAuthorizerV2IamResponse)await InvokeAsync("HttpApiIamRaw", new APIGatewayCustomAuthorizerV2Request
            {
                RouteArn = MethodArn,
                Headers = new Dictionary<string, string> { { "X-Org-Id", "5" } },
                QueryStringParameters = new Dictionary<string, string> { { "name", "abc" } }
            });
            Assert.Equal("Allow", httpIamRaw.PolicyDocument.Statement.Single().Effect);
            Assert.Contains("HttpApiIamRaw:5", Invocations());
        }

        private static void AssertDenyPolicy(APIGatewayCustomAuthorizerPolicy policy)
        {
            var statement = Assert.Single(policy.Statement);
            Assert.Equal("Deny", statement.Effect);
            Assert.Equal(new[] { "execute-api:Invoke" }, statement.Action);
            Assert.Equal(new[] { MethodArn }, statement.Resource);
        }

        private static void AssertDenyPolicyJson(Stream response)
        {
            var statement = ReadJson(response).RootElement.GetProperty("policyDocument").GetProperty("Statement").EnumerateArray().Single();
            Assert.Equal("Deny", statement.GetProperty("Effect").GetString());
            Assert.Equal(MethodArn, statement.GetProperty("Resource").GetString());
        }

        private static JsonDocument ReadJson(Stream stream)
        {
            return JsonDocument.Parse(stream);
        }

        private static List<string> Invocations()
        {
            var type = GeneratedAssembly.Value.GetType("TestApp.Authorizers", throwOnError: true);
            return (List<string>)type.GetField("Invocations").GetValue(null);
        }

        private static async Task<object> InvokeAsync(string methodName, object request)
        {
            var type = GeneratedAssembly.Value.GetType($"TestApp.Authorizers_{methodName}_Generated", throwOnError: true);
            var instance = Activator.CreateInstance(type);
            var method = type.GetMethod(methodName);

            object result;
            try
            {
                result = method.Invoke(instance, new[] { request, new TestLambdaContext() });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }

            if (result is Task task)
            {
                await task;
                return task.GetType().GetProperty("Result").GetValue(task);
            }

            return result;
        }

        private static Assembly CompileWithGenerator()
        {
            // The generator only runs when the source file is inside a project directory and writes the
            // serverless.template there, so use an isolated temporary project directory.
            var projectDirectory = Path.Combine(Path.GetTempPath(), "AuthorizerBindingFailureTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "TestApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var sourcePath = Path.Combine(projectDirectory, "Authorizers.cs");
            File.WriteAllText(sourcePath, UserSource);

            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: new[] { "NET8_0_OR_GREATER" });
            var compilation = CSharpCompilation.Create(
                "AuthorizerBindingFailureTests",
                new[] { CSharpSyntaxTree.ParseText(UserSource, parseOptions, sourcePath) },
                BuildReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            CSharpGeneratorDriver
                .Create(new[] { new SourceGenerator.Generator() }, parseOptions: parseOptions)
                .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

            Directory.Delete(projectDirectory, recursive: true);

            using var stream = new MemoryStream();
            var result = outputCompilation.Emit(stream);
            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(outputCompilation.SyntaxTrees.Count() > 1, "Source generator did not produce any output:\n" + string.Join("\n", generatorDiagnostics));
            Assert.True(result.Success, "Generated code failed to compile:\n" + string.Join("\n", errors));

            return Assembly.Load(stream.ToArray());
        }

        private static IReadOnlyList<MetadataReference> BuildReferences()
        {
            var references = new List<MetadataReference>();
            var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
                .Split(Path.PathSeparator);
            foreach (var path in trusted)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    references.Add(MetadataReference.CreateFromFile(path));
            }

            references.Add(MetadataReference.CreateFromFile(typeof(ILambdaContext).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(APIGatewayCustomAuthorizerRequest).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(APIGateway.AuthorizerResults).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer).Assembly.Location));
            return references.GroupBy(r => Path.GetFileName(r.Display)).Select(g => g.Last()).ToList();
        }

        private class TestLambdaContext : ILambdaContext
        {
            public string AwsRequestId => "request-id";
            public IClientContext ClientContext => null;
            public string FunctionName => "authorizer";
            public string FunctionVersion => "$LATEST";
            public ICognitoIdentity Identity => null;
            public string InvokedFunctionArn => null;
            public ILambdaLogger Logger { get; } = new TestLambdaLogger();
            public string LogGroupName => null;
            public string LogStreamName => null;
            public int MemoryLimitInMB => 128;
            public TimeSpan RemainingTime => TimeSpan.FromMinutes(1);
        }

        private class TestLambdaLogger : ILambdaLogger
        {
            public void Log(string message) { }
            public void LogLine(string message) { }
            public void Log(string level, string message, params object[] args) { }
            public void Log(string level, Exception exception, string message, params object[] args) { }
        }
    }
}
