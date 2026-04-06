using System;

namespace Amazon.Lambda.Annotations.ALB
{
    /// <summary>
    /// Maps this parameter to an HTTP header value from the ALB request
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : Attribute, INamedAttribute
    {
        /// <summary>
        /// Name of the header. If not specified, the parameter name is used.
        /// </summary>
        public string Name { get; set; }
    }
}
