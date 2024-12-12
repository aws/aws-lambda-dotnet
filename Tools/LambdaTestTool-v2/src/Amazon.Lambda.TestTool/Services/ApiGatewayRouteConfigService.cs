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
    private readonly IEnvironmentManager _environmentManager;
    private List<ApiGatewayRouteConfig> _routeConfigs = new();

    /// <summary>
    /// Constructs an instance of <see cref="ApiGatewayRouteConfigService"/>.
    /// </summary>
    /// <param name="environmentManager">A service to access environment variables.</param>
    /// <param name="logger">The logger instance for <see cref="ApiGatewayRouteConfigService"/></param>
    public ApiGatewayRouteConfigService(
        IEnvironmentManager environmentManager,
        ILogger<ApiGatewayRouteConfigService> logger)
    {
        _logger = logger;
        _environmentManager = environmentManager;

        LoadLambdaConfigurationFromEnvironmentVariables();
        UpdateRouteConfigMetadataAndSorting();
    }

    /// <summary>
    /// Loads and parses environment variables that match a specific prefix.
    /// </summary>
    private void LoadLambdaConfigurationFromEnvironmentVariables()
    {
        _logger.LogDebug("Retrieving all environment variables");
        var environmentVariables = _environmentManager.GetEnvironmentVariables();

        _logger.LogDebug("Looping over the retrieved environment variables");
        foreach (DictionaryEntry entry in environmentVariables)
        {
            var key = entry.Key.ToString();
            if (key is null)
                continue;
            _logger.LogDebug("Environment variables: {VariableName}", key);
            if (!(key.Equals(Constants.LambdaConfigEnvironmentVariablePrefix) || 
                key.StartsWith($"{Constants.LambdaConfigEnvironmentVariablePrefix}_")))
            {
                _logger.LogDebug("Skipping environment variable: {VariableName}", key);
                continue;
            }

            var jsonValue = entry.Value?.ToString()?.Trim();
            _logger.LogDebug("Environment variable value: {VariableValue}", jsonValue);
            if (string.IsNullOrEmpty(jsonValue))
                continue;

            try
            {
                if (jsonValue.StartsWith('['))
                {
                    _logger.LogDebug("Environment variable value starts with '['. Attempting to deserialize as a List.");
                    var config = JsonSerializer.Deserialize<List<ApiGatewayRouteConfig>>(jsonValue);
                    if (config != null)
                    {
                        _routeConfigs.AddRange(config);
                        _logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                    }
                    else
                    {
                        _logger.LogDebug("Environment variable was not properly deserialized and will be skipped.");
                    }
                }
                else
                {
                    _logger.LogDebug("Environment variable value does not start with '['.");
                    var config = JsonSerializer.Deserialize<ApiGatewayRouteConfig>(jsonValue);
                    if (config != null)
                    {
                        _routeConfigs.Add(config);
                        _logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                    }
                    else
                    {
                        _logger.LogDebug("Environment variable was not properly deserialized and will be skipped.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing environment variable {key}: {ex.Message}");
                _logger.LogDebug("Error deserializing environment variable {Key}: {Message}", key, ex.Message);
            }
        }
    }

    /// <summary>
    /// API Gateway selects the route with the most-specific match, using the following priorities:
    /// 1. Full match for a route and method.
    /// 2. Match for a route and method with path variable.
    /// 3. Match for a route and method with a greedy path variable ({proxy+}).
    /// 
    /// For example, this is the order for the following example routes:
    /// 1. GET /pets/dog/1
    /// 2. GET /pets/dog/{id}
    /// 3. GET /pets/{proxy+}
    /// 4. ANY /{proxy+}
    /// 
    /// For more info: https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-routes.html
    /// </summary>
    private void UpdateRouteConfigMetadataAndSorting()
    {
        _logger.LogDebug("Updating the metadata needed to properly sort the Lambda config");
        foreach (var routeConfig in _routeConfigs)
        {
            if (routeConfig.Path.Contains("{proxy+}"))
            {
                routeConfig.ApiGatewayRouteType = ApiGatewayRouteType.Proxy;
                routeConfig.LengthBeforeProxy = routeConfig.Path.IndexOf("{proxy+}", StringComparison.InvariantCultureIgnoreCase);
                _logger.LogDebug("{Method} {Route} uses a proxy variable which starts at position {Position}.", 
                    routeConfig.HttpMethod,
                    routeConfig.Path,
                    routeConfig.LengthBeforeProxy);
            }
            else if (routeConfig.Path.Contains("{") && routeConfig.Path.Contains("}"))
            {
                routeConfig.ApiGatewayRouteType = ApiGatewayRouteType.Variable;
                routeConfig.LengthBeforeProxy = int.MaxValue;
                
                var template = TemplateParser.Parse(routeConfig.Path);
                routeConfig.ParameterCount = template.Parameters.Count;
                
                _logger.LogDebug("{Method} {Route} uses {ParameterCount} path variable(s).", 
                    routeConfig.HttpMethod,
                    routeConfig.Path,
                    routeConfig.ParameterCount);
            }
            else
            {
                routeConfig.ApiGatewayRouteType = ApiGatewayRouteType.Exact;
                routeConfig.LengthBeforeProxy = int.MaxValue;
                
                _logger.LogDebug("{Method} {Route} is an exact route with no variables.", 
                    routeConfig.HttpMethod,
                    routeConfig.Path);
            }
        }

        _logger.LogDebug("Sorting the Lambda configs based on the updated metadata");
        
        // The sorting will be as follows:
        // 1. Exact paths first
        // 2. Paths with variables (the less the number of variables, the more exact the path is which means higher priority)
        // 3. Paths with greedy path variable {proxy+} (the more characters before {proxy+}, the more specific the path is, the higher the priority)
        _routeConfigs = _routeConfigs
            .OrderBy(x => x.ApiGatewayRouteType)
            .ThenBy(x => x.ParameterCount)
            .ThenByDescending(x => x.LengthBeforeProxy)
            .ToList();
    }
    
    /// <summary>
    /// A method to match an HTTP Method and HTTP Path with an existing <see cref="ApiGatewayRouteConfig"/>.
    /// Given that route templates could contain variables as well as greedy path variables.
    /// API Gateway matches incoming routes in a certain order.
    /// 
    /// API Gateway selects the route with the most-specific match, using the following priorities:
    /// 1. Full match for a route and method.
    /// 2. Match for a route and method with path variable.
    /// 3. Match for a route and method with a greedy path variable ({proxy+}).
    /// 
    /// For example, this is the order for the following example routes:
    /// 1. GET /pets/dog/1
    /// 2. GET /pets/dog/{id}
    /// 3. GET /pets/{proxy+}
    /// 4. ANY /{proxy+}
    /// 
    /// For more info: https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-develop-routes.html
    /// </summary>
    /// <param name="httpMethod">An HTTP Method</param>
    /// <param name="path">An HTTP Path</param>
    /// <returns>An <see cref="ApiGatewayRouteConfig"/> corresponding to Lambda function with an API Gateway HTTP Method and Path.</returns>
    public ApiGatewayRouteConfig? GetRouteConfig(string httpMethod, string path)
    {
        foreach (var routeConfig in _routeConfigs)
        {
            _logger.LogDebug("Checking if '{Path}' matches '{Template}'.", path, routeConfig.Path);
            
            // ASP.NET has similar functionality as API Gateway which supports a greedy path variable.
            // Replace the API Gateway greedy parameter with ASP.NET catch-all parameter
            var transformedPath = routeConfig.Path.Replace("{proxy+}", "{*proxy}");
            
            var template = TemplateParser.Parse(transformedPath);

            var matcher = new TemplateMatcher(template, new RouteValueDictionary());

            var routeValueDictionary = new RouteValueDictionary();
            if (!matcher.TryMatch(path, routeValueDictionary))
            {
                _logger.LogDebug("'{Path}' does not match '{Template}'.", path, routeConfig.Path);
                continue;
            }
            
            _logger.LogDebug("'{Path}' matches '{Template}'. Now checking the HTTP Method.", path, routeConfig.Path);
            
            if (!routeConfig.HttpMethod.Equals("ANY") && 
                !routeConfig.HttpMethod.Equals(httpMethod, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogDebug("HTTP Method of '{Path}' is {HttpMethod} and does not match the method of '{Template}' which is {TemplateMethod}.", path, httpMethod, routeConfig.Path, routeConfig.HttpMethod);
                continue;
            }
            
            _logger.LogDebug("{HttpMethod} {Path} matches the existing configuration {TemplateMethod} {Template}.", httpMethod, path, routeConfig.HttpMethod, routeConfig.Path);

            return routeConfig;
        }

        return null;
    }
}