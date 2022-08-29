using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Maps this parameter to an HTTP header value
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : Attribute, INamedAttribute
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string Name { get; set; }
    }
}