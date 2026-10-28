using System.Text.RegularExpressions;

namespace AWS.OCI.Builder;

/// <summary>
/// A parsed container image reference: the registry host, repository, and a tag or digest reference.
/// </summary>
/// <remarks>
/// <para>
/// Handles the common reference forms:
/// </para>
/// <list type="bullet">
///   <item><c>mcr.microsoft.com/dotnet/aspnet:8.0</c> — explicit registry host.</item>
///   <item><c>123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:latest</c> — an ECR reference.</item>
///   <item><c>library/ubuntu:22.04</c> / <c>ubuntu</c> — Docker Hub shorthand (host defaults to
///     <c>registry-1.docker.io</c>, the <c>library/</c> prefix is added for single-segment repositories).</item>
///   <item>A <c>@sha256:...</c> digest reference in place of a tag.</item>
/// </list>
/// <para>
/// The distinction between a registry host and the first path segment follows the Docker rule: the first segment
/// is a host only if it contains a <c>.</c> or <c>:</c>, or is exactly <c>localhost</c>. Otherwise the whole
/// string is a Docker Hub repository.
/// </para>
/// </remarks>
public sealed partial class ImageReference
{
    private const string DockerHubHost = "registry-1.docker.io";

    private ImageReference(string registry, string repository, string reference, bool referenceIsDigest)
    {
        Registry = registry;
        Repository = repository;
        Reference = reference;
        ReferenceIsDigest = referenceIsDigest;
    }

    /// <summary>The registry host, for example <c>mcr.microsoft.com</c> or an ECR endpoint.</summary>
    public string Registry { get; }

    /// <summary>The repository path, for example <c>dotnet/aspnet</c> or <c>myapp</c>.</summary>
    public string Repository { get; }

    /// <summary>The tag (for example <c>8.0</c>) or digest (for example <c>sha256:...</c>).</summary>
    public string Reference { get; }

    /// <summary>Whether <see cref="Reference"/> is a digest (<see langword="true"/>) rather than a tag.</summary>
    public bool ReferenceIsDigest { get; }

    /// <summary>Whether this reference points at an Amazon ECR registry.</summary>
    public bool IsEcr => EcrHostRegex().IsMatch(Registry);

    /// <summary>
    /// The AWS region embedded in an ECR registry host (for example <c>us-west-2</c>), or <see langword="null"/>
    /// when the reference is not an ECR reference.
    /// </summary>
    public string? EcrRegion
    {
        get
        {
            Match match = EcrHostRegex().Match(Registry);
            return match.Success ? match.Groups["region"].Value : null;
        }
    }

    /// <summary>
    /// Parses an image reference string. A missing tag defaults to <c>latest</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The reference is null, empty, or malformed.</exception>
    public static ImageReference Parse(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        string remainder = reference;
        string registry;

        int firstSlash = remainder.IndexOf('/');
        string firstSegment = firstSlash < 0 ? remainder : remainder[..firstSlash];

        // The first path segment is a registry host only if it looks like one (contains '.' or ':', or is
        // "localhost"); otherwise the whole reference is a Docker Hub repository.
        bool firstSegmentIsHost = firstSlash > 0 &&
            (firstSegment.Contains('.', StringComparison.Ordinal) ||
             firstSegment.Contains(':', StringComparison.Ordinal) ||
             firstSegment.Equals("localhost", StringComparison.OrdinalIgnoreCase));

        if (firstSegmentIsHost)
        {
            registry = firstSegment;
            remainder = remainder[(firstSlash + 1)..];
        }
        else
        {
            registry = DockerHubHost;
        }

        // Split off the tag/digest. A '@' marks a digest; otherwise the last ':' after the final '/' is the tag.
        string repository;
        string refValue;
        bool isDigest;

        int atIndex = remainder.IndexOf('@');
        if (atIndex >= 0)
        {
            string namePortion = remainder[..atIndex];
            refValue = remainder[(atIndex + 1)..];
            isDigest = true;

            // A reference may carry BOTH a tag and a digest (e.g. "dotnet/runtime:8.0@sha256:..."). The digest is
            // authoritative, so strip a trailing ":tag" from the name portion; otherwise the tag would leak into
            // the repository and address the wrong path. Only a ':' AFTER the final '/' is a tag separator (a ':'
            // inside an earlier segment would be a malformed host:port that repository validation rejects anyway).
            int nameLastSlash = namePortion.LastIndexOf('/');
            int nameTagColon = namePortion.IndexOf(':', nameLastSlash + 1);
            repository = nameTagColon >= 0 ? namePortion[..nameTagColon] : namePortion;
        }
        else
        {
            int lastSlash = remainder.LastIndexOf('/');
            int tagColon = remainder.IndexOf(':', lastSlash + 1);
            if (tagColon >= 0)
            {
                repository = remainder[..tagColon];
                refValue = remainder[(tagColon + 1)..];
            }
            else
            {
                repository = remainder;
                refValue = "latest";
            }
            isDigest = false;
        }

        if (repository.Length == 0)
        {
            throw new ArgumentException($"The image reference '{reference}' has no repository.", nameof(reference));
        }
        if (refValue.Length == 0)
        {
            throw new ArgumentException($"The image reference '{reference}' has an empty tag or digest.", nameof(reference));
        }

        // Docker Hub shorthand: a single-segment repository (e.g. "ubuntu") lives under "library/".
        if (registry == DockerHubHost && !repository.Contains('/', StringComparison.Ordinal))
        {
            repository = "library/" + repository;
        }

        // Validate the repository against the OCI/Docker name grammar. Without this, an input such as
        // "123456789012.dkr.ecr.us-west-2.amazonaws.com///attacker.example/app" would be accepted and its
        // "//attacker.example/app" repository would override the authority of the request URIs the RegistryClient
        // builds, sending the push to an attacker-controlled host. Rejecting malformed names here closes that vector.
        if (!OciNames.IsValidRepository(repository))
        {
            throw new ArgumentException(
                $"The image reference '{OciNames.ForDisplay(reference)}' has an invalid repository '{OciNames.ForDisplay(repository)}'. " +
                "A repository is one or more lowercase path components (letters, digits, and separators '.', '_', '-') " +
                "separated by single '/' characters.",
                nameof(reference));
        }

        // The tag or digest is also spliced into request paths, so it must match its grammar too.
        if (isDigest ? !OciNames.IsValidDigest(refValue) : !OciNames.IsValidTag(refValue))
        {
            throw new ArgumentException(
                isDigest
                    ? $"The image reference '{OciNames.ForDisplay(reference)}' has an invalid digest '{OciNames.ForDisplay(refValue)}'; expected 'sha256:' followed by 64 lowercase hex characters."
                    : $"The image reference '{OciNames.ForDisplay(reference)}' has an invalid tag '{OciNames.ForDisplay(refValue)}'.",
                nameof(reference));
        }

        return new ImageReference(registry, repository, refValue, isDigest);
    }

    /// <summary>Returns the canonical <c>registry/repository:tag</c> (or <c>@digest</c>) form.</summary>
    public override string ToString()
        => ReferenceIsDigest ? $"{Registry}/{Repository}@{Reference}" : $"{Registry}/{Repository}:{Reference}";

    // Matches an ECR host and captures the region: <account>.dkr.ecr.<region>.amazonaws.com
    // (also supports the FIPS and China/GovCloud partitions' domains).
    [GeneratedRegex(@"^\d{12}\.dkr\.ecr(-fips)?\.(?<region>[a-z0-9-]+)\.amazonaws\.com(\.cn)?$", RegexOptions.IgnoreCase)]
    private static partial Regex EcrHostRegex();
}
