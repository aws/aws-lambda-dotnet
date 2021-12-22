using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Class)]
    public class LambdaStartupAttribute : Attribute
    {
    }
}
