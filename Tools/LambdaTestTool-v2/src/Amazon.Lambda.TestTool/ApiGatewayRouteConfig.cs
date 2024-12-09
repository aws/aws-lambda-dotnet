namespace Amazon.Lambda.TestTool
{
    public class ApiGatewayRouteConfig
    {
        public required string LambdaResourceName { get; set; }
        public required string Endpoint { get; set; }
        public required string HttpMethod { get; set; }
        public required string Path { get; set; }
    }
}
