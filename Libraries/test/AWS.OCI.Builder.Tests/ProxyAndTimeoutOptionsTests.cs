using System.Net;
using System.Net.Sockets;
using System.Text;
using AWS.OCI.Builder;
using AWS.OCI.Builder.Images;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>The proxy and timeout set in <see cref="BaseImagePullOptions"/> and <see cref="EcrPushOptions"/>.</summary>
public class ProxyAndTimeoutOptionsTests
{
    private const string EcrHost = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
    private static readonly string FakeEcrToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("AWS:fake-password"));

    /// <summary>
    /// Accepts one connection on a loopback port, returns its first request line, and refuses the tunnel. No request
    /// leaves the machine.
    /// </summary>
    private static async Task<string?> RefuseOneTunnelAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using TcpClient accepted = await listener.AcceptTcpClientAsync(cancellationToken);
        using var reader = new StreamReader(accepted.GetStream(), Encoding.ASCII, leaveOpen: true);
        string? requestLine = await reader.ReadLineAsync(cancellationToken);
        byte[] reply = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n");
        await accepted.GetStream().WriteAsync(reply, cancellationToken);
        return requestLine;
    }

    private static (TcpListener Listener, WebProxy Proxy) StartFakeProxy()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return (listener, new WebProxy($"http://127.0.0.1:{port}"));
    }

    [Fact]
    public async Task Pull_sends_requests_through_the_proxy_in_the_pull_options()
    {
        (TcpListener listener, WebProxy proxy) = StartFakeProxy();
        using var _ = listener;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        Task<OciImageBuilder> pull = OciImageBuilder.FromBaseImageAsync(
            "public.ecr.aws/docker/library/alpine:3.20", "linux/amd64", new BaseImagePullOptions { Proxy = proxy }, cts.Token);

        Assert.Equal("CONNECT public.ecr.aws:443 HTTP/1.1", await RefuseOneTunnelAsync(listener, cts.Token));
        await Assert.ThrowsAsync<RegistryException>(() => pull);
    }

    [Fact]
    public async Task Pull_requests_time_out_after_the_timeout_in_the_pull_options()
    {
        (TcpListener listener, WebProxy proxy) = StartFakeProxy();
        using var _ = listener;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        // The proxy accepts the connection and never answers, so only the client timeout ends the request.
        Task<OciImageBuilder> pull = OciImageBuilder.FromBaseImageAsync(
            "public.ecr.aws/docker/library/alpine:3.20",
            "linux/amd64",
            new BaseImagePullOptions { Proxy = proxy, Timeout = TimeSpan.FromMilliseconds(300) },
            cts.Token);
        using TcpClient silent = await listener.AcceptTcpClientAsync(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pull);
        Assert.False(cts.IsCancellationRequested, "the request ended on the caller's token, not the timeout");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task Pull_rejects_a_timeout_that_is_not_positive(int milliseconds)
    {
        var options = new BaseImagePullOptions { Timeout = TimeSpan.FromMilliseconds(milliseconds) };
        await Assert.ThrowsAsync<ArgumentException>(
            () => OciImageBuilder.FromBaseImageAsync("public.ecr.aws/docker/library/alpine:3.20", "linux/amd64", options));
    }

    [Fact]
    public async Task Push_sends_requests_through_the_proxy_in_the_push_options()
    {
        const string configJson = """{"architecture":"amd64","os":"linux","rootfs":{"type":"layers","diff_ids":[]},"config":{}}""";
        const string manifestJson = """{"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json","config":{"mediaType":"application/vnd.oci.image.config.v1+json","size":10,"digest":"sha256:cfg"},"layers":[]}""";
        ImageBuilder imageBuilder = ImageBuilder.FromBaseImage(configJson, manifestJson, MediaTypes.OciManifestV1);
        var unusedSource = new RegistryClient("registry.example", new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));
        using OciImageBuilder builder = OciImageBuilder.CreateForTest(
            ImageReference.Parse("registry.example/base:1.0"), unusedSource, imageBuilder, Array.Empty<Descriptor>());

        (TcpListener listener, WebProxy proxy) = StartFakeProxy();
        using var _ = listener;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        Task<string> push = builder.PushToEcrAsync($"{EcrHost}/myapp:latest", FakeEcrToken, new EcrPushOptions { Proxy = proxy }, cts.Token);

        Assert.Equal($"CONNECT {EcrHost}:443 HTTP/1.1", await RefuseOneTunnelAsync(listener, cts.Token));
        await Assert.ThrowsAsync<RegistryException>(() => push);
    }
}
