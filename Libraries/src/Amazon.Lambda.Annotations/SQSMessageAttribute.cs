using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class SqsMessageAttribute : Attribute
    {
    }
}
