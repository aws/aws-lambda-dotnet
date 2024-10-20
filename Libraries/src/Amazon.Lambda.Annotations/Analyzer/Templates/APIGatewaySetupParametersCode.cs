using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    internal partial class APIGatewaySetupParameters
    {
        private readonly LambdaFunctionModel _model;

        public string ParameterSignature { get; set; }

        internal APIGatewaySetupParameters(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}