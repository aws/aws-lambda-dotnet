using System.Net;
using System.Net.Http.Headers;

namespace AWS.OCI.Builder.Tests;

/// <summary>An <see cref="HttpMessageHandler"/> that answers every request from a function and records what it was sent.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    /// <summary>Each request's method, URI and Authorization scheme, in the order they were sent.</summary>
    public List<(HttpMethod Method, Uri Uri, string? Auth)> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.Scheme));
        return Task.FromResult(_respond(request));
    }
}

internal static class TestHttp
{
    /// <summary>A 401 with a Basic challenge, as Amazon ECR sends it.</summary>
    public static HttpResponseMessage BasicChallenge(string registryHost)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue("Basic", $"realm=\"https://{registryHost}/\""));
        return response;
    }
}
