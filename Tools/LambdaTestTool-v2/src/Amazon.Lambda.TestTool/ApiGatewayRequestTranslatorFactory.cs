namespace Amazon.Lambda.TestTool
{

    /// <summary>
    /// Factory class for creating API Gateway request translators based on the specified API Gateway mode.
    /// </summary>
    public class ApiGatewayRequestTranslatorFactory : IApiGatewayRequestTranslatorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiGatewayRequestTranslatorFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve dependencies.</param>
        public ApiGatewayRequestTranslatorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Creates an API Gateway request translator based on the specified API Gateway mode.
        /// </summary>
        /// <param name="apiGatewayMode">The API Gateway mode.</param>
        /// <returns>An instance of <see cref="IApiGatewayRequestTranslator"/> appropriate for the specified mode.</returns>
        public IApiGatewayRequestTranslator Create(ApiGatewayMode apiGatewayMode)
        {
            return apiGatewayMode switch
            {
                ApiGatewayMode.REST => _serviceProvider.GetRequiredService<ApiGatewayProxyRequestTranslator>(),
                ApiGatewayMode.HTTPV1 => _serviceProvider.GetRequiredService<ApiGatewayProxyRequestTranslator>(),
                ApiGatewayMode.HTTPV2 => _serviceProvider.GetRequiredService<ApiGatewayHttpApiV2ProxyRequestTranslator>()
            };
        }
    }
}
