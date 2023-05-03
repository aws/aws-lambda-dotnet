using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class FieldsAndConstructor
    {
        private readonly LambdaFunctionModel _model;

        public FieldsAndConstructor(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}