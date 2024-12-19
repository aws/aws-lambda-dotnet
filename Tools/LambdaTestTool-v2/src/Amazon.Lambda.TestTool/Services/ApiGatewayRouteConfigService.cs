// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Text.Json;
using Amazon.Lambda.TestTool.Models;
using Amazon.Lambda.TestTool.Services.IO;

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
                    var configs = JsonSerializer.Deserialize<List<ApiGatewayRouteConfig>>(jsonValue);
                    if (configs != null)
                    {
                        foreach (var config in configs)
                        {
                            if (IsRouteConfigValid(config))
                            {
                                _routeConfigs.Add(config);
                                _logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                            }
                            else
                            {
                                _logger.LogError("The route config {Method} {Path} is not valid. It will be skipped.", config.HttpMethod, config.Path);
                            }
                        }
                        _logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                    }
                    else
                    {
                        _logger.LogError("Environment variable was not properly deserialized and will be skipped.");
                    }
                }
                else
                {
                    _logger.LogDebug("Environment variable value does not start with '['.");
                    var config = JsonSerializer.Deserialize<ApiGatewayRouteConfig>(jsonValue);
                    if (config != null)
                    {
                        if (IsRouteConfigValid(config))
                        {
                            _routeConfigs.Add(config);
                            _logger.LogDebug("Environment variable deserialized and added to the existing configuration.");
                        }
                        else
                        {
                            _logger.LogError("The route config {Method} {Path} is not valid. It will be skipped.", config.HttpMethod, config.Path);
                        }
                    }
                    else
                    {
                        _logger.LogError("Environment variable was not properly deserialized and will be skipped.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deserializing environment variable {key}: {ex.Message}");
                _logger.LogError("Error deserializing environment variable {Key}: {Message}", key, ex.Message);
            }
        }
    }

    /// <summary>
    /// Applies some validity checks for Lambda route configuration.
    /// </summary>
    /// <param name="routeConfig">Lambda route configuration</param>
    /// <returns>true if route is valid, false if not</returns>
    private bool IsRouteConfigValid(ApiGatewayRouteConfig routeConfig)
    {
        if (string.IsNullOrEmpty(routeConfig.LambdaResourceName))
        {
            _logger.LogError("The Lambda resource name cannot be empty for the route config {Method} {Path}.",
                routeConfig.HttpMethod, routeConfig.Path);
            return false;
        }

        if (string.IsNullOrEmpty(routeConfig.HttpMethod))
        {
            _logger.LogError("The HTTP Method cannot be empty for the route config with the Lambda resource name {Lambda}.",
                routeConfig.LambdaResourceName);
            return false;
        }

        if (string.IsNullOrEmpty(routeConfig.Path))
        {
            _logger.LogError("The HTTP Path cannot be empty for the route config with the Lambda resource name {Lambda}.",
                routeConfig.LambdaResourceName);
            return false;
        }

        var occurrences = routeConfig.Path
            .Split('/')
            .Where(
                x => x.StartsWith("{") &&
                     x.EndsWith("+}"))
            .ToList();
        if (occurrences.Count > 1)
        {
            _logger.LogError("The route config {Method} {Path} cannot have multiple greedy variables {{proxy+}}.",
                routeConfig.HttpMethod, routeConfig.Path);
            return false;
        }

        if (occurrences.Count == 1 && !routeConfig.Path.EndsWith($"/{occurrences.Last()}"))
        {
            _logger.LogError("The route config {Method} {Path} uses a greedy variable {{proxy+}} but does not end with it.",
                routeConfig.HttpMethod, routeConfig.Path);
            return false;
        }

        return true;
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
        // Trimming start only because a '/' at the end of a path could correspond to 2 routes.
        // Example:
        // Route template: "/resource/{proxy+}"
        // Request path:   "/resource ---> Not a match
        // Request path:   "/resource/ ---> Is a match
        var requestSegments = path.TrimStart('/').Split('/');

        var candidates = new List<MatchResult>();

        foreach (var route in _routeConfigs)
        {
            _logger.LogDebug("{RequestMethod} {RequestPath}: Checking if matches with {TemplateMethod} {TemplatePath}.",
                httpMethod, path, route.HttpMethod, route.Path);

            // Must match HTTP method or be ANY
            if (!route.HttpMethod.Equals("ANY", StringComparison.InvariantCultureIgnoreCase) &&
                !route.HttpMethod.Equals(httpMethod, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogDebug("{RequestMethod} {RequestPath}: The HTTP method does not match.",
                    httpMethod, path);
                continue;
            }

            _logger.LogDebug("{RequestMethod} {RequestPath}: The HTTP method matches. Checking the route {TemplatePath}.",
                httpMethod, path, route.Path);

            var routeSegments = route.Path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            var matchDetail = MatchRoute(routeSegments, requestSegments);
            if (matchDetail.Matched)
            {
                candidates.Add(new MatchResult
                {
                    Route = route,
                    LiteralMatches = matchDetail.LiteralMatches,
                    GreedyVariables = matchDetail.GreedyCount,
                    NormalVariables = matchDetail.VariableCount,
                    TotalSegments = routeSegments.Length,
                    MatchedSegmentsBeforeGreedy = matchDetail.MatchedSegmentsBeforeGreedy
                });
            }
        }

        if (candidates.Count == 0)
        {
            _logger.LogDebug("{RequestMethod} {RequestPath}: The HTTP path does not match any configured route.",
                httpMethod, path);
            return null;
        }

        _logger.LogDebug("{RequestMethod} {RequestPath}: The following routes matched: {Routes}.",
            httpMethod, path, string.Join(", ", candidates.Select(x => x.Route.Path)));

        var best = candidates
            .OrderByDescending(c => c.LiteralMatches)
            .ThenByDescending(c => c.MatchedSegmentsBeforeGreedy)
            .ThenBy(c => c.GreedyVariables)
            .ThenBy(c => c.NormalVariables)
            .ThenBy(c => c.TotalSegments)
            .First();

        _logger.LogDebug("{RequestMethod} {RequestPath}: Matched with the following route: {Routes}.",
            httpMethod, path, best.Route.Path);

        return best.Route;
    }

    /// <summary>
    /// Attempts to match a given request path against a route template.
    /// </summary>
    /// <param name="routeSegments">The array of route template segments, which may include literal segments, normal variable segments, and greedy variable segments.</param>
    /// <param name="requestSegments">The array of request path segments to be matched against the route template.</param>
    /// <returns>
    /// A tuple containing the following elements:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Matched</term>
    ///     <description><c>true</c> if the entire route template can be matched against the given request path segments; <c>false</c> otherwise.</description>
    ///   </item>
    ///   <item>
    ///     <term>LiteralMatches</term>
    ///     <description>The number of literal segments in the route template that exactly matched the corresponding request path segments.</description>
    ///   </item>
    ///   <item>
    ///     <term>VariableCount</term>
    ///     <description>The total number of normal variable segments matched during the process.</description>
    ///   </item>
    ///   <item>
    ///     <term>GreedyCount</term>
    ///     <description>The total number of greedy variable segments matched during the process.</description>
    ///   </item>
    ///   <item>
    ///     <term>MatchedSegmentsBeforeGreedy</term>
    ///     <description>The number of segments (literal or normal variable) that were matched before encountering any greedy variable segment. A higher number indicates a more specific match before resorting to greedily matching the remainder of the path.</description>
    ///   </item>
    /// </list>
    /// </returns>
    private (
        bool Matched,
        int LiteralMatches,
        int VariableCount,
        int GreedyCount,
        int MatchedSegmentsBeforeGreedy)
        MatchRoute(string[] routeSegments, string[] requestSegments)
    {
        // Example scenario:
        // Route template: "/resource/{id}/subsegment/{proxy+}"
        // Request path:   "/resource/123/subsegment/foo/bar"
        // Here, routeSegments are ["resource", "{id}", "subsegment", "{proxy+}"]
        // and requestSegments are ["resource", "123", "subsegment", "foo", "bar"].

        var routeTemplateIndex = 0;
        var requestPathIndex = 0;
        var literalMatches = 0;
        var variableCount = 0;
        var greedyCount = 0;
        var matched = true;

        var matchedSegmentsBeforeGreedy = 0;
        var encounteredGreedy = false;

        // First, we try to match segments one-by-one until we run out of one or both arrays
        while (
            matched &&
            routeTemplateIndex < routeSegments.Length &&
            requestPathIndex < requestSegments.Length)
        {
            var routeTemplateSegment = routeSegments[routeTemplateIndex];
            var requestPathSegment = requestSegments[requestPathIndex];

            if (IsVariableSegment(routeTemplateSegment))
            {
                // If the current route template segment is a variable:
                // Example: route segment "{id}", request segment "123"
                if (IsGreedyVariable(routeTemplateSegment))
                {
                    // If it's a greedy variable like "{proxy+}".
                    // This block runs if the route template includes something like "/{proxy+}"
                    // and the incoming request still has segments to match.
                    // Example: template "{proxy+}" and request "foo/bar".
                    if (requestPathIndex >= requestSegments.Length)
                    {
                        // No segments left for the greedy variable to match
                        matched = false;
                    }
                    else
                    {
                        // We have at least one segment to consume greedily.
                        // Example: template "{proxy+}" can absorb "foo/bar".
                        greedyCount++;
                        encounteredGreedy = true;
                        routeTemplateIndex++;
                        // Consume all remaining request segments at once
                        requestPathIndex = requestSegments.Length;
                    }
                }
                else
                {
                    // It's a normal variable like "{id}".
                    // Example: template segment "{id}" and request segment "123".
                    variableCount++;
                    if (!encounteredGreedy) matchedSegmentsBeforeGreedy++;
                    routeTemplateIndex++;
                    requestPathIndex++;
                }
            }
            else
            {
                // The route template segment is a literal.
                // Example: template segment "resource", request segment "resource".
                if (!routeTemplateSegment.Equals(requestPathSegment, StringComparison.OrdinalIgnoreCase))
                {
                    // Literals must match exactly.
                    // Example: if template is "subsegment" and request is "foo", not a match.
                    matched = false;
                }
                else
                {
                    // Literal match succeeded.
                    // Example: template "resource", request "resource".
                    literalMatches++;
                    if (!encounteredGreedy) matchedSegmentsBeforeGreedy++;
                    routeTemplateIndex++;
                    requestPathIndex++;
                }
            }
        }

        // If we exhaust the request before the route template (or vice versa), we must handle leftovers.
        // This happens, for example, if the route template still has segments to match but the request is shorter.
        // Example scenario:
        // Route template: "/resource/{id}/{proxy+}"
        // Request path:   "/resource/123"
        if (matched && routeTemplateIndex < routeSegments.Length)
        {
            matched = false;
        }

        // If we matched the template but still have leftover request segments:
        // Example:
        // Route template: "/resource/{id}"
        // Request path:  "/resource/123/foo"
        // "foo" is unmatched, fail.
        if (matched && requestPathIndex < requestSegments.Length)
        {
            matched = false;
        }

        return (matched, literalMatches, variableCount, greedyCount, matchedSegmentsBeforeGreedy);
    }

    /// <summary>
    /// Determines if a given segment represents a variable segment.
    /// </summary>
    /// <param name="segment">The route template segment to check.</param>
    /// <returns><c>true</c> if the segment is a variable segment; <c>false</c> otherwise.</returns>
    private bool IsVariableSegment(string segment)
    {
        return segment.StartsWith("{") && segment.EndsWith("}");
    }

    /// <summary>
    /// Determines if a given segment represents a greedy variable segment.
    /// Greedy variables match one or more segments at the end of the route.
    /// </summary>
    /// <param name="segment">The route template segment to check.</param>
    /// <returns><c>true</c> if the segment is a greedy variable segment; <c>false</c> otherwise.</returns>
    private bool IsGreedyVariable(string segment)
    {
        return segment.StartsWith("{") && segment.EndsWith("+}");
    }

    /// <summary>
    /// Represents a match result for a particular route configuration.
    /// Contains information about how closely it matched, such as how many literal segments were matched,
    /// how many greedy and normal variables were used, and how many segments were matched before any greedy variable.
    /// </summary>
    private class MatchResult
    {
        /// <summary>
        /// The route configuration that this match result corresponds to.
        /// </summary>
        public required ApiGatewayRouteConfig Route { get; set; }

        /// <summary>
        /// The number of literal segments matched.
        /// </summary>
        public int LiteralMatches { get; set; }

        /// <summary>
        /// The number of greedy variables matched.
        /// </summary>
        public int GreedyVariables { get; set; }

        /// <summary>
        /// The number of normal variables matched.
        /// </summary>
        public int NormalVariables { get; set; }

        /// <summary>
        /// The total number of segments in the route template.
        /// </summary>
        public int TotalSegments { get; set; }

        /// <summary>
        /// The number of segments (literal or normal variable) matched before encountering any greedy variable.
        /// </summary>
        public int MatchedSegmentsBeforeGreedy { get; set; }
    }
}
