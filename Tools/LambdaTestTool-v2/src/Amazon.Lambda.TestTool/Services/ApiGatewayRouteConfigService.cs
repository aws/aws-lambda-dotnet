using System.Collections;
using System.Text.Json;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services.IO;
using Microsoft.AspNetCore.Routing.Template;

namespace Amazon.Lambda.TestTool.Services;

/// <inheritdoc cref="IApiGatewayRouteConfigService"/>
public class ApiGatewayRouteConfigService : IApiGatewayRouteConfigService
{
    private readonly List<ApiGatewayRouteConfig> _routeConfigs = new();

    /// <summary>
    /// Constructs an instance of <see cref="ApiGatewayRouteConfigService"/>
    /// which loads and parses environment variables that match a specific prefix.
    /// </summary>
    /// <param name="environmentManager">A service to access environment variables.</param>
    public ApiGatewayRouteConfigService(
        IEnvironmentManager environmentManager)
    {
        var environmentVariables = environmentManager.GetEnvironmentVariables();

        foreach (DictionaryEntry entry in environmentVariables)
        {
            var key = entry.Key.ToString();
            if (key is null)
                continue;
            if (!(key.Equals(Constants.LambdaConfigEnvironmentVariablePrefix) || 
                key.StartsWith($"{Constants.LambdaConfigEnvironmentVariablePrefix}_")))
                continue;

            var jsonValue = entry.Value?.ToString();
            if (string.IsNullOrEmpty(jsonValue))
                continue;
            try
            {
                var config = JsonSerializer.Deserialize<ApiGatewayRouteConfig>(jsonValue);
                if (config != null)
                {
                    _routeConfigs.Add(config);
                }
            }
            catch (Exception)
            {
                try
                {
                    var config = JsonSerializer.Deserialize<List<ApiGatewayRouteConfig>>(jsonValue);
                    if (config != null)
                    {
                        _routeConfigs.AddRange(config);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deserializing environment variable {key}: {ex.Message}");
                }
            }
        }
    }

    /// <inheritdoc />
    public ApiGatewayRouteConfig? GetRouteConfig(string httpMethod, string path)
    {
        foreach (var routeConfig in _routeConfigs)
        {
            var template = TemplateParser.Parse(routeConfig.Path);

            var matcher = new TemplateMatcher(template, GetDefaults(template));

            var routeValueDictionary = new RouteValueDictionary();
            if (!matcher.TryMatch(path, routeValueDictionary))
                continue;
            
            if (!routeConfig.HttpMethod.Equals(httpMethod, StringComparison.InvariantCultureIgnoreCase))
                continue;

            return routeConfig;
        }

        return null;
    }

    private RouteValueDictionary GetDefaults(RouteTemplate parsedTemplate)
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