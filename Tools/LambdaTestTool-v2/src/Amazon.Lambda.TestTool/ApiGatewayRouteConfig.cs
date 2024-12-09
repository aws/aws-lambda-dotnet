namespace Amazon.Lambda.TestTool
{
    public class ApiGatewayRouteConfig
    {
        public string LambdaResourceName { get; set; }
        public string Endpoint { get; set; }
        public string HttpMethod { get; set; }
        public string Path { get; set; }
    }
}
