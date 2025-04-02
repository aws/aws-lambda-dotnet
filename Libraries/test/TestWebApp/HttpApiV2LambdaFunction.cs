using System.Collections.Generic;
using System.Net.Http;
using System;
using Amazon.Lambda.AspNetCoreServer;
namespace TestWebApp
{
    public class HttpV2LambdaFunction : APIGatewayHttpApiV2ProxyFunction<Startup>
    {
#if NET8_0_OR_GREATER
        protected override IEnumerable<HttpRequestMessage> RegisterBeforeSnapshotRequest() =>
        [
            new HttpRequestMessage
            {
                RequestUri = new Uri("/api/Snapstart"),
                Method = HttpMethod.Get
            }
        ];
#endif
    }
}
