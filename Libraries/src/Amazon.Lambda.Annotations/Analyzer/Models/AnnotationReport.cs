using System.Collections.Generic;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models
{
    internal class AnnotationReport
    {
        /// <summary>
        /// Collection of annotated Lambda functions detected in the project
        /// </summary>
        public IList<ILambdaFunctionSerializable> LambdaFunctions { get; } = new List<ILambdaFunctionSerializable>();

        /// <summary>
        /// Path to the CloudFormation template for the Lambda project
        /// </summary>
        public string CloudFormationTemplatePath{ get; set; }

        /// <summary>
        /// Path to the directory containing the csproj file for the Lambda project
        /// </summary>
        public string ProjectRootDirectory { get; set; }

        /// <summary>
        /// Whether the flag to suppress telemetry about Lambda Annotations 
        /// projects is set to true in the Lambda project csproj
        /// </summary>
        public bool IsTelemetrySuppressed { get; set; }
    }
}