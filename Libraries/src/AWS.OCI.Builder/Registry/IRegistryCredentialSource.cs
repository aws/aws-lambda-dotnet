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
public interface IRegistryCredentialSource
{
    /// <summary>
    /// Returns the credentials for <paramref name="registryHost"/>, or <see langword="null"/> to attempt
    /// anonymous access.
    /// </summary>
    /// <param name="registryHost">The registry host, for example <c>123456789012.dkr.ecr.us-west-2.amazonaws.com</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken cancellationToken = default);
}

/// <summary>A plain username/password pair used for registry authentication.</summary>
/// <param name="Username">The registry username.</param>
/// <param name="Password">The registry password or token.</param>
public readonly record struct BasicRegistryCredentials(string Username, string Password);

/// <summary>
/// A credential source that always returns <see langword="null"/>, causing the handshake to attempt
/// anonymous access. Suitable for pulling public base images (for example from
/// <c>mcr.microsoft.com</c> or Docker Hub).
/// </summary>
public sealed class AnonymousRegistryCredentialSource : IRegistryCredentialSource
{
    /// <inheritdoc />
    public Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken cancellationToken = default)
        => Task.FromResult<BasicRegistryCredentials?>(null);
}

/// <summary>
/// A credential source backed by a fixed username/password pair known up front (for example one decoded from an
/// Amazon ECR authorization token). It performs no network or credential resolution and has no dependency on the
/// AWS SDK; the caller is responsible for obtaining the token and keeping it fresh for the push's duration.
/// </summary>
public sealed class StaticRegistryCredentialSource : IRegistryCredentialSource
{
    private readonly BasicRegistryCredentials _credentials;

    /// <summary>Creates a source that returns the given username/password for every host.</summary>
    public StaticRegistryCredentialSource(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        _credentials = new BasicRegistryCredentials(username, password);
    }

    /// <inheritdoc />
    public Task<BasicRegistryCredentials?> GetCredentialsAsync(string registryHost, CancellationToken cancellationToken = default)
        => Task.FromResult<BasicRegistryCredentials?>(_credentials);
}
