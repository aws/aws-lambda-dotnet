using System;

namespace Amazon.Lambda.Annotations
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RestApiAttribute : Attribute
    {
        public string Template { get; set;  }
        public LambdaHttpMethod Method { get; set; }

        public RestApiAttribute(LambdaHttpMethod method, string template)
        {
            Template = template;
            Method = method;
        }
    }
}