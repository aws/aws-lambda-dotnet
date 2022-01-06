using System;
using System.Collections.Generic;
using System.Linq;

namespace Amazon.Lambda.Annotations.SourceGenerator.Models.Attributes
{
    public static class TemplateParametersExtension
    {
        /// <summary>
        /// Returns aa <see cref="HashSet{T}"/> of string containing template parameter
        /// parsed from the <see cref="RestApiAttribute.Template"/>.
        /// </summary>
        /// <param name="attribute">this <see cref="RestApiAttribute"/> object.</param>
        public static HashSet<string> GetTemplateParameters(this RestApiAttribute attribute)
        {
            var template = attribute.Template;
            return GetTemplateParameters(template);
        }

        /// <summary>
        /// Returns aa <see cref="HashSet{T}"/> of string containing template parameter
        /// parsed from the <see cref="HttpApiAttribute.Template"/>.
        /// </summary>
        /// <param name="attribute">this <see cref="HttpApiAttribute"/> object.</param>
        public static HashSet<string> GetTemplateParameters(this HttpApiAttribute attribute)
        {
            var template = attribute.Template;
            return GetTemplateParameters(template);
        }

        private static HashSet<string> GetTemplateParameters(string template)
        {
            var parameters = new HashSet<string>();

            var components = template.Split('/').Where(c => !string.IsNullOrWhiteSpace(c));
            foreach (var component in components)
            {
                if (component.StartsWith("{") && component.EndsWith("}"))
                {
                    var parameter = component.Substring(1, component.Length - 2);
                    parameters.Add(parameter);
                }
            }

            return parameters;
        }
    }
}