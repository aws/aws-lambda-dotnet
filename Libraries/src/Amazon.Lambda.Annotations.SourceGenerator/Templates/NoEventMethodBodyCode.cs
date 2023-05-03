using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class NoEventMethodBody
    {
        private readonly LambdaFunctionModel _model;

        public NoEventMethodBody(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}