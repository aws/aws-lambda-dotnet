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

/// <summary>The polling progress sink and the volume of events a push reports.</summary>
public class LayerPushProgressQueueTests
{
    private const string EcrHost = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
    private static readonly string AnyDigest = "sha256:" + new string('a', 64);

    [Fact]
    public void ProgressQueue_takes_concurrent_reports_without_loss()
    {
        var queue = new LayerPushProgressQueue();
        const int threads = 8, perThread = 5000;
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            for (int i = 0; i < perThread; i++)
            {
                queue.Report(new LayerPushProgress { LayerIndex = t, BytesPushed = i, Digest = AnyDigest });
            }
        });

        Assert.Equal(threads * perThread, queue.Count);
        var lastByThread = new long[threads];
        Array.Fill(lastByThread, -1);
        int read = 0;
        while (queue.TryDequeue(out LayerPushProgress p))
        {
            Assert.True(p.BytesPushed > lastByThread[p.LayerIndex]); // each reporter's order is kept
            lastByThread[p.LayerIndex] = p.BytesPushed;
            read++;
        }
        Assert.Equal(threads * perThread, read);
    }

    [Theory]
    [InlineData(10 * 1024 * 1024)]
    [InlineData(100 * 1024 * 1024)]
    public async Task Upload_progress_volume(int size)
    {
        // How many Uploading events one layer produces: the queue holds one per read of the layer stream.
        var stub = new StubHttpHandler(r =>
        {
            if (r.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (r.Method == HttpMethod.Patch) r.Content!.ReadAsStream().CopyTo(Stream.Null);
            var response = new HttpResponseMessage(r.Method == HttpMethod.Put ? HttpStatusCode.Created : HttpStatusCode.Accepted);
            response.Headers.Location = new Uri($"https://{EcrHost}/v2/repo/blobs/uploads/u1");
            return response;
        });
        var client = new RegistryClient(EcrHost, new HttpClient(stub));
        byte[] blob = new byte[size];
        int calls = 0;
        await client.UploadBlobAsync("repo", RegistryClient.ComputeDigest(blob), new MemoryStream(blob), onBytesSent: _ => calls++, contentLength: size);
        // One event per read of the layer stream; the read size is the runtime's (128 KiB measured on .NET 10).
        Assert.InRange(size / calls, 16 * 1024, 1024 * 1024);
    }
}
