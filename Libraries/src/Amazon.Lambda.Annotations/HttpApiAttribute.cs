using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpApiAttribute : Attribute
    {
        public HttpApiVersion Version { get; set; }
        public string Template { get; set;  }
        public HttpMethod Method { get; set; }

        public HttpApiAttribute(HttpMethod method, HttpApiVersion version, string template)
        {
            Version = version;
            Template = template;
            Method = method;
        }
    }
}