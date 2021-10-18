using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class APIRouteAttribute : Attribute
    {
    }
}