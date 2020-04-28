using System.IO;

using Xunit;

using Amazon.Lambda.Serialization.SystemTextJson;
using Newtonsoft.Json.Linq;

namespace EventsTests31
{
    public class TestResponseCasing
    {
        [Fact]
        public void TestPascalCase()
        {
            var serializer = new DefaultLambdaJsonSerializer();

            var response = new DummyResponse
            {
                BingBong = "Joy"
            };
            
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(response, ms);
            ms.Position = 0;
            var json = new StreamReader(ms).ReadToEnd();
            
            var serialized = JObject.Parse(json);
            Assert.Equal("Joy", serialized["BingBong"]?.ToString());
        }
        
        [Fact]
        public void TestCamelCase()
        {
            var serializer = new CamelCaseLambdaJsonSerializer();

            var response = new DummyResponse
            {
                BingBong = "Joy"
            };
            
            MemoryStream ms = new MemoryStream();
            serializer.Serialize(response, ms);
            ms.Position = 0;
            var json = new StreamReader(ms).ReadToEnd();
            
            var serialized = JObject.Parse(json);
            Assert.Equal("Joy", serialized["bingBong"]?.ToString());
        }        

        public class DummyResponse
        {
            public string BingBong { get; set; }
        }
    }
}