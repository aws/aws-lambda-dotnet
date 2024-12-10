namespace Amazon.Lambda.TestTool.Utilities;

using Microsoft.AspNetCore.Routing.Template;

/// <summary>
/// Provides utility methods for working with route templates and extracting path parameters.
/// </summary>
public static class RouteTemplateUtility
{
    /// <summary>
    /// Extracts path parameters from an actual path based on a route template.
    /// </summary>
    /// <param name="routeTemplate">The route template to match against.</param>
    /// <param name="actualPath">The actual path to extract parameters from.</param>
    /// <returns>A dictionary of extracted path parameters and their values.</returns>
    /// <example>
    /// Using this method:
    /// <code>
    /// var routeTemplate = "/users/{id}/orders/{orderId}";
    /// var actualPath = "/users/123/orders/456";
    /// var parameters = RouteTemplateUtility.ExtractPathParameters(routeTemplate, actualPath);
    /// // parameters will contain: { {"id", "123"}, {"orderId", "456"} }
    /// </code>
    /// </example>
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

    /// <summary>
    /// Gets the default values for parameters in a parsed route template.
    /// </summary>
    /// <param name="parsedTemplate">The parsed route template.</param>
    /// <returns>A dictionary of default values for the template parameters.</returns>
    /// <example>
    /// Using this method:
    /// <code>
    /// var template = TemplateParser.Parse("/api/{version=v1}/users/{id}");
    /// var defaults = RouteTemplateUtility.GetDefaults(template);
    /// // defaults will contain: { {"version", "v1"} }
    /// </code>
    /// </example>
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
