using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromHeaderAttribute : Attribute, INamedAttribute
    {
        public string Name { get; set; }
    }
}