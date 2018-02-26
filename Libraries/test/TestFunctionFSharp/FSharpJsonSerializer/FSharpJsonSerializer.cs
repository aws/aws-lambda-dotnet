using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.Json;
using Newtonsoft.Json;
using AmazonSerializer = Amazon.Lambda.Serialization.Json;

namespace FSharpJsonSerializer
{
    public class FSharpJsonSerializer : AmazonSerializer.JsonSerializer
    {
        private static JsonConverter converter = new Fable.JsonConverter();

        public FSharpJsonSerializer()
            : base(new []{converter})
        {
        }
    }
}