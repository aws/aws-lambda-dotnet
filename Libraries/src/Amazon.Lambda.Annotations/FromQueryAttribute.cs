using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromQueryAttribute : Attribute, INamedAttribute
    {
        public string Name { get; set; }
    }
}