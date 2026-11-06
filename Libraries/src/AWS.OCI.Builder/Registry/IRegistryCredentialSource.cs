namespace AWS.OCI.Builder.Registry;

/// <summary>
/// Supplies the username/password a registry's authentication handshake needs for a given registry host.
/// </summary>
/// <remarks>
/// This decouples the <see cref="AuthHandshakeHandler"/> from where credentials come from:
/// pushes to Amazon ECR use <see cref="StaticRegistryCredentialSource"/> (holding the username/password decoded
/// from a caller-supplied ECR authorization token), while pulls of public base images use
/// <see cref="AnonymousRegistryCredentialSource"/>.
/// </remarks>
internal interface IRegistryCredentialSource
{
    /// <summary>
    /// Returns the credentials for <paramref name="registryHost"/>, or <see langword="null"/> to attempt
    /// anonymous access. Return <see langword="null"/> for any host the credentials are not meant for.
    /// </summary>
    /// <param name="registryHost">
    /// The host the credentials would be sent to: the registry itself, or a Bearer token endpoint. It includes the
    /// port only when it is not 443, for example <c>123456789012.dkr.ecr.us-west-2.amazonaws.com</c>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken cancellationToken = default);
}

/// <summary>A plain username/password pair used for registry authentication.</summary>
/// <remarks><see cref="ToString"/> never includes the password.</remarks>
/// <param name="Username">The registry username.</param>
/// <param name="Password">The registry password or token.</param>
internal readonly record struct BasicRegistryCredentials(string Username, string Password)
{
    /// <summary>Returns the username with the password redacted.</summary>
    public override string ToString() => $"{nameof(BasicRegistryCredentials)} {{ Username = {Username}, Password = *** }}";
}

/// <summary>
/// A credential source that always returns <see langword="null"/>, causing the handshake to attempt
/// anonymous access. Suitable for pulling public base images (for example from
/// <c>mcr.microsoft.com</c> or Docker Hub).
/// </summary>
internal sealed class AnonymousRegistryCredentialSource : IRegistryCredentialSource
{
    /// <inheritdoc />
    public Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken cancellationToken = default)
        => Task.FromResult<BasicRegistryCredentials?>(null);
}

/// <summary>
/// A credential source backed by a fixed username/password pair for a single registry host (for example one decoded
/// from an Amazon ECR authorization token). It performs no network or credential resolution and has no dependency on
/// the AWS SDK; the caller is responsible for obtaining the token and keeping it fresh for the push's duration.
/// </summary>
/// <remarks>
/// The credentials are returned only for the host they were created for, and <see langword="null"/> for any other
/// host. A request that reaches an unexpected host (a forged upload location, a token endpoint on another host)
/// therefore never receives them.
/// </remarks>
internal sealed class StaticRegistryCredentialSource : IRegistryCredentialSource
{
    private readonly string _registryHost;
    private readonly BasicRegistryCredentials _credentials;

    /// <summary>Creates a source that returns the given username/password for <paramref name="registryHost"/> only.</summary>
    /// <param name="registryHost">
    /// The host the credentials belong to, with the port only when it is not 443, for example
    /// <c>123456789012.dkr.ecr.us-west-2.amazonaws.com</c>.
    /// </param>
    /// <param name="username">The registry username.</param>
    /// <param name="password">The registry password or token.</param>
    public StaticRegistryCredentialSource(string registryHost, string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryHost);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        _registryHost = registryHost;
        _credentials = new BasicRegistryCredentials(username, password);
    }

    /// <inheritdoc />
    public Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken cancellationToken = default)
        => Task.FromResult<BasicRegistryCredentials?>(
            string.Equals(registryHost, _registryHost, StringComparison.OrdinalIgnoreCase) ? _credentials : null);
}
