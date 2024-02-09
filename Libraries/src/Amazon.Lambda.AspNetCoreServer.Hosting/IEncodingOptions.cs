namespace Amazon.Lambda.AspNetCoreServer.Hosting
{
    public interface IEncodingOptions
    {
        public Dictionary<string, ResponseContentEncoding>? ResponseContentEncodingForContentType { get; set; }
        public Dictionary<string, ResponseContentEncoding>? ResponseContentEncodingForContentEncoding { get; set; }
    }
}