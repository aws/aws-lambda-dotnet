using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AWS.OCI.Builder.Images;

/// <summary>
/// The metadata of a single image layer produced from a <see cref="FileManifest"/> — a gzipped tar archive of
/// the mapped files — together with the two digests the OCI format requires. The compressed blob itself is
/// written to a caller-supplied stream by <see cref="CreateAsync"/>; this type does not hold the layer bytes.
/// </summary>
/// <remarks>
/// <para>
/// An image layer is identified by <em>two</em> different SHA-256 hashes, and the distinction matters:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="Digest"/> — the hash of the <em>compressed</em> (gzipped) tar bytes. This is the blob's
///     content address: it is what the registry stores the layer under and what the image manifest references.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="DiffId"/> — the hash of the <em>uncompressed</em> tar bytes. This is what the image config's
///     <c>rootfs.diff_ids</c> array references, so the runtime can verify the extracted filesystem.
///     </description>
///   </item>
/// </list>
/// <para>
/// Entries are written in a stable sorted order with fixed timestamps and normalized paths. Digests are not
/// guaranteed to be identical across builds, even for identical input.
/// </para>
/// </remarks>
internal sealed class Layer
{
    /// <summary>A fixed timestamp used for every tar entry.</summary>
    private static readonly DateTimeOffset ReproducibleTimestamp = DateTimeOffset.UnixEpoch;

    private Layer(string digest, string diffId, long size, string mediaType)
    {
        Digest = digest;
        DiffId = diffId;
        Size = size;
        MediaType = mediaType;
    }

    /// <summary>
    /// The <c>sha256:...</c> digest of the <em>compressed</em> tar. Addresses the blob in the registry and is
    /// referenced by the image manifest's layer descriptor.
    /// </summary>
    public string Digest { get; }

    /// <summary>
    /// The <c>sha256:...</c> digest of the <em>uncompressed</em> tar. Referenced by the image config's
    /// <c>rootfs.diff_ids</c>.
    /// </summary>
    public string DiffId { get; }

    /// <summary>The size, in bytes, of the compressed tar written to the destination stream.</summary>
    public long Size { get; }

    /// <summary>The layer media type (OCI or Docker gzipped-layer type, matching the target image's schema).</summary>
    public string MediaType { get; }

    /// <summary>
    /// A content descriptor for this layer, suitable for inclusion in an image manifest's <c>layers</c> array.
    /// </summary>
    public Descriptor ToDescriptor() => new()
    {
        MediaType = MediaType,
        Size = Size,
        Digest = Digest,
    };

    /// <summary>
    /// Builds a gzipped-tar layer from the files and directories described by <paramref name="manifest"/>,
    /// writing the compressed blob to <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The layer is produced in a single streaming pass — tar entries are written into a gzip compressor whose
    /// output goes straight to <paramref name="destination"/> — while both digests are computed on the fly: the
    /// <see cref="DiffId"/> over the uncompressed tar bytes, and the <see cref="Digest"/> and <see cref="Size"/>
    /// over the compressed bytes. Memory use is therefore bounded by stream buffers regardless of layer size, and
    /// there is no limit on layer size other than the capacity of the destination.
    /// </para>
    /// <para>
    /// <paramref name="destination"/> is not disposed, closed, or rewound; the caller owns it. Its prior contents
    /// and position are left alone, and the blob is appended starting at its current position. If this method
    /// throws, the destination may hold a partial blob and must be discarded.
    /// </para>
    /// </remarks>
    /// <param name="manifest">The local-to-container path mappings that populate the layer.</param>
    /// <param name="destination">A writable stream that receives the compressed (gzipped tar) layer blob.</param>
    /// <param name="mediaType">
    /// The gzipped-layer media type to report (defaults to <see cref="MediaTypes.OciLayerGzipV1"/>). Pass
    /// <see cref="MediaTypes.DockerLayerGzip"/> when overlaying onto a Docker-schema base image.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The built <see cref="Layer"/>'s metadata, with both digests and the compressed size.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The manifest is empty, an entry has an invalid container path, or <paramref name="destination"/> is not writable.
    /// </exception>
    /// <exception cref="FileNotFoundException">A mapped local file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">A mapped local directory does not exist.</exception>
    public static async Task<Layer> CreateAsync(
        FileManifest manifest,
        Stream destination,
        string mediaType = MediaTypes.OciLayerGzipV1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }
        if (manifest.Entries.Count == 0)
        {
            throw new ArgumentException("The file manifest is empty; a layer must contain at least one file.", nameof(manifest));
        }

