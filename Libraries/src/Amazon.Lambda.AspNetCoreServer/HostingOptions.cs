using Amazon.Lambda.Core;

namespace Amazon.Lambda.AspNetCoreServer;

public class HostingOptions
{
    public ILambdaSerializer Serializer { get; set; }
}