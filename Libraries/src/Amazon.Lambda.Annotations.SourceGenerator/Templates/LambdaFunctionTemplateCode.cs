using System;
using System.Collections;
using Amazon.Lambda.Annotations.SourceGenerator.Models;

namespace Amazon.Lambda.Annotations.SourceGenerator.Templates
{
    public partial class LambdaFunctionTemplate
    {
        private readonly LambdaFunctionModel _model;

        public LambdaFunctionTemplate(LambdaFunctionModel model)
        {
            _model = model;
        }
    }
}