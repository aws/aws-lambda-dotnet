using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromRouteAttribute : Attribute, INamedAttribute
    {
        public string Name { get; set; }
    }
}