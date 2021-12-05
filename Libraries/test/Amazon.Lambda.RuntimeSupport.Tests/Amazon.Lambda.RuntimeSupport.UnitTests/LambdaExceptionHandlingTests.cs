using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class LambdaExceptionHandlingTests
    {
        [Fact]
        public void WriteJsonForUserCodeException()
        {
            Exception exception = null;
            try
            {
                ThrowTest();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            var exceptionInfo = ExceptionInfo.GetExceptionInfo(exception);
            var json = LambdaXRayExceptionWriter.WriteJson(exceptionInfo);
            Assert.NotNull(json);
            Assert.DoesNotMatch("\r\n", json);
            Assert.DoesNotMatch("\n", json);

            var jsonDocument = JsonDocument.Parse(json);

            JsonElement jsonElement;
            Assert.True(jsonDocument.RootElement.TryGetProperty("working_directory", out jsonElement));
            Assert.Equal(JsonValueKind.String, jsonElement.ValueKind);
            Assert.True(jsonElement.GetString().Length > 0);

            Assert.True(jsonDocument.RootElement.TryGetProperty("exceptions", out jsonElement));
            Assert.Equal(JsonValueKind.Array, jsonElement.ValueKind);

            jsonElement = jsonElement.EnumerateArray().First();
            Assert.Equal("ApplicationException", jsonElement.GetProperty("type").GetString());
            Assert.Equal("This is a fake Exception", jsonElement.GetProperty("message").GetString());

            jsonElement = jsonElement.GetProperty("stack").EnumerateArray().First();
            Assert.True(jsonElement.GetProperty("path").GetString().Length > 0);
            Assert.Equal("LambdaExceptionHandlingTests.ThrowTest", jsonElement.GetProperty("label").GetString());
            Assert.True(jsonElement.GetProperty("line").GetInt32() > 0);

            Assert.True(jsonDocument.RootElement.TryGetProperty("paths", out jsonElement));
            Assert.Equal(JsonValueKind.Array, jsonElement.ValueKind);

            var paths = jsonElement.EnumerateArray().ToArray();
            Assert.Single(paths);
            Assert.Contains("LambdaExceptionHandlingTests.cs", paths[0].GetString());
        }


        private void ThrowTest()
        {
            throw new ApplicationException("This is a fake Exception");
        }
    }
}
