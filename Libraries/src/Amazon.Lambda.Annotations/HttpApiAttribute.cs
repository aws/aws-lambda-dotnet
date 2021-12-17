using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HttpApiAttribute : Attribute
    {
        public HttpApiVersion Version { get; set; } = HttpApiVersion.V2;
        public string Template { get; set;  }
        public HttpMethod Method { get; set; }

        public HttpApiAttribute(HttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}