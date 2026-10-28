using System.Text.Json.Serialization;

namespace AWS.OCI.Builder.Registry;

/// <summary>The JSON body returned by a registry token endpoint during the Bearer auth handshake.</summary>
/// <remarks>
/// Internal and top-level (rather than nested in <see cref="AuthHandshakeHandler"/>) so the source-generated
/// <see cref="Images.OciJsonContext"/> can reference it — a source-gen context cannot target a private nested type.
/// </remarks>
internal sealed record TokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; init; }

    [JsonPropertyName("issued_at")]
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>The usable token (registries populate either <c>token</c> or <c>access_token</c>).</summary>
    public string? ResolvedToken => Token ?? AccessToken;

    /// <summary>
    /// The computed expiry. Per the token spec, default to 60 seconds of validity when the endpoint
    /// omits <c>expires_in</c>; subtract a small safety margin so a cached token is refreshed before it
    /// actually expires.
    /// </summary>
    public DateTimeOffset ResolvedExpiration
    {
        get
        {
            DateTimeOffset issued = IssuedAt ?? DateTimeOffset.UtcNow;
            int validity = ExpiresIn ?? 60;
            // 10-second safety margin, floored so very short tokens still move forward in time.
            int margin = Math.Min(10, Math.Max(0, validity - 1));
            return issued.AddSeconds(validity - margin);
        }
    }
}
