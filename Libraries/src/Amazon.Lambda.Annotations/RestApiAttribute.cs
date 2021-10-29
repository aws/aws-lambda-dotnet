using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RestApiAttribute : Attribute
    {
        public string Template { get; set;  }
        public HttpMethod Method { get; set; }

        public RestApiAttribute(HttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}