using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Maps this parameter to a query string parameter
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : Attribute, INamedAttribute
    {
        /// <summary>
        /// Name of the parameter
        /// </summary>
        public string Name { get; set; }
    }
}