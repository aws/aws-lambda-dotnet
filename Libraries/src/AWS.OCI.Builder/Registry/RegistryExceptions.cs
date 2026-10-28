namespace AWS.OCI.Builder.Registry;

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

/// <summary>
/// Thrown when a downloaded blob's length does not match the size declared for it in a manifest descriptor.
/// The content may still hash to the expected digest — the descriptor itself is wrong — but pushing it would
/// fail (the upload's Content-Length could not match) or produce a manifest referencing an incorrect size, so
/// the mismatch is reported up front with a clear error instead.
/// </summary>
public sealed class BlobSizeMismatchException : RegistryException
{
    /// <summary>The digest of the blob whose size did not match.</summary>
    public string Digest { get; }

    /// <summary>The size, in bytes, declared by the manifest descriptor.</summary>
    public long DeclaredSize { get; }

    /// <summary>
    /// The actual size, in bytes, of the downloaded blob. When the download was stopped because the blob ran past
    /// its declared size, this is the number of bytes accepted before stopping (the declared size), and the real
    /// size is larger.
    /// </summary>
    public long ActualSize { get; }

    /// <summary>Creates the exception.</summary>
    public BlobSizeMismatchException(string what, string digest, long declaredSize, long actualSize)
        : this(
            $"Integrity check failed for {what} {digest}: the manifest declares a size of {declaredSize} bytes but the blob is {actualSize} bytes.",
            declaredSize, actualSize, digest)
    {
    }

    private BlobSizeMismatchException(string message, long declaredSize, long actualSize, string digest)
        : base(message)
    {
        Digest = digest;
        DeclaredSize = declaredSize;
        ActualSize = actualSize;
    }

    /// <summary>Creates the exception for a download stopped because the blob is larger than declared.</summary>
    internal static BlobSizeMismatchException LargerThanDeclared(string what, string digest, long declaredSize)
        => new(
            $"Integrity check failed for {what} {digest}: the manifest declares a size of {declaredSize} bytes but the " +
            "blob is larger, so the download was stopped.",
            declaredSize, declaredSize, digest);
}
