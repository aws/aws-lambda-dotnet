using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.SourceGenerator
{
    /// <summary>
    /// Contains fully qualified name constants for various C# types
    /// </summary>
    public static class TypeFullNames
    {
        public const string IEnumerable = "System.Collections.IEnumerable";
        public const string Task1 = "System.Threading.Tasks.Task`1";
        public const string Task = "System.Threading.Tasks.Task";
        public const string MemoryStream = "System.IO.MemoryStream";
        public const string Stream = "System.IO.Stream";

        public const string ILambdaContext = "Amazon.Lambda.Core.ILambdaContext";
        public const string APIGatewayProxyRequest = "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest";
        public const string APIGatewayProxyResponse = "Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse";
        public const string APIGatewayHttpApiV2ProxyRequest = "Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest";
        public const string APIGatewayHttpApiV2ProxyResponse = "Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse";

        public const string IHttpResult = "Amazon.Lambda.Annotations.APIGateway.IHttpResult";

        public const string LambdaFunctionAttribute = "Amazon.Lambda.Annotations.LambdaFunctionAttribute";
        public const string FromServiceAttribute = "Amazon.Lambda.Annotations.FromServicesAttribute";

        public const string HttpApiVersion = "Amazon.Lambda.Annotations.APIGateway.HttpApiVersion";
        public const string RestApiAttribute = "Amazon.Lambda.Annotations.APIGateway.RestApiAttribute";
        public const string HttpApiAttribute = "Amazon.Lambda.Annotations.APIGateway.HttpApiAttribute";
        public const string FromQueryAttribute = "Amazon.Lambda.Annotations.APIGateway.FromQueryAttribute";
        public const string FromHeaderAttribute = "Amazon.Lambda.Annotations.APIGateway.FromHeaderAttribute";
        public const string FromBodyAttribute = "Amazon.Lambda.Annotations.APIGateway.FromBodyAttribute";
        public const string FromRouteAttribute = "Amazon.Lambda.Annotations.APIGateway.FromRouteAttribute";
        public const string FromCustomAuthorizerAttribute = "Amazon.Lambda.Annotations.APIGateway.FromCustomAuthorizerAttribute";

        public const string SQSEvent = "Amazon.Lambda.SQSEvents.SQSEvent";
        public const string SQSBatchResponse = "Amazon.Lambda.SQSEvents.SQSBatchResponse";
        public const string SQSEventAttribute = "Amazon.Lambda.Annotations.SQS.SQSEventAttribute";

        public const string LambdaSerializerAttribute = "Amazon.Lambda.Core.LambdaSerializerAttribute";
        public const string DefaultLambdaSerializer = "Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer";

        public const string LambdaSerializerAttributeWithoutNamespace = "LambdaSerializerAttribute";

        public static HashSet<string> Requests = new HashSet<string>
        {
            APIGatewayProxyRequest,
            APIGatewayHttpApiV2ProxyRequest
        };

        public static HashSet<string> Events = new HashSet<string>
        {
            RestApiAttribute,
            HttpApiAttribute,
            SQSEventAttribute
        };
    }
}