using System.Collections;
using System.Text.Json;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services.IO;
using Microsoft.AspNetCore.Routing.Template;

namespace Amazon.Lambda.TestTool.Services;

/// <inheritdoc cref="IApiGatewayRouteConfigService"/>
public class ApiGatewayRouteConfigService : IApiGatewayRouteConfigService
{
    private readonly ILogger<ApiGatewayRouteConfigService> _logger;
    private readonly List<ApiGatewayRouteConfig> _routeConfigs = new();

    /// <summary>
    /// Constructs an instance of <see cref="ApiGatewayRouteConfigService"/>
    /// which loads and parses environment variables that match a specific prefix.
    /// </summary>
    /// <param name="environmentManager">A service to access environment variables.</param>
    /// <param name="logger">The logger instance for <see cref="ApiGatewayRouteConfigService"/></param>
    public ApiGatewayRouteConfigService(
        IEnvironmentManager environmentManager,
        ILogger<ApiGatewayRouteConfigService> logger)
    {
        _logger = logger;
        
        logger.LogDebug("Retrieving all environment variables");
        var environmentVariables = environmentManager.GetEnvironmentVariables();

        logger.LogDebug("Looping over the retrieved environment variables");
        foreach (DictionaryEntry entry in environmentVariables)
        {
            var key = entry.Key.ToString();
            if (key is null)
                continue;
            logger.LogDebug("Environment variables: {VariableName}", key);
            if (!(key.Equals(Constants.LambdaConfigEnvironmentVariablePrefix) || 
                key.StartsWith($"{Constants.LambdaConfigEnvironmentVariablePrefix}_")))
            {
                logger.LogDebug("Skipping environment variable: {VariableName}", key);
                continue;
            }

            var jsonValue = entry.Value?.ToString()?.Trim();
            logger.LogDebug("Environment variable value: {VariableValue}", jsonValue);
            if (string.IsNullOrEmpty(jsonValue))
                continue;

            try
            {
                if (jsonValue.StartsWith('['))
                {
                    logger.LogDebug("Environment variable value starts with '['. Attempting to deserialize as a List.");
                    var config = JsonSerializer.Deserialize<List<ApiGatewayRouteConfig>>(jsonValue);
                    if (config != null)
                    {
                        _routeConfigs.AddRange(config);
                        logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                    }
                    else
                    {
                        logger.LogDebug("Environment variable was not properly deserialized and will be skipped.");
                    }
                }
                else
                {
                    logger.LogDebug("Environment variable value does not start with '['.");
                    var config = JsonSerializer.Deserialize<ApiGatewayRouteConfig>(jsonValue);
                    if (config != null)
                    {
                        _routeConfigs.Add(config);
                        logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                    }
                    else
                    {
                        logger.LogDebug("Environment variable was not properly deserialized and will be skipped.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing environment variable {key}: {ex.Message}");
                logger.LogDebug("Error deserializing environment variable {Key}: {Message}", key, ex.Message);
            }
        }
    }

    /// <inheritdoc />
    public ApiGatewayRouteConfig? GetRouteConfig(string httpMethod, string path)
    {
        foreach (var routeConfig in _routeConfigs)
        {
            _logger.LogDebug("Checking if '{Path}' matches '{Template}'.", path, routeConfig.Path);
            var template = TemplateParser.Parse(routeConfig.Path);

            var matcher = new TemplateMatcher(template, GetDefaults(template));

            var routeValueDictionary = new RouteValueDictionary();
            if (!matcher.TryMatch(path, routeValueDictionary))
            {
                _logger.LogDebug("'{Path}' does not match '{Template}'.", path, routeConfig.Path);
                continue;
            }
            
            _logger.LogDebug("'{Path}' matches '{Template}'. Now checking the HTTP Method.", path, routeConfig.Path);
            
            if (!routeConfig.HttpMethod.Equals(httpMethod, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogDebug("HTTP Method of '{Path}' is {HttpMethod} and does not match the method of '{Template}' which is {TemplateMethod}.", path, httpMethod, routeConfig.Path, routeConfig.HttpMethod);
                continue;
            }
            
            _logger.LogDebug("{HttpMethod} {Path} matches the existing configuration {TemplateMethod} {Template}.", httpMethod, path, routeConfig.HttpMethod, routeConfig.Path);

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