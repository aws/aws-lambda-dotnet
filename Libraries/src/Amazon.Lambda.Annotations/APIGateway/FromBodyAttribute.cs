using System;

namespace Amazon.Lambda.Annotations.APIGateway
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class FromBodyAttribute : Attribute
    {
    }
}