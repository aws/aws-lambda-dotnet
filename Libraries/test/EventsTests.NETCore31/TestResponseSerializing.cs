using System.IO;

using Xunit;

using Amazon.Lambda.Serialization.SystemTextJson;

namespace EventsTests31
{
	public class TestResponseSerializing
    {
        [Fact]
        public void TestDefaultSerializer()
        {
            var serializer = new DefaultLambdaJsonSerializer();

            var response = new DummyResponse
            {
                BingBong = "Joy"
            };

            MemoryStream ms = new MemoryStream();
            StreamReader streamReader = new StreamReader(ms);
            serializer.Serialize(response, ms);
            ms.Seek(0, SeekOrigin.Begin);
            var utf8Payload = streamReader.ReadToEnd();

            var albResponse = new Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse
            {
                Body = utf8Payload
            };
            serializer.Serialize(albResponse, ms);
            ms.Seek(0, SeekOrigin.Begin);
            var json = streamReader.ReadToEnd();

            Assert.Equal(106, json.Length);
        }

        [Fact]
        public void TestDefaultSerializerWithUnsafeEncoder()
        {
            var serializer = new DefaultLambdaJsonSerializer(x => x.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

            var response = new DummyResponse
            {
                BingBong = "Joy"
            };

            MemoryStream ms = new MemoryStream();
            StreamReader streamReader = new StreamReader(ms);
            serializer.Serialize(response, ms);
            ms.Seek(0, SeekOrigin.Begin);
            var utf8Payload = streamReader.ReadToEnd();

            var albResponse = new Amazon.Lambda.ApplicationLoadBalancerEvents.ApplicationLoadBalancerResponse
            {
                Body = utf8Payload
            };
            serializer.Serialize(albResponse, ms);
            ms.Seek(0, SeekOrigin.Begin);
            var json = streamReader.ReadToEnd();

            Assert.Equal(90, json.Length);
        }

        public class DummyResponse
        {
            public string BingBong { get; set; }
        }
    }
}