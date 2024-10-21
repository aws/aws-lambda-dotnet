using System;

namespace Amazon.Lambda.Annotations
{
    /// <summary>
    /// Indicates that this service parameter will be injected into the Lambda function invocation.
    /// </summary>
    /// <remarks>
    /// Services injected using the FromServices attribute are created within the scope 
    /// that is created for each Lambda invocation.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromServicesAttribute : Attribute
    {
    }
}