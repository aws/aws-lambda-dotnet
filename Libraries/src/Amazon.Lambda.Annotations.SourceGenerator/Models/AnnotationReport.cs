using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    public class AnnotationReport
    {
        public IList<ILambdaFunctionSerializable> LambdaFunctions { get; } = new List<ILambdaFunctionSerializable>();
        public string CloudFormationTemplatePath{ get; set; }
        public string  ProjectRootDirectory { get; set; }
    }
}