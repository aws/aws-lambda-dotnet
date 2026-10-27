using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;

namespace Amazon.OCI.Builder.Registry;

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
public sealed partial class AuthHandshakeHandler : DelegatingHandler
{
    private const string BasicScheme = "Basic";
    private const string BearerScheme = "Bearer";

    private readonly IRegistryCredentialSource _credentialSource;

    // Cached authorization header per registry host, with the time it stops being valid.
    private readonly ConcurrentDictionary<string, CachedAuth> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the handler.</summary>
    /// <param name="credentialSource">Supplies credentials for each registry host.</param>
    /// <param name="innerHandler">The inner handler (typically an <see cref="HttpClientHandler"/>).</param>
    public AuthHandshakeHandler(IRegistryCredentialSource credentialSource, HttpMessageHandler innerHandler)
        : base(innerHandler)
        => _credentialSource = credentialSource ?? throw new ArgumentNullException(nameof(credentialSource));

    private readonly record struct CachedAuth(AuthenticationHeaderValue Header, DateTimeOffset Expiry);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new ArgumentException("The request must have an absolute RequestUri.", nameof(request));
        }

        string registryHost = request.RequestUri.Host;

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

        // Drain the 401 body so the connection is freed for the token request.
        _ = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        response.Dispose();

        (AuthenticationHeaderValue header, DateTimeOffset expiry) =
            await AuthenticateAsync(registryHost, scheme!, authInfo, cancellationToken).ConfigureAwait(false);

        _cache[registryHost] = new CachedAuth(header, expiry);

        // Retry the original request with the authorization header.
        HttpRequestMessage retry = await CloneAsync(request).ConfigureAwait(false);
        retry.Headers.Authorization = header;
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(AuthenticationHeaderValue, DateTimeOffset)> AuthenticateAsync(
        string registryHost, string scheme, AuthInfo? authInfo, CancellationToken cancellationToken)
    {
        BasicRegistryCredentials? creds =
            await _credentialSource.GetCredentialsAsync(registryHost, cancellationToken).ConfigureAwait(false);

        if (scheme.Equals(BasicScheme, StringComparison.OrdinalIgnoreCase))
        {
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
            return await GetBearerTokenAsync(registryHost, info, creds, cancellationToken).ConfigureAwait(false);
        }

        throw new RegistryAuthenticationException(registryHost, $"unsupported authentication scheme '{scheme}'.");
    }

    private async Task<(AuthenticationHeaderValue, DateTimeOffset)> GetBearerTokenAsync(
        string registryHost, AuthInfo info, BasicRegistryCredentials? creds, CancellationToken cancellationToken)
    {
        // SSRF guard: never send credentials to an unvalidated realm.
        Uri realm = RealmValidator.Validate(info.Realm, registryHost);

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
        await using (Stream s = await tokenResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            token = await JsonSerializer.DeserializeAsync(s, Images.OciJsonContext.TokenResponseInfo, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private static Dictionary<string, string> ParseBearerParameters(string? parameters)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters is null)
        {
            return result;
        }

        foreach (Match match in BearerParameterSplitter().Matches(parameters))
        {
            result[match.Groups["key"].Value] = match.Groups["value"].Value;
        }
        return result;
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };

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
                replayContent = new StreamContent(contentStream);
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

    [GeneratedRegex("(?<key>\\w+)=\"(?<value>[^\"]*)\"(?:,|$)")]
    private static partial Regex BearerParameterSplitter();

    /// <summary>Parsed <c>Bearer</c> challenge parameters.</summary>
    private sealed record AuthInfo(string Realm, string? Service, string? Scope);
}
