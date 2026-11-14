using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class LambdaContextTests
    {
        private readonly TestEnvironmentVariables _environmentVariables;

        public LambdaContextTests()
        {
            _environmentVariables = new TestEnvironmentVariables();
        }

        [Fact]
        public void RemainingTimeIsPositive()
        {
            var deadline = DateTimeOffset.UtcNow.AddHours(1);
            var deadlineMs = deadline.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Aws-Request-Id"] = new[] { Guid.NewGuid().ToString() },
                ["Lambda-Runtime-Deadline-Ms"] = new[] { deadlineMs },
                ["Lambda-Runtime-Invoked-Function-Arn"] = new[] { "my-function-arn" }
            };

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);

            var context = new LambdaContext(runtimeApiHeaders, lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.True(context.RemainingTime >= TimeSpan.Zero, $"Remaining time is not a positive value: {context.RemainingTime}");
        }

        [Fact]
        public void RuntimeApiHeadersAddedToContext()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Aws-Request-Id"] = new[] { "request-generated-id" },
                ["Lambda-Runtime-Invoked-Function-Arn"] = new[] { "my-function-arn" },
                ["Lambda-Runtime-Aws-Tenant-Id"] = new[] { "tenant-generated-id" }
            };

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);

            var context = new LambdaContext(runtimeApiHeaders, lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.Equal("request-generated-id", context.AwsRequestId);
            Assert.Equal("my-function-arn", context.InvokedFunctionArn);
            Assert.Equal("tenant-generated-id", context.TenantId);
        }

        [Fact]
        public void InvocationIdIsReadWhenHeaderPresent()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Aws-Request-Id"] = new[] { "request-generated-id" },
                ["Lambda-Runtime-Invocation-Id"] = new[] { "invocation-generated-id" }
            };

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);

            var context = new LambdaContext(runtimeApiHeaders, lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.Equal("invocation-generated-id", runtimeApiHeaders.InvocationId);
            Assert.Equal("invocation-generated-id", context.InvocationId);
        }

        [Fact]
        public void InvocationIdIsNullWhenHeaderAbsent()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Aws-Request-Id"] = new[] { "request-generated-id" }
            };

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);

            var context = new LambdaContext(runtimeApiHeaders, lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.Null(runtimeApiHeaders.InvocationId);
            Assert.Null(context.InvocationId);
        }

        [Fact]
        public void RuntimeApiHeadersAreReadIgnoringCase()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["lambda-runtime-aws-request-id"] = new[] { "request-generated-id" },
                ["LAMBDA-RUNTIME-INVOKED-FUNCTION-ARN"] = new[] { "my-function-arn" },
                ["lambda-runtime-trace-id"] = new[] { "trace-id" },
                ["Lambda-Runtime-Client-Context"] = new[] { "{}" },
                ["lambda-runtime-cognito-identity"] = new[] { "{}" },
                ["lambda-runtime-deadline-ms"] = new[] { "1542409706888" },
                ["lambda-runtime-aws-tenant-id"] = new[] { "tenant-generated-id" },
                ["lambda-runtime-invocation-id"] = new[] { "invocation-generated-id" }
            };

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);

            Assert.Equal("request-generated-id", runtimeApiHeaders.AwsRequestId);
            Assert.Equal("my-function-arn", runtimeApiHeaders.InvokedFunctionArn);
            Assert.Equal("trace-id", runtimeApiHeaders.TraceId);
            Assert.Equal("{}", runtimeApiHeaders.ClientContextJson);
            Assert.Equal("{}", runtimeApiHeaders.CognitoIdentityJson);
            Assert.Equal("1542409706888", runtimeApiHeaders.DeadlineMs);
            Assert.Equal("tenant-generated-id", runtimeApiHeaders.TenantId);
            Assert.Equal("invocation-generated-id", runtimeApiHeaders.InvocationId);
        }

        [Fact]
        public void ContextValuesAreParsedFromHeadersOnce()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Aws-Request-Id"] = new[] { "request-generated-id" },
                ["Lambda-Runtime-Client-Context"] = new[] { "{\"custom\":{\"value\":\"test\"}}" },
                ["Lambda-Runtime-Cognito-Identity"] = new[] { "{\"cognitoIdentityId\":\"identity-id\",\"cognitoIdentityPoolId\":\"identity-pool-id\"}" }
            };

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);
            var lambdaEnvironment = new LambdaEnvironment(_environmentVariables);

            var context = new LambdaContext(runtimeApiHeaders, lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.Equal("test", context.ClientContext.Custom["value"]);
            Assert.Same(context.ClientContext, context.ClientContext);

            Assert.Equal("identity-id", context.Identity.IdentityId);
            Assert.Equal("identity-pool-id", context.Identity.IdentityPoolId);
            Assert.Same(context.Identity, context.Identity);

            Assert.Empty(context.W3C);
            Assert.Same(context.W3C, context.W3C);
        }

        [Fact]
        public void RuntimeApiHeadersThrowsWhenRequestIdIsAbsent()
        {
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Invoked-Function-Arn"] = new[] { "my-function-arn" }
            };

            Assert.Throws<KeyNotFoundException>(() => new RuntimeApiHeaders(headers));
        }
    }
}
