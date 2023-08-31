using System.Collections.Generic;

namespace Amazon.Lambda.AspNetCoreServer.Internal
{
    public class EncodingOptions : IEncodingOptions
    {
        public Dictionary<string, ResponseContentEncoding> ResponseContentEncodingForContentType { get; set ; }
        public Dictionary<string, ResponseContentEncoding> ResponseContentEncodingForContentEncoding { get; set; }
    }
}
