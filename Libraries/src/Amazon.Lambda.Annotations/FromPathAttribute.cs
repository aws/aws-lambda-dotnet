using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromPathAttribute : Attribute, INamedAttribute
    {
        public string Name { get; set; }
    }
}