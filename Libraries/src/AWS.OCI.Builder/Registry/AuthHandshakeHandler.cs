using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace AWS.OCI.Builder.Registry;

/// <summary>
/// A <see cref="DelegatingHandler"/> that performs the Docker/OCI registry authentication handshake.
/// </summary>
/// <remarks>
/// <para>
/// When a registry responds <c>401 Unauthorized</c> with a <c>WWW-Authenticate</c> challenge, this handler:
/// </para>
/// <list type="number">
///   <item>parses the challenge scheme (<c>Bearer</c> or <c>Basic</c>);</item>
///   <item>
///     for <c>Bearer</c>, validates the <c>realm</c> endpoint (see <see cref="RealmValidator"/>) and performs a
///     token <c>GET</c> against it, sending the resolved credentials as <c>Basic</c> auth and reading back a token;
///   </item>
///   <item>for <c>Basic</c>, encodes the resolved credentials directly;</item>
///   <item>caches the resulting authorization header per registry host (with expiry for Bearer tokens) and retries the request.</item>
/// </list>
/// <para>
/// Credentials come from an <see cref="IRegistryCredentialSource"/>. Anonymous access is attempted when the
/// source returns <see langword="null"/> (public base-image pulls do not need credentials to fetch a token).
/// </para>
/// </remarks>
internal sealed partial class AuthHandshakeHandler : DelegatingHandler
{
    private const string BasicScheme = "Basic";
    private const string BearerScheme = "Bearer";

    private readonly IRegistryCredentialSource _credentialSource;

    // Cached authorization header per registry host, with the time it stops being valid.
    private readonly ConcurrentDictionary<string, CachedAuth> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When set to <see langword="true"/> on a request's <see cref="HttpRequestMessage.Options"/>, this handler
    /// sends the request without attaching cached credentials and without answering a 401 challenge. Used for
    /// cross-host blob redirect hops so credentials are never presented to a redirect target.
    /// </summary>
    internal static readonly HttpRequestOptionsKey<bool> SkipAuthentication = new("AWS.OCI.Builder.SkipAuthentication");

    /// <summary>Creates the handler.</summary>
    /// <param name="credentialSource">Supplies credentials for each registry host.</param>
    /// <param name="innerHandler">The inner handler (typically an <see cref="HttpClientHandler"/>).</param>
    public AuthHandshakeHandler(IRegistryCredentialSource credentialSource, HttpMessageHandler innerHandler)
        : base(innerHandler)
        => _credentialSource = credentialSource ?? throw new ArgumentNullException(nameof(credentialSource));

    private readonly record struct CachedAuth(AuthenticationHeaderValue Header, DateTimeOffset Expiry)
    {
        // The header carries the credential (a bearer token or encoded username:password); never print it.
        public override string ToString() => $"{nameof(CachedAuth)} {{ Scheme = {Header.Scheme}, Expiry = {Expiry:O} }}";
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException("The request must have an absolute RequestUri.", nameof(request));
        }

