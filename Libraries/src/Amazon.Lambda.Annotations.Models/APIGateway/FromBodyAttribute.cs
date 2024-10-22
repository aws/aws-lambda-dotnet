using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Maps this parameter to the HTTP request body
    /// </summary>
    /// <remarks>
    /// If the parameter is a complex type then the request body will be assumed to be JSON and deserialized into the type.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromBodyAttribute : Attribute
    {
    }
}