        // Expand every manifest entry into concrete file additions (container path -> local file + mode), plus the
        // set of directory paths implied by those files. Using a sorted dictionary keyed by the normalized
        // container path gives us a deterministic emission order regardless of manifest insertion order.
        var files = new SortedDictionary<string, FileSource>(StringComparer.Ordinal);
        var directories = new SortedSet<string>(StringComparer.Ordinal);

        foreach (FileManifestEntry entry in manifest.Entries)
        {
            CollectEntry(entry, files, directories);
        }

        // One streaming pass:  tar -> [hash: diff_id] -> gzip -> [normalize gzip header] -> [hash + count: digest,
        // size] -> destination. Nothing is buffered beyond stream buffers, so layer size is not limited by memory
        // (an in-memory MemoryStream/byte[] would cap a layer at ~2 GiB and hold several full copies at once).
        using var diffIdHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var digestHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var compressedSink = new HashingWriteStream(destination, digestHash);
        var headerNormalizer = new GzipHeaderNormalizingStream(compressedSink);

        // leaveOpen: true — disposing the compressor writes the gzip trailer through the wrappers but must not
        // close the caller's destination. SmallestSize keeps layer blobs compact (same level as before).
        await using (var gzip = new GZipStream(headerNormalizer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var tarSink = new HashingWriteStream(gzip, diffIdHash);
            await WriteTarAsync(files, directories, tarSink, cancellationToken).ConfigureAwait(false);
        }

        await compressedSink.FlushAsync(cancellationToken).ConfigureAwait(false);

        string diffId = ToDigestString(diffIdHash);
        string digest = ToDigestString(digestHash);
        return new Layer(digest, diffId, compressedSink.BytesWritten, mediaType);
    }

    private static string ToDigestString(IncrementalHash hash)
        // Convert.ToHexStringLower exists only on net9.0+, so lower-case explicitly for net8.0 compatibility.
        => "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

    /// <summary>
    /// Expands a single manifest entry into file additions and the directory entries to emit.
    /// </summary>
    /// <remarks>
    /// Only EXPLICITLY mapped directories are recorded in <paramref name="directories"/>; the implicit ancestors
    /// of a file are not. Tar extraction creates missing parent directories implicitly, so emitting ancestor
    /// entries is unnecessary — and harmful, because a synthesized 0755 ancestor entry would overlay (and reset)
    /// the base image's own permissions on that directory (for example <c>/tmp</c>'s 1777, or a private 0700
    /// parent). Leaving ancestors out preserves the base image's directory metadata.
    /// </remarks>
    private static void CollectEntry(FileManifestEntry entry, SortedDictionary<string, FileSource> files, SortedSet<string> directories)
    {
        string containerPath = NormalizeContainerPath(entry.ContainerPath);

        // Reject a symlink/reparse point at the mapped path itself: following it could pull in content from
        // outside the mapping (including host secrets).
        RejectIfSymlink(entry.LocalPath, entry.LocalPath);

        if (File.Exists(entry.LocalPath))
        {
            AddFile(entry.LocalPath, containerPath, entry.Mode, files);
            return;
        }

        if (Directory.Exists(entry.LocalPath))
        {
            // A directory maps recursively. Record the mapped directory itself so an empty mapping still appears,
            // then walk it WITHOUT following symlinks, recording every descendant file AND directory so the full
            // structure (including empty subdirectories) is preserved.
            directories.Add(containerPath);

            string root = Path.GetFullPath(entry.LocalPath);
            CollectDirectoryTree(root, containerPath, entry.Mode, files, directories);
            return;
        }

        // Neither a file nor a directory: surface a precise error naming the missing source.
        throw new FileNotFoundException(
            $"The manifest maps '{entry.LocalPath}' to '{entry.ContainerPath}', but no such file or directory exists.",
            entry.LocalPath);
    }

