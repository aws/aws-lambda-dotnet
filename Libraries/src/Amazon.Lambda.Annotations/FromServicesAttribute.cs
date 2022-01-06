using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromServicesAttribute : Attribute
    {
    }
}