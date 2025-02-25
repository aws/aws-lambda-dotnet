using System.Collections.Concurrent;
using Amazon.Lambda.Model;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Implementation of ILambdaClient that manages Lambda client instances for different endpoints.
/// </summary>
public class LambdaClient : ILambdaClient, IDisposable
{
    internal ConcurrentDictionary<string, IAmazonLambda> Clients => _clients; // used for unit tests only
    private readonly ConcurrentDictionary<string, IAmazonLambda> _clients;

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaClient"/> class.
    /// </summary>
    public LambdaClient()
    {
        _clients = new ConcurrentDictionary<string, IAmazonLambda>();
    }

    /// <inheritdoc />
    public Task<InvokeResponse> InvokeAsync(InvokeRequest request, string endpoint)
    {
        return _clients.GetOrAdd(endpoint, CreateClient(endpoint)).InvokeAsync(request);
    }

    /// <summary>
    /// Creates a new Lambda client for the specified endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint URL for the Lambda client.</param>
    /// <returns>A new instance of IAmazonLambda configured for the specified endpoint.</returns>
    private IAmazonLambda CreateClient(string endpoint)
    {
        var config = new AmazonLambdaConfig
        {
            ServiceURL = endpoint
        };
        return new AmazonLambdaClient(
            new Amazon.Runtime.BasicAWSCredentials("accessKey", "secretKey"),
            config);
    }

    /// <summary>
    /// Disposes all Lambda clients and clears the client dictionary.
    /// </summary>
    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client?.Dispose();
        }
        _clients.Clear();
    }
}
