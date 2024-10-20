namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    using System.Collections.Generic;

    using Amazon.Lambda.Annotations.SourceGenerator.Models;

    internal partial class ExecutableAssembly
    {
        private List<LambdaFunctionModel> _lambdaFunctions;
        private string _containingNamespace;
        
        internal ExecutableAssembly(List<LambdaFunctionModel> lambdaFunctions, string containingNamespace)
        {
            this._lambdaFunctions = lambdaFunctions;
            this._containingNamespace = containingNamespace;
        }
    }
}