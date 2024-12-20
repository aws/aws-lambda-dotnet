namespace Amazon.Lambda.TestTool.Utilities;

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing.Template;

/// <summary>
/// Provides utility methods for working with route templates and extracting path parameters.
/// </summary>
public static class RouteTemplateUtility
{
    private const string TemporaryPrefix = "__aws_param__";

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
        // Preprocess the route template to convert from .net style format to aws
        routeTemplate = PreprocessRouteTemplate(routeTemplate);

        var template = TemplateParser.Parse(routeTemplate);
        var matcher = new TemplateMatcher(template, new RouteValueDictionary());
        var routeValues = new RouteValueDictionary();

        if (matcher.TryMatch(actualPath, routeValues))
        {
            var result = new Dictionary<string, string>();

            foreach (var param in template.Parameters)
            {
                if (routeValues.TryGetValue(param.Name, out var value))
                {
                    var stringValue = value?.ToString() ?? string.Empty;

                    // For catch-all parameters, remove the leading slash if present
                    if (param.IsCatchAll)
                    {
                        stringValue = stringValue.TrimStart('/');
                    }

                    // Restore original parameter name
                    var originalParamName = RestoreOriginalParamName(param.Name);
                    result[originalParamName] = stringValue;
                }
            }

            return result;
        }

        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Preprocesses a route template to make it compatible with ASP.NET Core's TemplateMatcher.
    /// </summary>
    /// <param name="template">The original route template, potentially in AWS API Gateway format.</param>
    /// <returns>A preprocessed route template compatible with ASP.NET Core's TemplateMatcher.</returns>
    /// <remarks>
    /// This method performs two main transformations:
    /// 1. Converts AWS-style {proxy+} to ASP.NET Core style {*proxy}
    /// 2. Handles AWS ignoring constraignts by temporarily renaming parameters
    ///    (e.g., {abc:int} becomes {__aws_param__abc__int})
    /// </remarks>
    private static string PreprocessRouteTemplate(string template)
    {
        // Convert AWS-style {proxy+} to ASP.NET Core style {*proxy}
        template = Regex.Replace(template, @"\{(\w+)\+\}", "{*$1}");

        // Handle AWS-style "constraints" by replacing them with temporary parameter names
        return Regex.Replace(template, @"\{([^}]+):([^}]+)\}", match =>
        {
            var paramName = match.Groups[1].Value;
            var constraint = match.Groups[2].Value;

            // There is a low chance that one of the parameters being used actually follows the syntax of {TemporaryPrefix}{paramName}__{constraint}.
            // But i dont think its signifigant enough to worry about.
            return $"{{{TemporaryPrefix}{paramName}__{constraint}}}";
        });
    }

    /// <summary>
    /// Restores the original parameter name after processing by TemplateMatcher.
    /// </summary>
    /// <param name="processedName">The parameter name after processing and matching.</param>
    /// <returns>The original parameter name.</returns>
    /// <remarks>
    /// This method reverses the transformation done in PreprocessRouteTemplate.
    /// For example, "__aws_param__abc__int" would be restored to "abc:int".
    /// </remarks>
    private static string RestoreOriginalParamName(string processedName)
    {
        if (processedName.StartsWith(TemporaryPrefix))
        {
            var parts = processedName.Substring(TemporaryPrefix.Length).Split("__", 2);
            if (parts.Length == 2)
            {
                return $"{parts[0]}:{parts[1]}";
            }
        }
        return processedName;
    }
}
