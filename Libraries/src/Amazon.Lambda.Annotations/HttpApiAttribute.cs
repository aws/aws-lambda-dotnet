using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpApiAttribute : Attribute
    {
        public HttpApiVersion Version { get; set; }

        public HttpApiAttribute(HttpApiVersion version)
        {
            Version = version;
        }
    }
}