using System.Collections.Generic;
using System.Net.Http;
using System;
using Amazon.Lambda.AspNetCoreServer;
namespace TestWebApp
{
    public class HttpV2LambdaFunction : APIGatewayHttpApiV2ProxyFunction<Startup>
    {
        protected override IEnumerable<HttpRequestMessage> GetBeforeSnapshotRequests() =>
        [
            new HttpRequestMessage(HttpMethod.Get, "api/SnapStart")
        ];
    }
}
