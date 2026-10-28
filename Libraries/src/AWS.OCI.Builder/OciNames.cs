using System.Text.RegularExpressions;

namespace AWS.OCI.Builder;

/// <summary>
/// Validation for the names that end up in registry request URLs: repositories, tags and digests.
/// </summary>
/// <remarks>
/// These values are spliced into URL paths such as <c>/v2/&lt;repository&gt;/blobs/&lt;digest&gt;</c>, and some come
/// from untrusted documents (a base image's manifest). A value like <c>sha256:x/../../other/manifests/latest</c>
/// would otherwise redirect an authenticated request to a different path, and a repository starting with
/// <c>//</c> would replace the request's host. All patterns are anchored with <c>\z</c> so a trailing newline
/// cannot slip through.
/// </remarks>
internal static partial class OciNames
{
    /// <summary>Returns whether <paramref name="digest"/> is <c>sha256:</c> followed by 64 lowercase hex characters.</summary>
    public static bool IsValidDigest(string? digest) => digest is not null && DigestRegex().IsMatch(digest);

    /// <summary>Returns whether <paramref name="tag"/> matches the OCI tag grammar.</summary>
    public static bool IsValidTag(string? tag) => tag is not null && TagRegex().IsMatch(tag);

    /// <summary>Returns whether <paramref name="repository"/> matches the OCI repository-name grammar.</summary>
    public static bool IsValidRepository(string? repository) => repository is not null && RepositoryRegex().IsMatch(repository);

    /// <summary>Returns whether <paramref name="reference"/> is a valid tag or a valid digest.</summary>
    public static bool IsValidReference(string? reference) => IsValidTag(reference) || IsValidDigest(reference);

    /// <summary>Formats an untrusted value for an error message: shortened, with control characters escaped.</summary>
    public static string ForDisplay(string? value)
    {
        if (value is null)
        {
            return "(null)";
        }
        string shortened = value.Length <= 100 ? value : value[..100] + "…";
        return Regex.Replace(shortened, @"\p{C}", m => $"\\u{(int)m.Value[0]:x4}");
    }

    [GeneratedRegex(@"^sha256:[a-f0-9]{64}\z")]
    private static partial Regex DigestRegex();

    // OCI distribution spec tag grammar.
    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}\z")]
    private static partial Regex TagRegex();

    // One or more path components separated by single '/' characters, each component lowercase alphanumerics with
    // optional internal '.', '_', '__' or '-' separators. Rejects leading/trailing/double slashes, empty components
    // and uppercase: the shapes that could smuggle an alternate authority or path into a request URI.
    [GeneratedRegex(@"^[a-z0-9]+(?:(?:\.|_|__|-+)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:\.|_|__|-+)[a-z0-9]+)*)*\z")]
    private static partial Regex RepositoryRegex();
}
