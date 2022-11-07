using Amazon.Lambda.Core;

namespace Amazon.Lambda.AspNetCoreServer.Internal;

public class HostingOptions
{
    public ILambdaSerializer Serializer { get; set; }
}