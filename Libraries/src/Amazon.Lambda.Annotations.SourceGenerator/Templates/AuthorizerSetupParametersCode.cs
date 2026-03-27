using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class AuthorizerSetupParameters
    {
        private readonly LambdaFunctionModel _model;

        public string ParameterSignature { get; set; }

        public AuthorizerSetupParameters(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}
