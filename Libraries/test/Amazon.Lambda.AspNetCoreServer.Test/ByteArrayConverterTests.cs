using Amazon.Lambda.Serialization.SystemTextJson;
using System.IO;
using System.Text;
using Xunit;

namespace Amazon.Lambda.AspNetCoreServer.Test;

public class DefaultLambdaJsonSerializerTests
{
    private const string SampleValue = "Hello World!";

    [Fact]
    public void CanDeserializeIntArrayEncodedString() =>
        RunTest("{\"Value\":[72,101,108,108,111,32,87,111,114,108,100,33]}");

    [Fact]
    public void CanDeserializeBase64EncodedString() =>
        RunTest("{\"Value\":\"SGVsbG8gV29ybGQh\"}");

    private void RunTest(string jsonPayload)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));

        var defaultLambdaJsonSerializer = new DefaultLambdaJsonSerializer();
        var inputFromMemoryStream = defaultLambdaJsonSerializer.Deserialize<ClassWithByteArrayProperty>(ms);

        Assert.Equal(SampleValue, Encoding.UTF8.GetString(inputFromMemoryStream.Value));
    }

    private class ClassWithByteArrayProperty
    {
        public byte[] Value { get; set; }
    }
}
