namespace Amazon.OCI.Builder.Registry;

/// <summary>
/// The base type for errors raised while interacting with a container registry — authentication,
/// malformed responses, failed requests, or higher-level build problems such as a missing platform.
/// </summary>
public class RegistryException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    public RegistryException(string message) : base(message) { }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public RegistryException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Thrown when a registry's authentication challenge or token response is malformed or unsafe.</summary>
public sealed class InvalidAuthResponseException : RegistryException
{
    /// <summary>The registry that produced the invalid response.</summary>
    public string RegistryName { get; }

    /// <summary>Creates the exception.</summary>
    public InvalidAuthResponseException(string registryName, string message)
        : base($"Invalid authentication response from registry '{registryName}': {message}")
        => RegistryName = registryName;
}

/// <summary>Thrown when the client cannot authenticate to, or is denied access by, a registry.</summary>
public sealed class RegistryAuthenticationException : RegistryException
{
    /// <summary>The registry that denied access.</summary>
    public string RegistryName { get; }

    /// <summary>Creates the exception.</summary>
    public RegistryAuthenticationException(string registryName, string message)
        : base($"Unable to authenticate to registry '{registryName}': {message}")
        => RegistryName = registryName;
}

/// <summary>Thrown when a registry request fails with an unexpected HTTP status.</summary>
public sealed class RegistryRequestException : RegistryException
{
    /// <summary>The HTTP status code returned by the registry.</summary>
    public int StatusCode { get; }

    /// <summary>Creates the exception.</summary>
    public RegistryRequestException(int statusCode, string message)
        : base(message)
        => StatusCode = statusCode;
}

/// <summary>
/// Thrown when downloaded content does not hash to its expected digest — i.e. a content-integrity check failed.
/// This indicates the registry, a cache/CDN, or the transport returned bytes that do not match the
/// content-addressable digest they were requested under, and the content must not be trusted.
/// </summary>
public sealed class DigestMismatchException : RegistryException
{
    /// <summary>The digest the content was expected to have.</summary>
    public string ExpectedDigest { get; }

    /// <summary>The digest actually computed over the downloaded bytes.</summary>
    public string ActualDigest { get; }

    /// <summary>Creates the exception.</summary>
    public DigestMismatchException(string what, string expectedDigest, string actualDigest)
        : base($"Integrity check failed for {what}: expected {expectedDigest} but the downloaded content hashed to {actualDigest}.")
    {
        ExpectedDigest = expectedDigest;
        ActualDigest = actualDigest;
    }
}
