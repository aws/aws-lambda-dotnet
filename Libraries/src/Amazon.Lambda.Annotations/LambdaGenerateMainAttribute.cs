
using System;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Used when deploying Lambda functions as a .NET executable instead of a class library. Ensure the .NET project's output type is configured for `Console Application`. In the project file the `OutputType` property will be set to `Exe`.
    ///
    /// Deploying as an executable versus a class library is required when compiling functions with Native AOT. It can also be useful to deploy as an executable to include specific versions of `Amazon.Lambda.RuntimeSupport` the .NET Lambda runtime client.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly)]
    public class LambdaGenerateMainAttribute : Attribute
    {
    }
}