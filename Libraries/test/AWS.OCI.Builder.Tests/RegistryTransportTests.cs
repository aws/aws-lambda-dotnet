using System.Net;
using System.Net.Sockets;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>How the registry client connects, and how transport failures surface.</summary>
public class RegistryTransportTests
{
    private const string EcrHost = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
    private static readonly string AnyDigest = "sha256:" + new string('a', 64);

    [Fact]
    public async Task A_registry_on_a_private_address_is_connected_to()
    {
        // A caller-named registry on a private address, such as an internal mirror or ECR through an interface VPC
        // endpoint, is dialed like any other host. A loopback listener stands in for it.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = RegistryClient.Create($"127.0.0.1:{port}", new AnonymousRegistryCredentialSource());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task call = client.GetManifestAsync("repo", "latest", cts.Token);

        using TcpClient accepted = await listener.AcceptTcpClientAsync(cts.Token); // the registry was dialed
        accepted.Close();

        // TLS then fails against the plain listener, reported as a RegistryException.
        var ex = await Assert.ThrowsAsync<RegistryException>(() => call);
        Assert.IsType<HttpRequestException>(ex.InnerException);
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
