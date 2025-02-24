using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Microsoft.Extensions.Options;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Implementation of ILambdaClient that manages Lambda client instances for different endpoints.
/// </summary>
public class LambdaClient : ILambdaClient, IDisposable
{
    internal Dictionary<string, IAmazonLambda> Clients => _clients; // used for unit tests only
    private readonly Dictionary<string, IAmazonLambda> _clients;
    private string _currentEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaClient"/> class.
    /// </summary>
    /// <param name="settings">The run command settings containing Lambda emulator configuration.</param>
    public LambdaClient(IOptions<RunCommandSettings> settings)
    {
        _clients = new Dictionary<string, IAmazonLambda>();
        _currentEndpoint = $"http://{settings.Value.LambdaEmulatorHost}:{settings.Value.LambdaEmulatorPort}";
        _clients[_currentEndpoint] = CreateClient(_currentEndpoint);
    }

    /// <inheritdoc />
    public Task<InvokeResponse> InvokeAsync(InvokeRequest request)
    {
        return _clients[_currentEndpoint].InvokeAsync(request);
    }

    /// <inheritdoc />
    public void SetEndpoint(string endpoint)
    {
        if (!_clients.ContainsKey(endpoint))
        {
            _clients[endpoint] = CreateClient(endpoint);
        }
        _currentEndpoint = endpoint;
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