        // Requests explicitly marked to skip authentication (e.g. blob-download redirect hops to a different host,
        // such as object storage) are sent as-is: no cached header is attached and a 401 challenge is NOT
        // answered. Otherwise a malicious registry could redirect a blob GET to a host it controls, reply 401
        // with a challenge, and harvest the credentials this handler would send in response.
        if (request.Options.TryGetValue(SkipAuthentication, out bool skip) && skip)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // Respect a caller-supplied Authorization header: send the request unchanged and let the caller handle
        // any 401, rather than overwriting their credentials with cached or handshake-derived ones.
        if (request.Headers.Authorization is not null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // Host plus port (Authority omits the default 443), so credentials and cached headers never carry over to the
        // same host name on a different port.
        string registryHost = request.RequestUri.Authority;

        // Apply a cached, still-valid authorization header if we have one.
        if (_cache.TryGetValue(registryHost, out CachedAuth cached) && cached.Expiry > DateTimeOffset.UtcNow)
        {
            request.Headers.Authorization = cached.Header;
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // 401 -> parse the challenge and perform the handshake.
        if (!TryParseChallenge(response, out string? scheme, out AuthInfo? authInfo))
        {
            return response; // No usable challenge; let the caller handle the 401.
        }

        // Drain (a bounded prefix of) the 401 body so the connection can be reused for the token request. The read
        // is capped because the body comes from the registry; anything beyond the cap is dropped with the response.
        _ = await BoundedRead.ReadAsync(response.Content, BoundedRead.MaxErrorBodyBytes, cancellationToken).ConfigureAwait(false);
        response.Dispose();

        (AuthenticationHeaderValue header, DateTimeOffset expiry) =
            await AuthenticateAsync(registryHost, scheme!, authInfo, cancellationToken).ConfigureAwait(false);

        _cache[registryHost] = new CachedAuth(header, expiry);

        // Retry the original request with the authorization header. The clone is disposed once sent; its content
        // never owns the caller's body stream (see CloneAsync), so disposing it cannot close that stream.
        using HttpRequestMessage retry = await CloneAsync(request).ConfigureAwait(false);
        retry.Headers.Authorization = header;
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(AuthenticationHeaderValue, DateTimeOffset)> AuthenticateAsync(
        string registryHost, string scheme, AuthInfo? authInfo, CancellationToken cancellationToken)
    {
        if (scheme.Equals(BasicScheme, StringComparison.OrdinalIgnoreCase))
        {
            // Basic credentials go to the registry host itself, so ask for that host's credentials.
            BasicRegistryCredentials? creds =
                await _credentialSource.GetCredentialsAsync(registryHost, cancellationToken).ConfigureAwait(false);
            if (creds is not { } basic)
            {
                throw new RegistryAuthenticationException(registryHost, "the registry requires Basic auth but no credentials were available.");
            }
            return (BasicHeader(basic), DateTimeOffset.MaxValue);
        }

        if (scheme.Equals(BearerScheme, StringComparison.OrdinalIgnoreCase))
        {
            if (authInfo is not { } info)
            {
                throw new InvalidAuthResponseException(registryHost, "the Bearer challenge did not include a realm.");
            }
            return await GetBearerTokenAsync(registryHost, info, cancellationToken).ConfigureAwait(false);
        }

        throw new RegistryAuthenticationException(registryHost, $"unsupported authentication scheme '{scheme}'.");
    }

    private async Task<(AuthenticationHeaderValue, DateTimeOffset)> GetBearerTokenAsync(
        string registryHost, AuthInfo info, CancellationToken cancellationToken)
    {
        // SSRF guard: never send credentials to an unvalidated realm.
        Uri realm = RealmValidator.Validate(info.Realm, registryHost);

        // The credentials are sent to the token endpoint, not the registry, so ask for the realm host's credentials.
        // A challenge pointing the realm at some other host then gets none, rather than the registry's.
        BasicRegistryCredentials? creds =
            await _credentialSource.GetCredentialsAsync(realm.Authority, cancellationToken).ConfigureAwait(false);

        var builder = new UriBuilder(realm);
        System.Collections.Specialized.NameValueCollection query = HttpUtility.ParseQueryString(realm.Query);
        if (info.Service is { } svc)
        {
            query["service"] = svc;
        }
        if (info.Scope is { } scope)
        {
            query["scope"] = scope;
        }
        builder.Query = query.ToString();

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, builder.Uri);
        if (creds is { } c)
        {
            // Even for a Bearer scheme, username/password are presented to the token endpoint as Basic auth.
            tokenRequest.Headers.Authorization = BasicHeader(c);
        }

        using HttpResponseMessage tokenResponse =
            await base.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new RegistryAuthenticationException(
                registryHost, $"the token endpoint returned {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase}.");
        }

        TokenResponse? token;
        (byte[] tokenBytes, bool exceeded) =
            await BoundedRead.ReadAsync(tokenResponse.Content, BoundedRead.MaxMetadataBytes, cancellationToken).ConfigureAwait(false);
        if (exceeded)
        {
            throw new InvalidAuthResponseException(
                registryHost, $"the token endpoint response is larger than the {BoundedRead.MaxMetadataBytes}-byte limit.");
        }
        try
        {
            token = JsonSerializer.Deserialize(tokenBytes, Images.OciJsonContext.TokenResponseInfo);
        }
        catch (JsonException)
        {
            token = null;
        }

        if (token is null || token.ResolvedToken is null)
        {
            throw new InvalidAuthResponseException(registryHost, "the token endpoint response could not be parsed into a token.");
        }

        return (new AuthenticationHeaderValue(BearerScheme, token.ResolvedToken), token.ResolvedExpiration);
    }

    private static AuthenticationHeaderValue BasicHeader(BasicRegistryCredentials c)
        => new(BasicScheme, Convert.ToBase64String(Encoding.ASCII.GetBytes($"{c.Username}:{c.Password}")));

