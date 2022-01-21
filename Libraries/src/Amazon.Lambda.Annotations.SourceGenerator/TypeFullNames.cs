using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    /// <summary>
    /// Contains fully qualified name constants for various C# types
    /// </summary>
    public static class TypeFullNames
    {
        public const string RestApiAttribute = "Amazon.Lambda.Annotations.RestApiAttribute";
        public const string HttpApiAttribute = "Amazon.Lambda.Annotations.HttpApiAttribute";
        public const string APIGatewayProxyRequest = "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest";
        public const string APIGatewayProxyResponse = "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse";
        public const string APIGatewayHttpApiV2ProxyRequest = "Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest";
        public const string APIGatewayHttpApiV2ProxyResponse = "Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse";
        public const string MemoryStream = "System.IO.MemoryStream";
        public const string HttpApiVersion = "Amazon.Lambda.Annotations.HttpApiVersion";
        public const string LambdaFunctionAttribute = "Amazon.Lambda.Annotations.LambdaFunctionAttribute";
        public const string FromQueryAttribute = "Amazon.Lambda.Annotations.FromQueryAttribute";
        public const string FromHeaderAttribute = "Amazon.Lambda.Annotations.FromHeaderAttribute";
        public const string FromBodyAttribute = "Amazon.Lambda.Annotations.FromBodyAttribute";
        public const string FromRouteAttribute = "Amazon.Lambda.Annotations.FromRouteAttribute";
        public const string FromServiceAttribute = "Amazon.Lambda.Annotations.FromServicesAttribute";
        public const string ILambdaContext = "Amazon.Lambda.Core.ILambdaContext";
        public const string IEnumerable = "System.Collections.IEnumerable";
        public const string Task1 = "System.Threading.Tasks.Task`1";
        public const string Task = "System.Threading.Tasks.Task";

        public static HashSet<string> Requests = new HashSet<string>
        {
            APIGatewayProxyRequest,
            APIGatewayHttpApiV2ProxyRequest
        };

        public static HashSet<string> Events = new HashSet<string>
        {
            RestApiAttribute,
            HttpApiAttribute
        };
    }
}