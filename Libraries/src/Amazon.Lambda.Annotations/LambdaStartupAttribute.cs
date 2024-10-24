using System;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Indicates that the class will be used for registering services that 
    /// can be injected into Lambda functions.
    /// </summary>
    /// <remarks>
    /// The class should implement a ConfigureServices method that 
    /// adds one or more services to an IServiceCollection.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class LambdaStartupAttribute : Attribute
    {
    }
}