    private static bool TryParseChallenge(HttpResponseMessage response, out string? scheme, out AuthInfo? authInfo)
    {
        scheme = null;
        authInfo = null;

        HttpHeaderValueCollection<AuthenticationHeaderValue> challenges = response.Headers.WwwAuthenticate;
        if (!challenges.Any())
        {
            return false;
        }

        // A registry may legally return multiple WWW-Authenticate challenges (RFC 7235) in an unspecified order,
        // so inspect every challenge rather than only the first. Prefer Bearer (token flow) over Basic; a Bearer
        // challenge is only usable if it carries a realm. Fall back to the first Basic challenge if no usable
        // Bearer is present.
        AuthenticationHeaderValue? basicChallenge = null;
        foreach (AuthenticationHeaderValue challenge in challenges)
        {
            if (challenge.Scheme is null)
            {
                continue;
            }

            if (challenge.Scheme.Equals(BearerScheme, StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> parsed = ParseBearerParameters(challenge.Parameter);
                if (parsed.TryGetValue("realm", out string? realm))
                {
                    parsed.TryGetValue("service", out string? service);
                    parsed.TryGetValue("scope", out string? scopeValue);
                    scheme = challenge.Scheme;
                    authInfo = new AuthInfo(realm, service, scopeValue);
                    return true;
                }
            }
            else if (basicChallenge is null && challenge.Scheme.Equals(BasicScheme, StringComparison.OrdinalIgnoreCase))
            {
                basicChallenge = challenge;
            }
        }

        if (basicChallenge is not null)
        {
            scheme = basicChallenge.Scheme;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses the <c>auth-param</c> list of a <c>WWW-Authenticate</c> challenge (RFC 7235 §2.1):
    /// <c>name = ( token / quoted-string )</c> pairs separated by commas, with optional whitespace around both
    /// the <c>=</c> and the <c>,</c>. Quoted values may contain commas and backslash-escaped characters; unquoted
    /// (token) values are also accepted. Names are matched case-insensitively; if a name repeats, the first wins.
    /// Malformed segments are skipped rather than aborting the whole parse.
    /// </summary>
    internal static Dictionary<string, string> ParseBearerParameters(string? parameters)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(parameters))
        {
            return result;
        }

        string s = parameters;
        int i = 0;
        int n = s.Length;

        while (i < n)
        {
            // Skip list separators and optional whitespace before the next parameter.
            while (i < n && (s[i] == ',' || IsOptionalWhitespace(s[i])))
            {
                i++;
            }
            if (i >= n)
            {
                break;
            }

            // Parameter name: one or more token characters.
            int nameStart = i;
            while (i < n && IsTokenChar(s[i]))
            {
                i++;
            }
            string name = s[nameStart..i];

            while (i < n && IsOptionalWhitespace(s[i]))
            {
                i++;
            }

            if (name.Length == 0 || i >= n || s[i] != '=')
            {
                // Not a name=value pair: skip to the next separator and carry on.
                SkipToNextSeparator(s, ref i);
                continue;
            }

            i++; // consume '='
            while (i < n && IsOptionalWhitespace(s[i]))
            {
                i++;
            }

            string value;
            if (i < n && s[i] == '"')
            {
                // quoted-string: read until the closing quote, honoring backslash escapes (quoted-pair).
                i++;
                var builder = new StringBuilder();
                while (i < n && s[i] != '"')
                {
                    if (s[i] == '\\' && i + 1 < n)
                    {
                        i++;
                    }
                    builder.Append(s[i]);
                    i++;
                }
                i++; // consume the closing quote (harmless if it was missing)
                value = builder.ToString();
            }
            else
            {
                // token value: read until a separator or whitespace.
                int valueStart = i;
                while (i < n && s[i] != ',' && !IsOptionalWhitespace(s[i]))
                {
                    i++;
                }
                value = s[valueStart..i];
            }

            result.TryAdd(name, value);
            SkipToNextSeparator(s, ref i);
        }

        return result;
    }

    private static void SkipToNextSeparator(string s, ref int i)
    {
        while (i < s.Length && s[i] != ',')
        {
            i++;
        }
    }

    private static bool IsOptionalWhitespace(char c) => c is ' ' or '\t';

    /// <summary>An RFC 7230 <c>tchar</c>: the characters allowed in a token such as a parameter name.</summary>
    private static bool IsTokenChar(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.'
            or '^' or '_' or '`' or '|' or '~';

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy,
        };

        // Carry request options across so the authenticated retry behaves exactly like the original request.
        IDictionary<string, object?> cloneOptions = clone.Options;
        foreach (KeyValuePair<string, object?> option in original.Options)
        {
            cloneOptions[option.Key] = option.Value;
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content is not null)
        {
            HttpContent replayContent;

            // Prefer replaying a SEEKABLE body by rewinding it — this is the real upload case (the PATCH body is a
            // StreamContent over a FileStream or MemoryStream), and rewinding avoids buffering the entire blob into
            // memory, which would defeat the spill-to-disk memory bound for large layers. Only a non-seekable body
            // of unknown provenance falls back to buffering (small control requests, never the large layer PATCH).
            Stream? contentStream = original.Content is StreamContent
                ? await original.Content.ReadAsStreamAsync().ConfigureAwait(false)
                : null;

            if (contentStream is { CanSeek: true })
            {
                contentStream.Position = 0;
                // The stream belongs to the original request (and ultimately the caller); wrap it so that
                // disposing the retry clone does not close it.
                replayContent = new StreamContent(new NonOwningStream(contentStream));
            }
            else
            {
                // Fallback: buffer. Reached only for non-seekable, typically tiny, bodies.
                byte[] bytes = await original.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                replayContent = new ByteArrayContent(bytes);
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in original.Content.Headers)
            {
                replayContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            clone.Content = replayContent;
        }

        return clone;
    }

    /// <summary>
    /// A pass-through stream that forwards everything to an inner stream but does NOT dispose it. Used for the
    /// retry clone's body so disposing the clone leaves the original request's stream — owned by the caller —
    /// open and usable.
    /// </summary>
    internal sealed class NonOwningStream : Stream
    {
        private readonly Stream _inner;

        public NonOwningStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Flush() => _inner.Flush();

        // Dispose is intentionally not overridden: the base implementation never touches _inner, which belongs to
        // the caller that owns the original request.
    }

    /// <summary>Parsed <c>Bearer</c> challenge parameters.</summary>
    private sealed record AuthInfo(string Realm, string? Service, string? Scope);
}
