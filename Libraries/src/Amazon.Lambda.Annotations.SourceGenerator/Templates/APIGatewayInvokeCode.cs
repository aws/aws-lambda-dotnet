using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class APIGatewayInvoke
    {
        private readonly LambdaFunctionModel _model;

        public readonly string _parameterSignature;

        public APIGatewayInvoke(LambdaFunctionModel model, string parameterSignature)
        {
            _model = model;
            _parameterSignature = parameterSignature;
        }
    }
}