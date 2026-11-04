using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AWS.OCI.Builder;
using AWS.OCI.Builder.Images;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>The socket-level address guard: which hosts are checked, proxies, ECR trust, and how failures surface.</summary>
public class ConnectGuardTests
{
    private const string EcrHost = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
    private static readonly string AnyDigest = "sha256:" + new string('a', 64);

    [Fact]
    public async Task Guard_allows_a_loopback_proxy()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = RegistryClient.Create(
            "public.ecr.aws", new AnonymousRegistryCredentialSource(), proxy: new WebProxy($"http://127.0.0.1:{port}"));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task call = client.GetManifestAsync("docker/library/alpine", "3.20", cts.Token);

        using TcpClient accepted = await listener.AcceptTcpClientAsync(cts.Token);
        using var reader = new StreamReader(accepted.GetStream(), Encoding.ASCII, leaveOpen: true);
        string? requestLine = await reader.ReadLineAsync(cts.Token);
        byte[] reply = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n");
        await accepted.GetStream().WriteAsync(reply, cts.Token);

        Assert.Equal("CONNECT public.ecr.aws:443 HTTP/1.1", requestLine);
        await Assert.ThrowsAnyAsync<Exception>(() => call); // the fake proxy refuses the tunnel
    }

    [Fact]
    public async Task Guard_refuses_a_non_ECR_registry_on_a_private_address()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = RegistryClient.Create($"127.0.0.1:{port}", new AnonymousRegistryCredentialSource());
        var ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetManifestAsync("repo", "latest"));
        Assert.Contains("Refusing to connect to '127.0.0.1'", ex.Message);
        Assert.False(listener.Pending()); // the socket was never dialed
    }

    [Fact]
    public async Task Guard_connects_to_ECR_on_a_private_address_as_through_a_VPC_endpoint()
    {
        // Simulates ECR private DNS: the ECR host name resolves to a private (here loopback) address.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string ecrHost = $"{EcrHost}:{port}";
        Task<IPAddress[]> Resolve(string host, CancellationToken _) => Task.FromResult(new[] { IPAddress.Loopback });

        // A realm on another host that also resolves privately must still be refused by the same client.
        using var client = RegistryClient.Create(ecrHost, new AnonymousRegistryCredentialSource(), resolveHost: Resolve);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task call = client.GetManifestAsync("repo", "latest", cts.Token);
        using TcpClient accepted = await listener.AcceptTcpClientAsync(cts.Token); // the ECR host was dialed
        accepted.Close();
        Exception ex = await Assert.ThrowsAnyAsync<Exception>(() => call);  // TLS then fails against the plain listener
        Assert.DoesNotContain("Refusing to connect", ex.ToString());

        using var other = RegistryClient.Create($"mirror.corp.example:{port}", new AnonymousRegistryCredentialSource(), resolveHost: Resolve);
        var refused = await Assert.ThrowsAsync<RegistryException>(() => other.GetManifestAsync("repo", "latest"));
        Assert.Contains("Refusing to connect to 'mirror.corp.example'", refused.Message);
        Assert.False(listener.Pending());
    }

    [Theory]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com", "123456789012.dkr.ecr.us-west-2.amazonaws.com")]
    [InlineData("123456789012.dkr.ecr-fips.us-gov-west-1.amazonaws.com", "123456789012.dkr.ecr-fips.us-gov-west-1.amazonaws.com")]
    [InlineData("123456789012.dkr.ecr.cn-north-1.amazonaws.com.cn", "123456789012.dkr.ecr.cn-north-1.amazonaws.com.cn")]
    [InlineData("127.0.0.1:5000", null)]
    [InlineData("localhost:5000", null)]
    [InlineData("public.ecr.aws", null)]
    [InlineData("mirror.corp.example", null)]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com\n", "123456789012.dkr.ecr.us-west-2.amazonaws.com")] // Uri drops the newline; the same host is dialed
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com:443", "123456789012.dkr.ecr.us-west-2.amazonaws.com")]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com.evil.example", null)]
    [InlineData("123456789012.dkr.ecr.us-west-2.amazonaws.com@evil.example", null)]
    [InlineData("evil.example/123456789012.dkr.ecr.us-west-2.amazonaws.com", null)]
    public void Only_ECR_registries_are_trusted(string registryHost, string? expected)
        => Assert.Equal(expected, RegistryClient.TrustedHostOf(registryHost));

    [Theory]
    [InlineData("https://reg.example/v2/", "reg.example", "reg.example", null, false)]        // the trusted registry
    [InlineData("https://reg.example/v2/", "reg.example", null, null, true)]                  // no trusted registry
    [InlineData("https://reg.example./v2/", "reg.example.", "reg.example", null, false)]      // trailing dot
    [InlineData("https://auth.example/token", "auth.example", "reg.example", null, true)]     // realm on another host
    [InlineData("https://[::1]/x", "::1", "reg.example", null, true)]                         // IPv6 literal, not trusted
    [InlineData("https://[::1]:5000/v2/", "::1", "::1", null, false)]                         // IPv6 registry, trusted
    [InlineData("https://auth.example/token", "127.0.0.1", "reg.example", "http://127.0.0.1:3128", false)] // its proxy
    [InlineData("https://auth.example/token", "10.0.0.9", "reg.example", "http://127.0.0.1:3128", true)]   // not its proxy
    public void Guard_decides_which_connections_to_check(string requestUri, string connectHost, string? trustedHost, string? proxy, bool expected)
    {
        IWebProxy? webProxy = proxy is null ? null : new WebProxy(proxy);
        Assert.Equal(expected, RegistryClient.IsAddressCheckRequired(new HttpRequestMessage(HttpMethod.Get, requestUri), connectHost, trustedHost, webProxy));
    }

    /// <summary>An IWebProxy that answers "connect directly" by returning the destination itself.</summary>
    private sealed class DestinationProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }
        public Uri GetProxy(Uri destination) => destination;
        public bool IsBypassed(Uri host) => false;
    }

    [Fact]
    public void Guard_checks_a_direct_connection_reported_by_a_destination_returning_proxy()
        => Assert.True(RegistryClient.IsAddressCheckRequired(new HttpRequestMessage(HttpMethod.Get, "https://internal.corp/x"), "internal.corp", null, new DestinationProxy()));

    [Fact]
    public void Guard_skips_the_handlers_CONNECT_to_its_proxy_but_not_a_CONNECT_elsewhere()
    {
        var connect = new HttpRequestMessage(HttpMethod.Connect, "http://127.0.0.1:3128");
        Assert.False(RegistryClient.IsAddressCheckRequired(connect, "127.0.0.1", null, null));
        Assert.True(RegistryClient.IsAddressCheckRequired(connect, "10.0.0.9", null, null));
    }

    [Fact]
    public void Guard_checks_a_host_the_proxy_bypasses()
    {
        var proxy = new WebProxy("http://127.0.0.1:3128", BypassOnLocal: false, BypassList: new[] { @"auth\.example" });
        Assert.True(RegistryClient.IsAddressCheckRequired(new HttpRequestMessage(HttpMethod.Get, "https://auth.example/token"), "127.0.0.1", "reg.example", proxy));
    }

    [Fact]
    public async Task Guard_failure_surfaces_as_RegistryException()
    {
        var stub = new StubHttpHandler(_ => throw new HttpRequestException("wrapped", new RegistryException("Refusing to connect to 'x'.")));
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        RegistryException ex = await Assert.ThrowsAsync<RegistryException>(() => client.GetManifestAsync("repo", "latest"));
        Assert.Equal("Refusing to connect to 'x'.", ex.Message);
    }

    [Fact]
    public async Task Transport_failures_surface_as_RegistryException()
    {
        var stub = new StubHttpHandler(_ => throw new HttpRequestException("Connection refused (proxy.corp.example:8080)"));
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        RegistryException ex = await Assert.ThrowsAsync<RegistryException>(() => client.BlobExistsAsync("repo", AnyDigest));
        Assert.IsType<HttpRequestException>(ex.InnerException);
        Assert.Contains($"HEAD https://{EcrHost}/v2/repo/blobs/{AnyDigest} failed: Connection refused", ex.Message);
    }
}
