using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    /// <summary>
    /// Maps this parameter to a custom authorizer item
    /// </summary>
    /// <remarks>
    /// Will try to get the specified key from Custom Authorizer values
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromCustomAuthorizerAttribute : Attribute, INamedAttribute
    {
        /// <summary>
        /// Key of the value
        /// </summary>
        public string Name { get; set; }
    }
}
