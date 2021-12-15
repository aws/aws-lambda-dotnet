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

            var context = new LambdaContext(runtimeApiHeaders, lambdaEnvironment, new Helpers.SimpleLoggerWriter());

            Assert.True(context.RemainingTime >= TimeSpan.Zero, $"Remaining time is not a positive value: {context.RemainingTime}");
        }
    }
}
