using System.Collections;

namespace Amazon.OCI.Builder;

/// <summary>
/// Describes a set of local files and/or directories to add to an image, each mapped to an absolute
/// destination path inside the container. A single <see cref="FileManifest"/> becomes a single image layer.
/// </summary>
/// <remarks>
/// <para>Collection-initializer syntax is supported, for example:</para>
/// <code>
/// var files = new FileManifest
/// {
///     { "/local/app.dll", "/app/app.dll" },
///     { "/local/config",  "/app/config"  },
/// };
/// </code>
/// <para>
/// File permissions in the resulting layer: when an entry's <see cref="FileManifestEntry.Mode"/> is not set,
/// the layer builder derives the Unix mode from the source file on Linux/macOS (preserving an executable bit),
/// and falls back to a standard <c>0644</c> for files / <c>0755</c> for directories on Windows, which has no Unix
/// mode bits to read. To guarantee a file is executable in the container regardless of the build host — the only
/// way to get the executable bit when building on Windows — use <see cref="AddExecutable"/> or pass an explicit
/// <see cref="System.IO.UnixFileMode"/> to <see cref="Add(string, string, System.IO.UnixFileMode)"/>.
/// </para>
/// </remarks>
public sealed class FileManifest : IEnumerable<FileManifestEntry>
{
    private readonly List<FileManifestEntry> _entries = new();

    /// <summary>The entries that make up this manifest.</summary>
    public IReadOnlyList<FileManifestEntry> Entries => _entries;

    /// <summary>
    /// Adds a local file or directory to be placed at <paramref name="containerPath"/> inside the container, with
    /// its Unix mode derived from the source (on Linux/macOS) or defaulted (on Windows).
    /// </summary>
    /// <param name="localPath">An absolute or relative path to a file or directory on the local filesystem.</param>
    /// <param name="containerPath">The absolute destination path inside the container (for example <c>/app/app.dll</c>).</param>
    public void Add(string localPath, string containerPath)
        => _entries.Add(new FileManifestEntry(localPath, containerPath));

    /// <summary>
    /// Adds a local file or directory with an explicit Unix <paramref name="mode"/> (for example
    /// <c>(UnixFileMode)0b111_101_101</c> for <c>0755</c>). The explicit mode is applied to the mapped file, or to
    /// every file produced by a mapped directory, overriding any source-derived or default mode.
    /// </summary>
    /// <param name="localPath">An absolute or relative path to a file or directory on the local filesystem.</param>
    /// <param name="containerPath">The absolute destination path inside the container.</param>
    /// <param name="mode">The Unix file mode to stamp on the entry.</param>
    public void Add(string localPath, string containerPath, UnixFileMode mode)
        => _entries.Add(new FileManifestEntry(localPath, containerPath, mode));

    /// <summary>
    /// Adds a local file (or directory of files) and marks it executable (<c>0755</c>) in the container. Use this
    /// for scripts and binaries so they are runnable regardless of the build host — on Windows the source file has
    /// no executable bit to preserve, so this is the only way to produce an executable entry there.
    /// </summary>
    /// <param name="localPath">An absolute or relative path to a file or directory on the local filesystem.</param>
    /// <param name="containerPath">The absolute destination path inside the container.</param>
    public void AddExecutable(string localPath, string containerPath)
        => _entries.Add(new FileManifestEntry(localPath, containerPath, FileManifestEntry.ExecutableMode));

    /// <inheritdoc />
    public IEnumerator<FileManifestEntry> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>A single local-to-container path mapping within a <see cref="FileManifest"/>.</summary>
/// <param name="LocalPath">The source path on the local filesystem.</param>
/// <param name="ContainerPath">The absolute destination path inside the container.</param>
/// <param name="Mode">
/// An explicit Unix file mode for the entry, or <see langword="null"/> to derive it from the source (on
/// Linux/macOS) or use the default (on Windows). For a directory mapping, an explicit mode applies to the files
/// it contains.
/// </param>
public readonly record struct FileManifestEntry(string LocalPath, string ContainerPath, UnixFileMode? Mode = null)
{
    /// <summary>A standard executable file mode: <c>0755</c> (rwxr-xr-x).</summary>
    public const UnixFileMode ExecutableMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
}
