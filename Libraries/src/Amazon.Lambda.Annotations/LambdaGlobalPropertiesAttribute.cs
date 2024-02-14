
using System;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Used to add global configuration to the Lambda Source Generators. 
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly)]
    public class LambdaGlobalPropertiesAttribute : Attribute
    {
        /// <summary>
        /// Indicates whether the Lambda Annotations Framework will generate a static main method and the code to bootstrap the Lambda runtime.
        /// Ensure the .NET project's output type is configured for `Console Application`. In the project file the `OutputType` property will be set to `Exe`.
        /// Deploying as an executable versus a class library is required when compiling functions with Native AOT. It can also be useful to deploy as an executable to include specific versions of `Amazon.Lambda.RuntimeSupport` the .NET Lambda runtime client.
        /// </summary>
        public bool GenerateMain { get; set; }

        /// <summary>
        /// The runtime to set in the generated CloudFormation template. Either 'dotnet6', 'provided.al2', 'provided.al2023'. or 'dotnet8'.
        /// </summary>
        public string Runtime { get; set; }
    }
}