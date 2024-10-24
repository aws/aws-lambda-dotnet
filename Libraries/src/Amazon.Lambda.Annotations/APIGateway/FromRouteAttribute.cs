using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Maps this parameter to a resource path segment
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromRouteAttribute : Attribute, INamedAttribute
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string Name { get; set; }
    }
}