    /// <summary>
    /// Recursively collects the files and subdirectories under <paramref name="localRoot"/> into the layer,
    /// placing each under <paramref name="containerRoot"/>. Does not follow directory or file symlinks: a reparse
    /// point is rejected rather than traversed, so a link cannot copy files from outside the mapped tree into the
    /// image. Empty subdirectories are recorded so the directory structure is preserved.
    /// </summary>
    private static void CollectDirectoryTree(
        string localRoot,
        string containerRoot,
        UnixFileMode? explicitMode,
        SortedDictionary<string, FileSource> files,
        SortedSet<string> directories)
    {
        // Enumerate entries at this level without recursing, so we can inspect each for a reparse point before
        // descending. TopDirectoryOnly keeps the symlink decision local to each entry.
        foreach (string localEntry in Directory.EnumerateFileSystemEntries(localRoot, "*", SearchOption.TopDirectoryOnly))
        {
            string relative = Path.GetRelativePath(localRoot, localEntry).Replace('\\', '/');
            // Normalize/validate the expanded target too (not just caller-supplied destinations): a source name
            // containing '..' or backslashes must not be able to escape the container root.
            string target = NormalizeContainerPath($"{containerRoot}/{relative}");

            RejectIfSymlink(localEntry, localEntry);

            if (Directory.Exists(localEntry))
            {
                directories.Add(target);
                CollectDirectoryTree(localEntry, target, explicitMode, files, directories);
            }
            else if (File.Exists(localEntry))
            {
                AddFile(localEntry, target, explicitMode, files);
            }
        }
    }

    /// <summary>
    /// Throws when <paramref name="path"/> is a symbolic link or other reparse point. The layer builder archives
    /// entries as regular files/directories; silently following a link would let a link inside a mapped directory
    /// pull in files from outside it (an exfiltration vector) and cyclic links would loop. Rejecting is explicit
    /// and safe for this library's scope (overlay a known set of local files).
    /// </summary>
    private static void RejectIfSymlink(string path, string reportedPath)
    {
        FileSystemInfo? info = Directory.Exists(path) ? new DirectoryInfo(path)
            : File.Exists(path) ? new FileInfo(path)
            : null;

        if (info is not null && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new ArgumentException(
                $"The manifest source '{reportedPath}' is a symbolic link or reparse point, which is not supported. " +
                "Map the real target path explicitly instead.");
        }
    }

    private static void AddFile(string localPath, string containerPath, UnixFileMode? explicitMode, SortedDictionary<string, FileSource> files)
    {
        if (files.ContainsKey(containerPath))
        {
            throw new ArgumentException($"Two manifest entries map to the same container path '{containerPath}'.");
        }
        files[containerPath] = new FileSource(localPath, ResolveFileMode(localPath, explicitMode));
    }

    /// <summary>
    /// Determines the Unix mode to stamp on a file entry:
    /// <list type="number">
    ///   <item>an explicit mode from the manifest entry wins;</item>
    ///   <item>otherwise, on Linux/macOS the source file's own mode is used, so an executable source stays
    ///     executable (a non-readable source is still forced readable so the layer is usable);</item>
    ///   <item>otherwise (Windows, which has no Unix mode bits) a standard <c>0644</c> is used.</item>
    /// </list>
    /// </summary>
    private static UnixFileMode ResolveFileMode(string localPath, UnixFileMode? explicitMode)
    {
        if (explicitMode is { } mode)
        {
            return mode;
        }

        if (!OperatingSystem.IsWindows())
        {
            // On Unix the source carries real mode bits. Preserve them (notably the executable bits), but always
            // ensure the owner can read the file so the resulting layer entry is never unreadable.
            UnixFileMode sourceMode = File.GetUnixFileMode(localPath);
            return sourceMode | UnixFileMode.UserRead;
        }

        return DefaultFileMode;
    }

