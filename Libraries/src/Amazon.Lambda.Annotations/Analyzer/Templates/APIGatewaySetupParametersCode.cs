using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class APIGatewaySetupParameters
    {
        private readonly LambdaFunctionModel _model;

        public string ParameterSignature { get; set; }

        public APIGatewaySetupParameters(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}