namespace Amazon.Lambda.TestTool
{
    public interface IRouteConfigurationParser
    {
        ApiGatewayRouteConfig GetRouteConfig(string httpMethod, string path);
        IDictionary<string, string> ExtractPathParameters(ApiGatewayRouteConfig routeConfig, string requestPath);
    }
}
