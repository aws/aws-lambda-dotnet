using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    internal partial class FieldsAndConstructor
    {
        private readonly LambdaFunctionModel _model;

        internal FieldsAndConstructor(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}