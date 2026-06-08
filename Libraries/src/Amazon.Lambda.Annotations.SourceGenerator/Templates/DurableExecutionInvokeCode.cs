using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class DurableExecutionInvoke
    {
        private readonly LambdaFunctionModel _model;

        public DurableExecutionInvoke(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}
