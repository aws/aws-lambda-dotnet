using System;

namespace Amazon.Lambda.Annotations.ALB
{
    /// <summary>
    /// Maps this parameter to a query string parameter from the ALB request
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : Attribute, INamedAttribute
    {
        /// <summary>
        /// Name of the query string parameter. If not specified, the parameter name is used.
        /// </summary>
        public string Name { get; set; }
    }
}
