using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    internal partial class APIGatewayInvoke
    {
        private readonly LambdaFunctionModel _model;

        public readonly string _parameterSignature;

        internal APIGatewayInvoke(LambdaFunctionModel model, string parameterSignature)
        {
            _model = model;
            _parameterSignature = parameterSignature;
        }
    }
}