using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class AuthorizerInvoke
    {
        private readonly LambdaFunctionModel _model;

        public readonly string _parameterSignature;

        public AuthorizerInvoke(LambdaFunctionModel model, string parameterSignature)
        {
            _model = model;
            _parameterSignature = parameterSignature;
        }
    }
}
