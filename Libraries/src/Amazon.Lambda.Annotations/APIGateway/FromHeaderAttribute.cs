using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : Attribute, INamedAttribute
    {
        public string Name { get; set; }
    }
}