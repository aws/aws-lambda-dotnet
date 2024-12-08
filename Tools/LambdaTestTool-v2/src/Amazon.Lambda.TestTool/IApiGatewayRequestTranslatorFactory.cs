namespace Amazon.Lambda.TestTool
{
    /// <summary>
    /// Defines the contract for a factory that creates API Gateway request translators.
    /// </summary>
    public interface IApiGatewayRequestTranslatorFactory
    {
        /// <summary>
        /// Creates an API Gateway request translator based on the specified API Gateway mode.
        /// </summary>
        /// <param name="apiGatewayMode">The API Gateway mode.</param>
        /// <returns>An instance of <see cref="IApiGatewayRequestTranslator"/> appropriate for the specified mode.</returns>
        IApiGatewayRequestTranslator Create(ApiGatewayMode apiGatewayMode);
    }
}
