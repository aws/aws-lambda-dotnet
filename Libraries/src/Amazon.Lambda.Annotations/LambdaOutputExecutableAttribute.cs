
using System;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Indicates this the Lambda function is going to target an executable instead of a class based handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LambdaOutputExecutableAttribute : Attribute
    {
    }
}