    /// <summary>
    /// Writes the directory and file entries as a PAX tar archive, in a deterministic order, to
    /// <paramref name="destination"/> (the hashing/compression pipeline). Source files are streamed, not buffered.
    /// </summary>
    /// <remarks>
    /// PAX is the format used by container tooling (including <c>Microsoft.NET.Build.Containers</c>) and is what
    /// registries and runtimes expect for image layers. Every entry gets a fixed extended-attribute set (see
    /// <see cref="ReproducibleExtendedAttributes"/>) and a fixed <see cref="ReproducibleTimestamp"/> rather than
    /// the current time.
    /// </remarks>
    private static async Task WriteTarAsync(
        SortedDictionary<string, FileSource> files,
        SortedSet<string> directories,
        Stream destination,
        CancellationToken cancellationToken)
    {
        // leaveOpen: true — the destination is the hashing/compression pipeline, finalized by the caller.
        await using (var tar = new TarWriter(destination, TarEntryFormat.Pax, leaveOpen: true))
        {
            // Directories first, shallowest-to-deepest (the sorted set already yields a stable order), so every
            // file's parent exists before the file entry. Both collections are ordinally sorted for determinism.
            foreach (string dir in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dirEntry = new PaxTarEntry(TarEntryType.Directory, ToTarEntryName(dir, isDirectory: true), ReproducibleExtendedAttributes())
                {
                    Mode = DefaultDirectoryMode,
                    ModificationTime = ReproducibleTimestamp,
                };
                await tar.WriteEntryAsync(dirEntry, cancellationToken).ConfigureAwait(false);
            }

            foreach ((string containerPath, FileSource file) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileEntry = new PaxTarEntry(TarEntryType.RegularFile, ToTarEntryName(containerPath, isDirectory: false), ReproducibleExtendedAttributes())
                {
                    Mode = file.Mode,
                    ModificationTime = ReproducibleTimestamp,
                };

                // Open each source file read-only and shared so concurrent readers (e.g. the build) aren't blocked.
                await using FileStream source = new(file.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                fileEntry.DataStream = source;
                await tar.WriteEntryAsync(fileEntry, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>A resolved file to place in the layer: its source path and the Unix mode to stamp on it.</summary>
    private readonly record struct FileSource(string LocalPath, UnixFileMode Mode);

    /// <summary>
    /// The PAX extended attributes applied to every entry: fixed <c>mtime</c>, <c>atime</c> and <c>ctime</c>.
    /// </summary>
    /// <remarks>
    /// On .NET 8 the PAX writer otherwise fills <c>atime</c> and <c>ctime</c> with the current wall-clock time;
    /// pinning all three to a fixed epoch keeps them out of the archive.
    /// </remarks>
    private static Dictionary<string, string> ReproducibleExtendedAttributes()
    {
        string epoch = ReproducibleTimestamp.ToUnixTimeSeconds().ToString("D", System.Globalization.CultureInfo.InvariantCulture);
        return new Dictionary<string, string>
        {
            ["mtime"] = epoch,
            ["atime"] = epoch,
            ["ctime"] = epoch,
        };
    }

    /// <summary>
    /// Normalizes a caller-supplied container path to an absolute, forward-slash path with no trailing slash
    /// and no redundant segments.
    /// </summary>
    private static string NormalizeContainerPath(string containerPath)
    {
        if (string.IsNullOrWhiteSpace(containerPath))
        {
            throw new ArgumentException("A container path must not be null or empty.");
        }

        string normalized = containerPath.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            throw new ArgumentException($"Container path '{containerPath}' must be absolute (start with '/').");
        }

        // Collapse '.' and empty segments; reject '..' which could escape the intended location.
        var segments = new List<string>();
        foreach (string segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                throw new ArgumentException($"Container path '{containerPath}' must not contain '..' segments.");
            }
            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException($"Container path '{containerPath}' does not name a file or directory.");
        }

        return "/" + string.Join('/', segments);
    }

    /// <summary>
    /// Converts a normalized, rooted container path (for example <c>/app/app.dll</c>) to a tar entry name. Tar
    /// entry names are relative (no leading slash) and directory entries carry a trailing slash by convention.
    /// </summary>
    private static string ToTarEntryName(string normalizedPath, bool isDirectory)
    {
        string name = normalizedPath.TrimStart('/');
        return isDirectory ? name + "/" : name;
    }

    // Standard rootfs permissions: 0755 for directories, 0644 for files.
    private const UnixFileMode DefaultDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private const UnixFileMode DefaultFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead |
        UnixFileMode.OtherRead;
}
