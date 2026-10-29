using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace AWS.OCI.Builder.Registry;

/// <summary>
/// A client for the OCI Distribution / Docker Registry HTTP API (v2): pull and push of manifests and blobs.
/// </summary>
/// <remarks>
/// <para>
/// One instance targets one registry host. Authentication is handled transparently by an
/// <see cref="AuthHandshakeHandler"/> wrapped around the supplied <see cref="HttpClient"/>, so callers do not
/// deal with tokens directly.
/// </para>
/// <para>
/// Blob uploads send the whole blob in a single <c>PATCH</c> (see <see cref="UploadBlobAsync"/>). Amazon ECR does
/// not honor <c>Content-Range</c> offsets across multiple <c>PATCH</c>es to one upload session, so chunked uploads
/// are deliberately avoided.
/// </para>
/// </remarks>
internal sealed class RegistryClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _registryHost;
    private readonly bool _ownsHttpClient;

    /// <summary>Creates a client for <paramref name="registryHost"/> using the supplied authenticating client.</summary>
    /// <param name="registryHost">The registry host, for example <c>registry-1.docker.io</c> or an ECR endpoint.</param>
    /// <param name="httpClient">
    /// An <see cref="HttpClient"/> whose handler chain includes an <see cref="AuthHandshakeHandler"/>.
    /// Use <see cref="Create"/> to build one correctly. The caller owns and must dispose a client passed here.
    /// </param>
    public RegistryClient(string registryHost, HttpClient httpClient)
    {
        _registryHost = registryHost ?? throw new ArgumentNullException(nameof(registryHost));
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = false;
    }

    private RegistryClient(string registryHost, HttpClient httpClient, bool ownsHttpClient)
    {
        _registryHost = registryHost;
        _http = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    /// <summary>The registry host this client targets.</summary>
    public string RegistryHost => _registryHost;

    /// <summary>
    /// Builds a <see cref="RegistryClient"/> whose HTTP pipeline performs the authentication handshake using
    /// <paramref name="credentialSource"/>.
    /// </summary>
    /// <param name="registryHost">The registry host this client targets.</param>
    /// <param name="credentialSource">Supplies credentials for the authentication handshake.</param>
    /// <param name="timeout">
    /// The <see cref="HttpClient.Timeout"/>. Defaults to <see cref="Timeout.InfiniteTimeSpan"/> so a large single-
    /// PATCH layer upload is bounded by the caller's cancellation token rather than cancelled by the 100-second
    /// client default. Must be positive or <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </param>
    /// <param name="proxy">An explicit proxy, or <see langword="null"/> for the system default proxy.</param>
    /// <param name="resolveHost">Test-only: replaces DNS resolution in the connect guard.</param>
    public static RegistryClient Create(
        string registryHost,
        IRegistryCredentialSource credentialSource,
        TimeSpan? timeout = null,
        IWebProxy? proxy = null,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
    {
        // Automatic redirects are DISABLED: the same transport also carries the token requests issued by
        // AuthHandshakeHandler, and an auto-followed redirect from a validated public realm to a private/loopback
        // endpoint would bypass RealmValidator (an SSRF vector). Blob downloads — which legitimately redirect to
        // object storage — follow redirects explicitly in GetBlobAsync, validating each hop.
        var inner = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = false,
            // SSRF defense at the socket layer: resolve the target, reject any blocked (loopback/private/link-local)
            // address, and connect to the SAME address we validated — so a DNS name that resolves to a private
            // address is refused, and DNS rebinding cannot swap in a different address between check and connect.
            // An Amazon ECR registry is trusted even on a private address, which is how ECR resolves through an
            // interface VPC endpoint. Any other host, including a caller-supplied base registry, is checked.
            ConnectCallback = (context, cancellationToken) =>
                GuardedConnectAsync(context, TrustedHostOf(registryHost), proxy ?? HttpClient.DefaultProxy, resolveHost, cancellationToken),
        };
        if (proxy is not null)
        {
            inner.Proxy = proxy;
        }
        var authHandler = new AuthHandshakeHandler(credentialSource, inner);
        var http = new HttpClient(authHandler)
        {
            // No client-imposed timeout by default: a whole layer is sent in one PATCH, so the 100s default would
            // cancel large uploads on slow links. The caller's cancellation token is the intended deadline.
            Timeout = timeout ?? Timeout.InfiniteTimeSpan,
        };
        return new RegistryClient(registryHost, http, ownsHttpClient: true);
    }

    /// <summary>
    /// A <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves the target host, refuses to connect to any
    /// blocked (loopback/private/link-local) address, and connects to the exact address it validated. Because the
    /// check and the connect use the same resolved address, this is robust against DNS rebinding — unlike a
    /// separate pre-resolution that the socket could override with a second lookup.
    /// </summary>
    private static async ValueTask<Stream> GuardedConnectAsync(
        SocketsHttpConnectionContext context,
        string? trustedHost,
        IWebProxy? proxy,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost,
        CancellationToken cancellationToken)
    {
        string host = context.DnsEndPoint.Host;
        int port = context.DnsEndPoint.Port;

        // When a proxy is configured the handler connects to the proxy, not to the request's host. The proxy is
        // the caller's own configuration and is often on a loopback or private address, and the target's address
        // is resolved by the proxy, out of this callback's sight. So the address check applies only to direct
        // connections, and not to the trusted ECR registry.
        bool checkAddress = IsAddressCheckRequired(context.InitialRequestMessage, host, trustedHost, proxy);

        IPAddress[] addresses = IPAddress.TryParse(host, out IPAddress? literal)
            ? [literal]
            : await (resolveHost ?? System.Net.Dns.GetHostAddressesAsync)(host, cancellationToken).ConfigureAwait(false);

        // Try each resolved address in order, skipping blocked ones, connecting to the first that is allowed AND
        // reachable. A blocked address is never dialed.
        System.Net.Sockets.Socket? socket = null;
        Exception? lastError = null;
        foreach (IPAddress address in addresses)
        {
            if (checkAddress && RealmValidator.IsBlockedAddress(address))
            {
                lastError = new RegistryException(
                    $"Refusing to connect to '{host}': it resolves to a blocked (loopback/private/link-local) address '{address}'.");
                continue;
            }

            var attempt = new System.Net.Sockets.Socket(address.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp)
            {
                NoDelay = true,
            };
            try
            {
                await attempt.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
                socket = attempt;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                attempt.Dispose();
            }
        }

        if (socket is null)
        {
            throw lastError ?? new RegistryException($"Could not connect to '{host}:{port}'.");
        }

        return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
    }

    /// <summary>
    /// Whether a connection to <paramref name="connectHost"/> must pass the blocked-address check. It is skipped
    /// for the trusted ECR registry (<paramref name="trustedHost"/>, or <see langword="null"/> when the client's
    /// registry is not ECR) and for a proxy: the target of the handler's CONNECT tunnel request, or the proxy that
    /// <paramref name="proxy"/> selects for <paramref name="request"/>. It is required for every other host.
    /// </summary>
    internal static bool IsAddressCheckRequired(HttpRequestMessage request, string connectHost, string? trustedHost, IWebProxy? proxy)
    {
        string host = NormalizeHost(connectHost);
        if (trustedHost is not null && string.Equals(host, trustedHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Uri? requestUri = request.RequestUri;

        // An https request through a proxy opens its connection with the handler's own CONNECT request, which is
        // addressed to the proxy. Only the handler sends CONNECT, and only to a proxy it was configured with.
        if (request.Method == HttpMethod.Connect && requestUri is not null
            && string.Equals(host, NormalizeHost(requestUri.IdnHost), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (requestUri is not null && proxy is not null && !proxy.IsBypassed(requestUri)
            && proxy.GetProxy(requestUri) is { } proxyUri
            // Some IWebProxy implementations return the destination itself to mean "connect directly".
            && !string.Equals(NormalizeHost(proxyUri.IdnHost), NormalizeHost(requestUri.IdnHost), StringComparison.OrdinalIgnoreCase)
            && string.Equals(host, NormalizeHost(proxyUri.IdnHost), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    /// <summary>Strips IPv6 brackets and a trailing dot so host spellings compare equal.</summary>
    private static string NormalizeHost(string host)
        => TrimTrailingDot(host.Length > 1 && host[0] == '[' && host[^1] == ']' ? host[1..^1] : host);

    /// <summary>
    /// The host name (no port, no trailing dot) of the registry this client was created for, when it is an Amazon
    /// ECR registry; otherwise <see langword="null"/>, so no host is exempt from the address check.
    /// </summary>
    internal static string? TrustedHostOf(string registryHost)
    {
        // Match the ECR pattern against the host alone, so an explicit port (for example ':443') doesn't hide it.
        if (!Uri.TryCreate($"https://{registryHost}", UriKind.Absolute, out Uri? uri))
        {
            return null;
        }
        string host = NormalizeHost(uri.IdnHost);
        return ImageReference.IsEcrHost(host) ? host : null;
    }

    private static string TrimTrailingDot(string host)
        => host.Length > 1 && host[^1] == '.' ? host[..^1] : host;

    /// <summary>
    /// Sends a request, reporting a failure to send it or to receive the response headers (DNS, connection, TLS,
    /// proxy, or the connect guard) as a <see cref="RegistryException"/> with the <see cref="HttpRequestException"/>
    /// as its inner exception. Not covered: an <see cref="IOException"/> while a blob body is being read, and the
    /// <see cref="TaskCanceledException"/> raised when <see cref="EcrPushOptions.UploadTimeout"/> or the caller's
    /// token expires.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            string message = ex.InnerException is RegistryException inner
                ? inner.Message
                : $"{request.Method} {request.RequestUri?.GetLeftPart(UriPartial.Path)} failed: {ex.Message}";
            throw new RegistryException(message, ex);
        }
    }

    private Uri Base => new($"https://{_registryHost}/v2/");

    // ---- Manifests ----------------------------------------------------------------------------

    /// <summary>
    /// Fetches a manifest (or manifest list / image index) by tag or digest reference. The returned
    /// <see cref="ManifestResponse"/> carries the raw JSON, its media type, and the server-reported digest.
    /// </summary>
    public async Task<ManifestResponse> GetManifestAsync(string repository, string reference, CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        ValidateReference(reference);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Base, $"{repository}/manifests/{reference}"));
        AddManifestAcceptHeaders(request);

        using HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"GET manifest {repository}:{reference}", cancellationToken).ConfigureAwait(false);

        // Read the exact bytes on the wire. Digests must be computed over these, not over a string that has been
        // decoded and re-encoded (which could differ by BOM/charset), so the raw bytes are exposed for verification.
        // The read is bounded so a hostile registry cannot exhaust memory with an enormous "manifest".
        (byte[] raw, bool exceeded) = await BoundedRead.ReadAsync(response.Content, BoundedRead.MaxMetadataBytes, cancellationToken).ConfigureAwait(false);
        if (exceeded)
        {
            throw new RegistryException(
                $"The manifest {repository}:{reference} is larger than the {BoundedRead.MaxMetadataBytes}-byte limit for manifests.");
        }
        string json = System.Text.Encoding.UTF8.GetString(raw);
        string mediaType = response.Content.Headers.ContentType?.MediaType
                           ?? throw new RegistryRequestException((int)response.StatusCode, "manifest response had no Content-Type.");
        string? digest = response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

        return new ManifestResponse(json, raw, mediaType, digest);
    }

    /// <summary>Checks whether a manifest exists for the given reference without downloading it.</summary>
    public async Task<bool> ManifestExistsAsync(string repository, string reference, CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        ValidateReference(reference);

        using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(Base, $"{repository}/manifests/{reference}"));
        AddManifestAcceptHeaders(request);
        using HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>Uploads a manifest under the given tag or digest reference.</summary>
    public async Task PutManifestAsync(string repository, string reference, string manifestJson, string mediaType, CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        ValidateReference(reference);

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Base, $"{repository}/manifests/{reference}"))
        {
            Content = new StringContent(manifestJson),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        using HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"PUT manifest {repository}:{reference}", cancellationToken).ConfigureAwait(false);
    }

    // ---- Blobs --------------------------------------------------------------------------------

    /// <summary>Checks whether a blob with the given digest already exists in the repository.</summary>
    public async Task<bool> BlobExistsAsync(string repository, string digest, CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        ValidateDigest(digest);

        using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(Base, $"{repository}/blobs/{digest}"));
        using HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>Downloads a blob by digest as a stream. Disposing the returned stream also disposes the underlying HTTP response.</summary>
    /// <remarks>
    /// Auto-redirect is disabled on the transport (see <see cref="Create"/>), so redirects to object storage are
    /// followed here explicitly, up to <see cref="MaxBlobRedirects"/> hops. Each hop's target is validated against
    /// the blocked (loopback/private/link-local) networks so a redirect cannot bounce the download onto the host's
    /// private network, and the Authorization header is not carried to a different host.
    /// </remarks>
    public async Task<Stream> GetBlobAsync(string repository, string digest, CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        ValidateDigest(digest);

        Uri uri = new(Base, $"{repository}/blobs/{digest}");

        for (int hop = 0; ; hop++)
        {
            // The request is disposed before the next loop iteration, or (on the success path below) transferred
            // to the returned ResponseOwningStream so it outlives this method with the response.
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (Uri.Compare(uri, Base, UriComponents.HostAndPort, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
            {
                // Cross-host redirect hop (e.g. to object storage): never authenticate against the redirect
                // target, so a malicious registry cannot redirect to a host it controls and harvest credentials
                // via a 401 challenge. Presigned storage URLs carry their own authorization.
                request.Options.Set(AuthHandshakeHandler.SkipAuthentication, true);
            }
            HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                if (hop >= MaxBlobRedirects)
                {
                    response.Dispose();
                    request.Dispose();
                    throw new RegistryRequestException((int)response.StatusCode, $"blob download for {repository} {digest} exceeded {MaxBlobRedirects} redirects.");
                }

                Uri? next = response.Headers.Location;
                response.Dispose();
                request.Dispose();
                if (next is null)
                {
                    throw new RegistryRequestException(302, $"blob download for {repository} {digest} returned a redirect with no Location.");
                }
                if (!next.IsAbsoluteUri)
                {
                    next = new Uri(uri, next);
                }
                if (!next.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    throw new RegistryRequestException(302, $"blob download for {repository} {digest} redirected to a non-https URL.");
                }
                // SSRF guard: never follow a redirect onto a blocked (loopback/private/link-local) address.
                if (RealmValidator.IsBlockedHost(next))
                {
                    throw new RegistryRequestException(302, $"blob download for {repository} {digest} redirected to a blocked (loopback/private/link-local) host '{next.Host}'.");
                }
                uri = next;
                continue;
            }

            try
            {
                await EnsureSuccessAsync(response, $"GET blob {repository} {digest}", cancellationToken).ConfigureAwait(false);
                Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                // Hand ownership of the response (and its request) to the caller via the returned stream: disposing
                // the stream disposes the response, so the HttpResponseMessage/HttpRequestMessage are never leaked.
                return new ResponseOwningStream(body, response, request);
            }
            catch
            {
                response.Dispose();
                request.Dispose();
                throw;
            }
        }
    }

    private const int MaxBlobRedirects = 5;

    private static bool IsRedirect(HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    /// <summary>
    /// A read-through stream wrapper that owns the <see cref="HttpResponseMessage"/> (and the
    /// <see cref="HttpRequestMessage"/>) the content came from. Disposing this stream disposes the inner content
    /// stream and then the response and request, so callers that <c>using</c>/<c>await using</c> the stream
    /// returned by <see cref="GetBlobAsync"/> never leak the HTTP message pair.
    /// </summary>
    private sealed class ResponseOwningStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;
        private readonly HttpRequestMessage _request;

        public ResponseOwningStream(Stream inner, HttpResponseMessage response, HttpRequestMessage request)
        {
            _inner = inner;
            _response = response;
            _request = request;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => _inner.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
                _request.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
            _response.Dispose();
            _request.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts a cross-repository blob mount so an already-present base-image layer is not re-uploaded.
    /// Returns <see langword="true"/> when the registry mounted the blob (HTTP 201), <see langword="false"/>
    /// when it requires a full upload instead (HTTP 202).
    /// </summary>
    public async Task<bool> TryMountBlobAsync(string repository, string digest, string fromRepository, CancellationToken cancellationToken = default)
    {
        ValidateRepository(repository);
        ValidateDigest(digest);
        ValidateRepository(fromRepository, nameof(fromRepository));

        var uri = new Uri(Base, $"{repository}/blobs/uploads/?mount={Uri.EscapeDataString(digest)}&from={Uri.EscapeDataString(fromRepository)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        using HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            return true; // Mounted — no upload needed.
        }
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return false; // Not mounted; caller must upload.
        }

        await EnsureSuccessAsync(response, $"POST blob mount {repository} {digest}", cancellationToken).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Uploads a blob using the <c>POST</c>/<c>PATCH</c>/<c>PUT</c> flow, sending the whole blob in a single
    /// <c>PATCH</c>. Skips the upload entirely if the blob already exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The blob is sent as one <c>PATCH</c> rather than multiple 5&#160;MiB chunks. Amazon ECR does not honor the
    /// <c>Content-Range</c> offset across successive <c>PATCH</c>es to the same upload session — each is treated as
    /// writing from offset 0, so only the final chunk would survive and the commit would fail the digest check.
    /// A single <c>PATCH</c> of the full content is what ECR expects and is what registries accept generally.
    /// </para>
    /// <para>
    /// The request advertises a <c>Content-Length</c> (from <paramref name="contentLength"/>, or the stream's
    /// <see cref="Stream.Length"/> when it is seekable). This is important for memory: when the length is known
    /// <see cref="HttpClient"/> streams <paramref name="content"/> straight onto the socket, whereas without a
    /// known length it must buffer the <em>entire</em> body into memory first to compute the length — which would
    /// defeat the spill-to-disk download path by re-materializing a large layer in memory on the upload side.
    /// </para>
    /// </remarks>
    /// <param name="repository">The destination repository (for example <c>myapp</c>).</param>
    /// <param name="digest">The <c>sha256:...</c> digest of the (compressed) blob content.</param>
    /// <param name="content">The blob bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="onBytesSent">
    /// Optional callback invoked as bytes are read from <paramref name="content"/> and sent, receiving the
    /// cumulative number of bytes sent so far. Called on the thread driving the upload; keep it cheap and
    /// non-throwing. When the blob already exists no bytes are sent and this is not called.
    /// </param>
    /// <param name="contentLength">
    /// The exact length of <paramref name="content"/> in bytes, when the caller knows it. Supplying it lets the
    /// upload stream without buffering the whole body in memory. When <see langword="null"/>, a seekable stream's
    /// <see cref="Stream.Length"/> is used; a non-seekable stream of unknown length falls back to buffering.
    /// </param>
    public async Task UploadBlobAsync(
        string repository,
        string digest,
        Stream content,
        CancellationToken cancellationToken = default,
        Action<long>? onBytesSent = null,
        long? contentLength = null)
    {
        ValidateRepository(repository);
        ValidateDigest(digest);

        if (await BlobExistsAsync(repository, digest, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // 1. Start an upload session; the registry returns the upload URL in the Location header.
        Uri uploadUri = await StartUploadAsync(repository, cancellationToken).ConfigureAwait(false);

        // 2. PATCH the entire blob in one request. (See remarks: ECR mishandles multi-PATCH chunking.) When a
        //    progress callback is supplied, wrap the source so bytes are reported as the HTTP stack reads them.
        Stream patchContent = onBytesSent is null ? content : new ProgressReportingStream(content, onBytesSent);

        // Resolve the body length: prefer the caller-supplied value, else a seekable stream's REMAINING length
        // (Length - Position). StreamContent sends from the current position, so advertising the full Length for a
        // stream positioned past the start would claim more bytes than are actually sent and fail the upload.
        long? bodyLength = contentLength ?? (content.CanSeek ? content.Length - content.Position : null);

        using (var patch = new HttpRequestMessage(HttpMethod.Patch, uploadUri)
        {
            Content = new StreamContent(patchContent),
        })
        {
            patch.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            if (bodyLength is { } length)
            {
                // Advertising the length switches HttpClient from "buffer-to-measure" to streaming.
                patch.Content.Headers.ContentLength = length;
            }

            using HttpResponseMessage patchResponse = await SendAsync(patch, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(patchResponse, $"PATCH blob {repository}", cancellationToken).ConfigureAwait(false);

            // The registry hands back the upload URL to use for the commit.
            uploadUri = LocationFrom(patchResponse) ?? uploadUri;
        }

        // 3. Close the session with PUT ?digest=..., which commits the blob.
        Uri commitUri = AppendQuery(uploadUri, "digest", digest);
        using var put = new HttpRequestMessage(HttpMethod.Put, commitUri)
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        };
        put.Content.Headers.ContentLength = 0;
        using HttpResponseMessage putResponse = await SendAsync(put, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(putResponse, $"PUT blob commit {repository} {digest}", cancellationToken).ConfigureAwait(false);
    }

    private async Task<Uri> StartUploadAsync(string repository, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Base, $"{repository}/blobs/uploads/"));
        using HttpResponseMessage response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"POST blob upload start {repository}", cancellationToken).ConfigureAwait(false);

        return LocationFrom(response)
               ?? throw new RegistryRequestException((int)response.StatusCode, "the registry did not return an upload Location header.");
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static void AddManifestAcceptHeaders(HttpRequestMessage request)
    {
        foreach (string mediaType in new[]
        {
            MediaTypes.OciManifestV1,
            MediaTypes.OciImageIndexV1,
            MediaTypes.DockerManifestV2,
            MediaTypes.DockerManifestListV2,
        })
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        }
    }

    private Uri? LocationFrom(HttpResponseMessage response)
    {
        if (response.Headers.Location is not { } location)
        {
            return null;
        }
        // A registry may return a relative Location; resolve it against the registry base.
        return location.IsAbsoluteUri ? location : new Uri(new Uri($"https://{_registryHost}"), location);
    }

    private static Uri AppendQuery(Uri uri, string key, string value)
    {
        var builder = new UriBuilder(uri);
        string encoded = $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
        builder.Query = string.IsNullOrEmpty(builder.Query) ? encoded : builder.Query.TrimStart('?') + "&" + encoded;
        return builder.Uri;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = string.Empty;
        try
        {
            // Read only the start of the body: it is just context for the exception message, and an unbounded
            // read would let a hostile registry exhaust memory with a huge error response.
            (byte[] prefix, _) = await BoundedRead.ReadAsync(response.Content, BoundedRead.MaxErrorBodyBytes, cancellationToken).ConfigureAwait(false);
            body = System.Text.Encoding.UTF8.GetString(prefix);
        }
        catch
        {
            // Ignore body read failures; the status code is the primary signal.
        }

        throw new RegistryRequestException(
            (int)response.StatusCode,
            $"{operation} failed with {(int)response.StatusCode} {response.ReasonPhrase}. {Truncate(body, 512)}");
    }

    private static void ValidateRepository(string repository, string paramName = "repository")
    {
        if (!OciNames.IsValidRepository(repository))
        {
            throw new ArgumentException($"'{OciNames.ForDisplay(repository)}' is not a valid repository name.", paramName);
        }
    }

    private static void ValidateDigest(string digest)
    {
        if (!OciNames.IsValidDigest(digest))
        {
            throw new ArgumentException(
                $"'{OciNames.ForDisplay(digest)}' is not a valid digest; expected 'sha256:' followed by 64 lowercase hex characters.",
                nameof(digest));
        }
    }

    private static void ValidateReference(string reference)
    {
        if (!OciNames.IsValidReference(reference))
        {
            throw new ArgumentException(
                $"'{OciNames.ForDisplay(reference)}' is not a valid tag or digest.", nameof(reference));
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    /// <summary>Computes the <c>sha256:...</c> digest of a stream (used by callers constructing blobs).</summary>
    /// <remarks>
    /// The digest covers the bytes from the stream's <em>current position</em> to its end, and the stream is left
    /// positioned at the end — the same semantics as <see cref="SHA256.HashData(Stream)"/>. To digest a seekable
    /// stream's full content, set <see cref="Stream.Position"/> to <c>0</c> before calling. The position is not
    /// reset automatically, so callers can digest a suffix intentionally and non-seekable streams are supported.
    /// </remarks>
    /// <param name="content">The stream to hash, read from its current position to the end.</param>
    public static string ComputeDigest(Stream content)
    {
        byte[] hash = SHA256.HashData(content);
        // Convert.ToHexStringLower exists only on net9.0+, so lower-case explicitly for net8.0 compatibility.
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Computes the <c>sha256:...</c> digest of a byte buffer.</summary>
    public static string ComputeDigest(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that <paramref name="content"/> hashes to <paramref name="expectedDigest"/>, throwing
    /// <see cref="DigestMismatchException"/> otherwise. The comparison is case-insensitive on the hex and
    /// tolerant of an absent <c>sha256:</c> prefix on the expected value. Only the <c>sha256</c> algorithm is
    /// supported; an expected digest naming a different algorithm is rejected.
    /// </summary>
    /// <param name="what">A short description of the content, used in the error message (e.g. "config blob").</param>
    /// <param name="expectedDigest">The digest the content is expected to have (<c>sha256:&lt;hex&gt;</c>).</param>
    /// <param name="content">The downloaded bytes to verify.</param>
    public static void VerifyDigest(string what, string expectedDigest, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedDigest);

        int colon = expectedDigest.IndexOf(':');
        if (colon >= 0)
        {
            string algorithm = expectedDigest[..colon];
            if (!algorithm.Equals("sha256", StringComparison.OrdinalIgnoreCase))
            {
                throw new RegistryException(
                    $"Cannot verify {what}: unsupported digest algorithm '{algorithm}' (only sha256 is supported).");
            }
        }

        string actual = ComputeDigest(content);
        // Normalize both to "sha256:<lowerhex>" for comparison.
        string expectedNormalized = colon >= 0
            ? "sha256:" + expectedDigest[(colon + 1)..].ToLowerInvariant()
            : "sha256:" + expectedDigest.ToLowerInvariant();

        if (!actual.Equals(expectedNormalized, StringComparison.Ordinal))
        {
            throw new DigestMismatchException(what, expectedNormalized, actual);
        }
    }

    /// <summary>
    /// Downloads a blob by digest, buffers it fully, and verifies that the bytes hash to the requested digest
    /// before returning them. Use this (rather than <see cref="GetBlobAsync"/>) whenever the expected digest is
    /// known, so corrupted or tampered content is rejected rather than trusted.
    /// </summary>
    /// <param name="repository">The repository to read from.</param>
    /// <param name="digest">The expected <c>sha256:...</c> digest; the download is verified against it.</param>
    /// <param name="what">A short description used in any integrity error (e.g. "base layer").</param>
    /// <param name="expectedSize">
    /// The size declared by the manifest descriptor, if known. When supplied, the downloaded length must match it.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="DigestMismatchException">The downloaded bytes do not hash to <paramref name="digest"/>.</exception>
    /// <exception cref="BlobSizeMismatchException">The downloaded length differs from <paramref name="expectedSize"/>.</exception>
    public async Task<byte[]> GetVerifiedBlobBytesAsync(string repository, string digest, string what, long? expectedSize = null, CancellationToken cancellationToken = default)
    {
        if (expectedSize is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize), expectedSize, "The expected size must not be negative.");
        }

        byte[] bytes;
        await using (Stream stream = await GetBlobAsync(repository, digest, cancellationToken).ConfigureAwait(false))
        {
            if (expectedSize is { } limit)
            {
                // Stop as soon as the blob runs past its declared size, rather than buffering whatever the
                // registry sends.
                (bytes, bool exceeded) = await BoundedRead.ReadAsync(stream, limit, cancellationToken).ConfigureAwait(false);
                if (exceeded)
                {
                    throw BlobSizeMismatchException.LargerThanDeclared(what, digest, limit);
                }
            }
            else
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                bytes = buffer.ToArray();
            }
        }

        VerifyDigest(what, digest, bytes);
        VerifySize(what, digest, expectedSize, bytes.LongLength);
        return bytes;
    }

    /// <summary>
    /// Downloads a blob by digest to a temporary file on disk (rather than buffering it in memory), verifying its
    /// integrity twice: once against the bytes as they stream off the network, and again by re-reading the file
    /// from disk before it is handed back. Use this for large layers where an in-memory buffer would be a memory
    /// problem; small blobs should use <see cref="GetVerifiedBlobBytesAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second (re-read) verification is deliberate: the on-the-wire hash proves the <em>download</em> was not
    /// corrupted, but the bytes could still be corrupted <em>after</em> that by a faulty disk write (a bit flip,
    /// a torn or truncated write). Re-hashing the file as read back from disk — which is what will actually be
    /// uploaded — closes that window, so a corrupt spilled layer is rejected here rather than committed to the
    /// registry.
    /// </para>
    /// <para>
    /// The returned handle owns the temporary file and an open read-only stream positioned at the start, ready to
    /// pass to <see cref="UploadBlobAsync"/>. Disposing the handle closes the stream and deletes the temp file;
    /// the file is also cleaned up if verification fails.
    /// </para>
    /// </remarks>
    /// <param name="repository">The repository to read from.</param>
    /// <param name="digest">The expected <c>sha256:...</c> digest; both verifications check against it.</param>
    /// <param name="what">A short description used in any integrity error (e.g. "base layer").</param>
    /// <param name="expectedSize">
    /// The size declared by the manifest descriptor, if known. When supplied, the downloaded length must match it.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="DigestMismatchException">
    /// The downloaded bytes, or the bytes as read back from disk, do not hash to <paramref name="digest"/>.
    /// </exception>
    /// <exception cref="BlobSizeMismatchException">The downloaded length differs from <paramref name="expectedSize"/>.</exception>
    public async Task<VerifiedBlobFile> GetVerifiedBlobToFileAsync(string repository, string digest, string what, long? expectedSize = null, CancellationToken cancellationToken = default)
    {
        if (expectedSize is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize), expectedSize, "The expected size must not be negative.");
        }

        // A unique temp path under the system temp dir (honors TMPDIR). Created now so we can always clean it up.
        string tempPath = Path.Combine(Path.GetTempPath(), $"aws-oci-layer-{Guid.NewGuid():N}.tmp");

        try
        {
            // 1. Stream the blob to disk, hashing (and counting) the exact bytes written as we go.
            string downloadedDigest;
            long downloadedLength = 0;
            await using (Stream source = await GetBlobAsync(repository, digest, cancellationToken).ConfigureAwait(false))
            await using (var file = SpillFile.CreateWrite(tempPath))
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] rented = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
                try
                {
                    int read;
                    while ((read = await source.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        // Stop as soon as the blob runs past its declared size, so a lying registry cannot fill
                        // the disk. The catch below deletes the partial file.
                        if (expectedSize is { } limit && downloadedLength + read > limit)
                        {
                            throw BlobSizeMismatchException.LargerThanDeclared(what, digest, limit);
                        }

                        hasher.AppendData(rented, 0, read);
                        downloadedLength += read;
                        await file.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(rented);
                }

                // Push the OS buffers to disk before we re-read, so the re-read actually exercises the stored bytes.
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                file.Flush(flushToDisk: true);

                downloadedDigest = "sha256:" + Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            }

            VerifyDigest(what, digest, downloadedDigest);
            VerifySize(what, digest, expectedSize, downloadedLength);

            // 2. Re-read the file from disk and verify again — catches a faulty disk write that corrupted the
            //    spilled bytes after the download-side hash.
            await VerifyFileDigestAsync(what + " (spilled to disk)", digest, tempPath, cancellationToken).ConfigureAwait(false);

            // 3. Hand back an open read stream over the verified file. The handle owns deletion.
            var readStream = new FileStream(
                tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
            return new VerifiedBlobFile(tempPath, readStream);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Verifies that a downloaded blob's length matches the size its manifest descriptor declared, throwing
    /// <see cref="BlobSizeMismatchException"/> on mismatch. A wrong declared size would otherwise surface much
    /// later as an opaque Content-Length transport error during the push, or as a manifest referencing an
    /// incorrect size. No check is made when <paramref name="expectedSize"/> is <see langword="null"/>.
    /// </summary>
    internal static void VerifySize(string what, string digest, long? expectedSize, long actualSize)
    {
        if (expectedSize is { } declared && declared != actualSize)
        {
            throw new BlobSizeMismatchException(what, digest, declared, actualSize);
        }
    }

    /// <summary>
    /// Verifies that an expected digest matches an already-computed digest string (both normalized), throwing
    /// <see cref="DigestMismatchException"/> on mismatch. Used when the content was hashed incrementally while
    /// streaming, so there is no in-memory buffer to re-hash.
    /// </summary>
    private static void VerifyDigest(string what, string expectedDigest, string actualDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedDigest);

        int colon = expectedDigest.IndexOf(':');
        if (colon >= 0 && !expectedDigest[..colon].Equals("sha256", StringComparison.OrdinalIgnoreCase))
        {
            throw new RegistryException(
                $"Cannot verify {what}: unsupported digest algorithm '{expectedDigest[..colon]}' (only sha256 is supported).");
        }

        string expectedNormalized = colon >= 0
            ? "sha256:" + expectedDigest[(colon + 1)..].ToLowerInvariant()
            : "sha256:" + expectedDigest.ToLowerInvariant();

        if (!actualDigest.Equals(expectedNormalized, StringComparison.Ordinal))
        {
            throw new DigestMismatchException(what, expectedNormalized, actualDigest);
        }
    }

    /// <summary>Hashes a file's contents from disk and verifies the digest, without buffering the whole file.</summary>
    private static async Task VerifyFileDigestAsync(string what, string expectedDigest, string path, CancellationToken cancellationToken)
    {
        string actual;
        await using (var file = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true))
        {
            byte[] hash = await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false);
            actual = "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
        }

        VerifyDigest(what, expectedDigest, actual);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; a leftover temp file is not worth masking the original error.
        }
    }

    /// <summary>
    /// Disposes the underlying <see cref="HttpClient"/> only when this instance created it (via
    /// <see cref="Create"/>). A client supplied through the public constructor is left for its owner to dispose.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}

/// <summary>The result of fetching a manifest: its raw JSON, media type, and server-reported digest.</summary>
/// <param name="Json">The manifest JSON decoded as UTF-8 (convenient for parsing).</param>
/// <param name="RawBytes">The exact bytes received on the wire — digests must be computed over these.</param>
/// <param name="MediaType">The manifest media type (OCI or Docker; single image or index).</param>
/// <param name="Digest">The server-reported <c>Docker-Content-Digest</c>, if present.</param>
internal readonly record struct ManifestResponse(string Json, byte[] RawBytes, string MediaType, string? Digest);

/// <summary>
/// A verified blob that has been spilled to a temporary file on disk. Owns both the open read stream (positioned
/// at the start, ready to upload) and the temp file; disposing it closes the stream and deletes the file.
/// </summary>
internal sealed class VerifiedBlobFile : IAsyncDisposable, IDisposable
{
    private readonly string _path;
    private bool _disposed;

    internal VerifiedBlobFile(string path, FileStream stream)
    {
        _path = path;
        Stream = stream;
    }

    /// <summary>The open, read-only stream over the verified file, positioned at offset 0.</summary>
    public FileStream Stream { get; }

    /// <summary>The full path of the temporary file backing <see cref="Stream"/>.</summary>
    public string Path => _path;

    /// <summary>Closes the stream and deletes the temporary file.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await Stream.DisposeAsync().ConfigureAwait(false);
        Delete();
    }

    /// <summary>Closes the stream and deletes the temporary file.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stream.Dispose();
        Delete();
    }

    private void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

/// <summary>
/// A read-only pass-through stream that reports cumulative bytes read to a callback. Wrapping the upload source
/// in this lets the registry client surface byte-level upload progress: the HTTP stack reads from it to send the
/// PATCH body, and each read advances the reported total. The wrapped stream is not disposed by this wrapper —
/// its owner retains responsibility for it.
/// </summary>
internal sealed class ProgressReportingStream : Stream
{
    private readonly Stream _inner;
    private readonly Action<long> _onBytesRead;
    private long _totalRead;

    public ProgressReportingStream(Stream inner, Action<long> onBytesRead)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _onBytesRead = onBytesRead ?? throw new ArgumentNullException(nameof(onBytesRead));
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, count);
        Report(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer);
        Report(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Report(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        Report(read);
        return read;
    }

    private void Report(int read)
    {
        if (read <= 0)
        {
            return;
        }
        _totalRead += read;
        _onBytesRead(_totalRead);
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() => _inner.Flush();
}
