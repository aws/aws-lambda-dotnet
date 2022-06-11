using System;
using System.Collections.Generic;
using System.Text;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class SqsQueueAttribute : Attribute
    {
    }
}
