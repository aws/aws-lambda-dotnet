using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Commands.Settings;
using Amazon.Lambda.TestTool.Services;
using Microsoft.Extensions.Options;

public class LambdaClient : ILambdaClient, IDisposable
{
    private readonly Dictionary<string, IAmazonLambda> _clients;
    private readonly RunCommandSettings _settings;
    private string _currentEndpoint;

    public LambdaClient(IOptions<RunCommandSettings> settings)
    {
        _settings = settings.Value;
        _clients = new Dictionary<string, IAmazonLambda>();
        _currentEndpoint = $"http://{_settings.LambdaEmulatorHost}:{_settings.LambdaEmulatorPort}";
        _clients[_currentEndpoint] = CreateClient(_currentEndpoint);
    }

    public Task<InvokeResponse> InvokeAsync(InvokeRequest request)
    {
        return _clients[_currentEndpoint].InvokeAsync(request);
    }

    public void SetEndpoint(string endpoint)
    {
        if (!_clients.ContainsKey(endpoint))
        {
            _clients[endpoint] = CreateClient(endpoint);
        }
        _currentEndpoint = endpoint;
    }

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

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client?.Dispose();
        }
        _clients.Clear();
    }
}
