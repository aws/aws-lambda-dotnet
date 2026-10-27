using System.Formats.Tar;
using System.IO.Compression;
using Amazon.OCI.Builder;
using Amazon.OCI.Builder.Images;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class LayerTests : IDisposable
{
    private readonly string _tempRoot;

    public LayerTests()
    {
        // Each test gets an isolated temp directory for its source files.
        _tempRoot = Path.Combine(Path.GetTempPath(), "aws-oci-builder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best effort cleanup */ }
    }

    private string WriteFile(string relativePath, string content)
    {
        string full = Path.Combine(_tempRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public async Task Builds_layer_with_both_digests_populated()
    {
        string file = WriteFile("app.dll", "hello");
        var manifest = new FileManifest { { file, "/app/app.dll" } };

        Layer layer = await Layer.CreateAsync(manifest);

        Assert.StartsWith("sha256:", layer.Digest);
        Assert.StartsWith("sha256:", layer.DiffId);
        Assert.Equal(64 + "sha256:".Length, layer.Digest.Length);
        Assert.True(layer.Size > 0);
        Assert.Equal(layer.GzippedTarBytes.LongLength, layer.Size);
        Assert.Equal(MediaTypes.OciLayerGzipV1, layer.MediaType);
    }

    [Fact]
    public async Task Digest_and_diffId_differ_because_one_is_compressed()
    {
        string file = WriteFile("app.dll", new string('x', 4096));
        var manifest = new FileManifest { { file, "/app/app.dll" } };

        Layer layer = await Layer.CreateAsync(manifest);

        // Digest is over the gzipped bytes; DiffId is over the raw tar. They must not be equal.
        Assert.NotEqual(layer.Digest, layer.DiffId);
    }

    [Fact]
    public async Task Descriptor_reflects_layer_fields()
    {
        string file = WriteFile("app.dll", "hello");
        var manifest = new FileManifest { { file, "/app/app.dll" } };

        Layer layer = await Layer.CreateAsync(manifest, MediaTypes.DockerLayerGzip);
        Descriptor descriptor = layer.ToDescriptor();

        Assert.Equal(MediaTypes.DockerLayerGzip, descriptor.MediaType);
        Assert.Equal(layer.Size, descriptor.Size);
        Assert.Equal(layer.Digest, descriptor.Digest);
    }

    [Fact]
    public async Task Is_reproducible_identical_inputs_yield_identical_digests()
    {
        string file = WriteFile("app.dll", "deterministic");
        var manifest1 = new FileManifest { { file, "/app/app.dll" } };
        var manifest2 = new FileManifest { { file, "/app/app.dll" } };

        Layer a = await Layer.CreateAsync(manifest1);
        Layer b = await Layer.CreateAsync(manifest2);

        Assert.Equal(a.Digest, b.Digest);
        Assert.Equal(a.DiffId, b.DiffId);
    }

    [Fact]
    public async Task Entry_order_does_not_affect_digest()
    {
        string f1 = WriteFile("a.txt", "one");
        string f2 = WriteFile("b.txt", "two");

        var ordered = new FileManifest { { f1, "/app/a.txt" }, { f2, "/app/b.txt" } };
        var reversed = new FileManifest { { f2, "/app/b.txt" }, { f1, "/app/a.txt" } };

        Layer a = await Layer.CreateAsync(ordered);
        Layer b = await Layer.CreateAsync(reversed);

        Assert.Equal(a.Digest, b.Digest);
    }

    [Fact]
    public async Task Tar_contains_file_but_not_synthesized_ancestor_directories()
    {
        string file = WriteFile("app.dll", "hello");
        var manifest = new FileManifest { { file, "/app/bin/app.dll" } };

        Layer layer = await Layer.CreateAsync(manifest);
        List<(TarEntryType Type, string Name)> entries = ReadTarEntries(layer.GzippedTarBytes);

        // The file is present (no leading slash). Its ancestor directories are NOT synthesized as tar entries:
        // emitting a 0755 "app/" / "app/bin/" entry would overlay and reset the base image's permissions on those
        // directories. Tar extraction creates missing parents implicitly, so the file still lands correctly.
        Assert.Contains(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/bin/app.dll" });
        Assert.DoesNotContain(entries, e => e is { Type: TarEntryType.Directory, Name: "app/" });
        Assert.DoesNotContain(entries, e => e is { Type: TarEntryType.Directory, Name: "app/bin/" });
    }

    [Fact]
    public async Task Directory_mapping_recurses_preserving_subpaths()
    {
        WriteFile("content/root.txt", "r");
        WriteFile("content/sub/leaf.txt", "l");
        string dir = Path.Combine(_tempRoot, "content");

        var manifest = new FileManifest { { dir, "/app/data" } };

        Layer layer = await Layer.CreateAsync(manifest);
        List<(TarEntryType Type, string Name)> entries = ReadTarEntries(layer.GzippedTarBytes);

        Assert.Contains(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/data/root.txt" });
        Assert.Contains(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/data/sub/leaf.txt" });
        Assert.Contains(entries, e => e is { Type: TarEntryType.Directory, Name: "app/data/sub/" });
    }

    [Fact]
    public async Task Directory_mapping_preserves_empty_descendant_directories()
    {
        // A recursive directory mapping must preserve empty subdirectories, not just directories that hold files.
        WriteFile("content/root.txt", "r");
        Directory.CreateDirectory(Path.Combine(_tempRoot, "content", "cache"));   // empty
        string dir = Path.Combine(_tempRoot, "content");

        var manifest = new FileManifest { { dir, "/app/data" } };

        Layer layer = await Layer.CreateAsync(manifest);
        List<(TarEntryType Type, string Name)> entries = ReadTarEntries(layer.GzippedTarBytes);

        Assert.Contains(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/data/root.txt" });
        Assert.Contains(entries, e => e is { Type: TarEntryType.Directory, Name: "app/data/cache/" });
    }

    [Fact]
    public async Task Symlinked_source_file_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Creating symlinks on Windows requires elevation; the guard is exercised on Unix CI.
        }

        string real = WriteFile("secret.txt", "host secret");
        string link = Path.Combine(_tempRoot, "link.txt");
        File.CreateSymbolicLink(link, real);

        var manifest = new FileManifest { { link, "/app/link.txt" } };

        // A symlinked source must be rejected rather than silently followed (which could pull in files from
        // outside the mapped set, e.g. host secrets).
        await Assert.ThrowsAsync<ArgumentException>(() => Layer.CreateAsync(manifest));
    }

    [Fact]
    public async Task Symlink_inside_mapped_directory_is_rejected()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WriteFile("tree/ok.txt", "ok");
        string outside = WriteFile("outside.txt", "host secret");
        // A link INSIDE the mapped directory pointing outside it must not copy the external file into the layer.
        File.CreateSymbolicLink(Path.Combine(_tempRoot, "tree", "escape.txt"), outside);
        string dir = Path.Combine(_tempRoot, "tree");

        var manifest = new FileManifest { { dir, "/app" } };

        await Assert.ThrowsAsync<ArgumentException>(() => Layer.CreateAsync(manifest));
    }

    [Fact]
    public async Task Empty_manifest_throws()
    {
        var manifest = new FileManifest();
        await Assert.ThrowsAsync<ArgumentException>(() => Layer.CreateAsync(manifest));
    }

    [Fact]
    public async Task Missing_source_throws_FileNotFound()
    {
        var manifest = new FileManifest { { Path.Combine(_tempRoot, "does-not-exist"), "/app/x" } };
        await Assert.ThrowsAsync<FileNotFoundException>(() => Layer.CreateAsync(manifest));
    }

    [Theory]
    [InlineData("relative/path")]   // not absolute
    [InlineData("/app/../etc")]     // escaping segment
    [InlineData("/")]               // names nothing
    public async Task Invalid_container_path_throws(string containerPath)
    {
        string file = WriteFile("app.dll", "hello");
        var manifest = new FileManifest { { file, containerPath } };
        await Assert.ThrowsAsync<ArgumentException>(() => Layer.CreateAsync(manifest));
    }

    [Fact]
    public async Task Duplicate_container_path_throws()
    {
        string f1 = WriteFile("a.txt", "one");
        string f2 = WriteFile("b.txt", "two");
        var manifest = new FileManifest { { f1, "/app/same.txt" }, { f2, "/app/same.txt" } };
        await Assert.ThrowsAsync<ArgumentException>(() => Layer.CreateAsync(manifest));
    }

    /// <summary>Decompresses the gzipped tar and returns its real entries (type + name) in order.</summary>
    /// <remarks>
    /// PAX archives carry extended-attribute header blocks. Depending on the runtime, <see cref="TarReader"/>
    /// surfaces these differently: .NET 9+ folds them into the following entry, while .NET 8's reader can emit a
    /// separate pseudo-entry (named <c>PaxHeaders.NNN/...</c>) AND then read the real entry with a stray leading
    /// NUL in its name and a non-standard entry-type byte. Those are quirks of the .NET 8 reader, not of the
    /// archive bytes (which .NET 9+ and real registries parse correctly). This helper filters the metadata
    /// pseudo-entries and normalizes the NUL-prefixed names so the assertions test the layer's real structure.
    /// </remarks>
    private static List<(TarEntryType Type, string Name)> ReadTarEntries(byte[] gzippedTar)
        => ReadTarEntriesWithMode(gzippedTar).Select(e => (e.Type, e.Name)).ToList();

    /// <summary>Like <see cref="ReadTarEntries"/> but also returns each entry's Unix <see cref="UnixFileMode"/>.</summary>
    private static List<(TarEntryType Type, string Name, UnixFileMode Mode)> ReadTarEntriesWithMode(byte[] gzippedTar)
    {
        var result = new List<(TarEntryType, string, UnixFileMode)>();
        using var input = new MemoryStream(gzippedTar);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        while (reader.GetNextEntry() is { } entry)
        {
            string name = entry.Name.TrimStart('\0');

            bool isMetadata =
                entry.EntryType is TarEntryType.ExtendedAttributes or TarEntryType.GlobalExtendedAttributes
                || name.Contains("PaxHeaders", StringComparison.Ordinal);
            if (isMetadata)
            {
                continue;
            }

            // Normalize the .NET 8 reader artifact: a name not ending in '/' is a regular file even when the
            // reader reports a stray type byte for it.
            TarEntryType type = entry.EntryType;
            if (type is not TarEntryType.Directory && !name.EndsWith('/'))
            {
                type = TarEntryType.RegularFile;
            }

            result.Add((type, name, entry.Mode));
        }
        return result;
    }

    // --- File permission coverage (matters especially when building on Windows) ---------------

    [Fact]
    public async Task Default_file_mode_is_0644_and_directory_mode_is_0755()
    {
        string file = WriteFile("app.dll", "hello");
        // Map an explicit directory too, so there is a real (non-synthesized) directory entry to check the mode on.
        // Synthesized ancestors are intentionally not emitted, so the directory assertion uses a mapped directory.
        string dir = Directory.CreateDirectory(Path.Combine(_tempRoot, "assets")).FullName;
        var manifest = new FileManifest { { file, "/app/app.dll" }, { dir, "/app/assets" } };

        Layer layer = await Layer.CreateAsync(manifest);
        var entries = ReadTarEntriesWithMode(layer.GzippedTarBytes);

        // On Windows the source has no mode bits, so the file defaults to 0644. On Unix the source file written by
        // the test is created 0644 by default too (no execute bit), so this holds on every platform.
        (TarEntryType Type, string Name, UnixFileMode Mode) fileEntry =
            Assert.Single(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/app.dll" });
        Assert.Equal(Mode0644, fileEntry.Mode);

        (TarEntryType Type, string Name, UnixFileMode Mode) dirEntry =
            Assert.Single(entries, e => e is { Type: TarEntryType.Directory, Name: "app/assets/" });
        Assert.Equal(Mode0755, dirEntry.Mode);
    }

    [Fact]
    public async Task Explicit_mode_overrides_the_default()
    {
        string file = WriteFile("run.sh", "#!/bin/sh\necho hi\n");
        var manifest = new FileManifest { { file, "/app/run.sh", Mode0750 } };

        Layer layer = await Layer.CreateAsync(manifest);
        var entries = ReadTarEntriesWithMode(layer.GzippedTarBytes);

        var fileEntry = Assert.Single(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/run.sh" });
        Assert.Equal(Mode0750, fileEntry.Mode);
    }

    [Fact]
    public async Task AddExecutable_marks_the_file_0755()
    {
        string file = WriteFile("entrypoint.sh", "#!/bin/sh\n");
        var manifest = new FileManifest();
        manifest.AddExecutable(file, "/app/entrypoint.sh");

        Layer layer = await Layer.CreateAsync(manifest);
        var entries = ReadTarEntriesWithMode(layer.GzippedTarBytes);

        var fileEntry = Assert.Single(entries, e => e is { Type: TarEntryType.RegularFile, Name: "app/entrypoint.sh" });
        Assert.Equal(Mode0755, fileEntry.Mode);
    }

    [Fact]
    public async Task Explicit_mode_applies_to_every_file_in_a_mapped_directory()
    {
        WriteFile("scripts/a.sh", "#!/bin/sh\n");
        WriteFile("scripts/nested/b.sh", "#!/bin/sh\n");
        string dir = Path.Combine(_tempRoot, "scripts");

        var manifest = new FileManifest { { dir, "/app/scripts", FileManifestEntry.ExecutableMode } };

        Layer layer = await Layer.CreateAsync(manifest);
        var entries = ReadTarEntriesWithMode(layer.GzippedTarBytes);

        var a = Assert.Single(entries, e => e.Name == "app/scripts/a.sh");
        var b = Assert.Single(entries, e => e.Name == "app/scripts/nested/b.sh");
        Assert.Equal(Mode0755, a.Mode);
        Assert.Equal(Mode0755, b.Mode);
    }

    [Fact]
    public void Executable_bit_from_source_is_preserved_on_unix()
    {
        // On Windows there is no source executable bit to read, so this behavior only applies to Unix hosts.
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string file = WriteFile("tool", "#!/bin/sh\necho tool\n");
        File.SetUnixFileMode(file, Mode0755);

        var manifest = new FileManifest { { file, "/app/tool" } }; // no explicit mode -> derive from source
        Layer layer = Layer.CreateAsync(manifest).GetAwaiter().GetResult();
        var entries = ReadTarEntriesWithMode(layer.GzippedTarBytes);

        var fileEntry = Assert.Single(entries, e => e.Name == "app/tool");
        // The owner-execute bit from the source must survive into the layer.
        Assert.True(fileEntry.Mode.HasFlag(UnixFileMode.UserExecute));
    }

    [Fact]
    public async Task Explicit_mode_is_deterministic_in_the_digest()
    {
        string file = WriteFile("run.sh", "#!/bin/sh\n");
        var m1 = new FileManifest();
        m1.AddExecutable(file, "/app/run.sh");
        var m2 = new FileManifest();
        m2.AddExecutable(file, "/app/run.sh");

        Layer a = await Layer.CreateAsync(m1);
        Layer b = await Layer.CreateAsync(m2);

        Assert.Equal(a.Digest, b.Digest);
    }

    [Fact]
    public async Task Changing_the_mode_changes_the_digest()
    {
        string file = WriteFile("run.sh", "#!/bin/sh\n");
        var plain = new FileManifest { { file, "/app/run.sh" } };
        var executable = new FileManifest();
        executable.AddExecutable(file, "/app/run.sh");

        Layer a = await Layer.CreateAsync(plain);
        Layer b = await Layer.CreateAsync(executable);

        // Same content, different permissions -> different layer bytes -> different digest.
        Assert.NotEqual(a.Digest, b.Digest);
    }

    private const UnixFileMode Mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private const UnixFileMode Mode0755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    private const UnixFileMode Mode0750 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
}
