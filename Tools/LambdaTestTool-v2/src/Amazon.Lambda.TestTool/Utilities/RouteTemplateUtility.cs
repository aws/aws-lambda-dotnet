using Microsoft.AspNetCore.Routing.Template;

namespace Amazon.Lambda.TestTool.Utilities
{
    public static class RouteTemplateUtility
    {
        public static Dictionary<string, string> ExtractPathParameters(string routeTemplate, string actualPath)
        {
            var template = TemplateParser.Parse(routeTemplate);
            var matcher = new TemplateMatcher(template, GetDefaults(template));
            var routeValues = new RouteValueDictionary();

            if (matcher.TryMatch(actualPath, routeValues))
            {
                return routeValues.ToDictionary(rv => rv.Key, rv => rv.Value?.ToString() ?? string.Empty);
            }

            return new Dictionary<string, string>();
        }

        public static RouteValueDictionary GetDefaults(RouteTemplate parsedTemplate)
        {
            var result = new RouteValueDictionary();

            foreach (var parameter in parsedTemplate.Parameters)
            {
                if (parameter.DefaultValue != null)
                {
                    if (parameter.Name != null) result.Add(parameter.Name, parameter.DefaultValue);
                }
            }

            return result;
        }
    }
}
