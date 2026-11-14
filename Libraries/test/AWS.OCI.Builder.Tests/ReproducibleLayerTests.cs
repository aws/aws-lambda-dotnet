using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AWS.OCI.Builder;
using AWS.OCI.Builder.Images;
using AWS.OCI.Builder.Registry;
using Xunit;

namespace AWS.OCI.Builder.Tests;

/// <summary>Layers are byte-for-byte reproducible: ustar entries with fixed metadata, PAX only where ustar cannot hold an entry.</summary>
public class ReproducibleLayerTests
{
    [Fact]
    public async Task Layer_is_ustar_with_fixed_times_and_no_process_specific_headers()
    {
        string dir = Directory.CreateTempSubdirectory("fix-repro-").FullName;
        try
        {
            // A long path that still fits ustar's prefix/name split, a non-ASCII name, and one path component
            // too long for ustar (forces the PAX fallback for that entry only).
            string longDir = Path.Combine(dir, new string('d', 120));
            Directory.CreateDirectory(longDir);
            string unicodeName = "ü-ünïcode-" + new string('f', 60) + ".txt";
            await File.WriteAllTextAsync(Path.Combine(longDir, unicodeName), "x");
            string tooLong = new string('t', 150) + ".txt";
            await File.WriteAllTextAsync(Path.Combine(dir, tooLong), "y");
            var manifest = new FileManifest { { dir, "/opt/app" } };
            manifest.AddEmptyDirectory("/opt");

            using var blob = new MemoryStream();
            await Layer.CreateAsync(manifest, blob);
            byte[] tarBytes = Gunzip(blob.ToArray());

            using var tar = new TarReader(new MemoryStream(tarBytes));
            var entries = new List<(TarEntryFormat Format, string Name)>();
            while (tar.GetNextEntry() is { } e)
            {
                Assert.Equal(DateTimeOffset.UnixEpoch, e.ModificationTime);
                Assert.Equal(0, e.Uid);
                Assert.Equal(0, e.Gid);
                entries.Add((e.Format, e.Name));
            }
            Assert.Equal(
                new[]
                {
                    (TarEntryFormat.Ustar, "opt/"),
                    (TarEntryFormat.Ustar, "opt/app/"),
                    (TarEntryFormat.Pax, $"opt/app/{new string('d', 120)}"),   // last component > 100 bytes
                    (TarEntryFormat.Ustar, $"opt/app/{new string('d', 120)}/{unicodeName}"),
                    (TarEntryFormat.Pax, $"opt/app/{tooLong}"),
                },
                entries);

            // Only the fallback entry has a PAX header; a layer without such names has none at all.
            using var plain = new MemoryStream();
            var small = new FileManifest { { Path.Combine(longDir, unicodeName), "/a/b.txt" } };
            await Layer.CreateAsync(small, plain);
            Assert.DoesNotContain("PaxHeaders", Encoding.ASCII.GetString(Gunzip(plain.ToArray())));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("short/name.txt", 0L, true)]
    [InlineData("short/name.txt", (1L << 33) - 1, true)]   // largest ustar size
    [InlineData("short/name.txt", 1L << 33, false)]        // 8 GiB
    public void FitsUstar_checks_size(string name, long size, bool expected)
        => Assert.Equal(expected, Layer.FitsUstar(name, size));

    public static TheoryData<string, bool> UstarNames => new()
    {
        { new string('n', 100), true },                                       // fills the name field
        { new string('n', 101), false },                                      // no '/' to split at
        { new string('p', 155) + "/" + new string('n', 100), true },          // 256 bytes: both fields full
        { new string('p', 155) + "/" + new string('n', 101), false },         // name part too long
        { new string('p', 156) + "/" + new string('n', 99), false },          // prefix part too long
        { "/" + new string('n', 100), false },                                // splitting at the root isn't allowed
        { new string('p', 101) + "/", false },                                // empty name part
        { new string('ü', 60) + "/" + new string('ü', 50), true },            // 120 + 1 + 100 UTF-8 bytes
        { new string('ü', 60) + "/" + new string('ü', 51), false },           // 102-byte name part
        // Fits only when split at the first '/': .NET tries the last one, so this uses PAX.
        { new string('p', 100) + "/" + new string('q', 60) + "/n", false },
    };

    [Theory]
    [MemberData(nameof(UstarNames))]
    public void FitsUstar_splits_long_names_at_the_last_slash(string name, bool expected)
        => Assert.Equal(expected, Layer.FitsUstar(name, 1));

    [Fact]
    public void Names_up_to_255_bytes_round_trip_through_TarReader_as_ustar()
    {
        // Every name FitsUstar accepts must be written by .NET's ustar writer and read back unchanged. This catches a
        // change in how .NET splits a name between the prefix and name fields.
        var random = new Random(2603);
        string[] parts = { "a", "ü", "lib", "Microsoft.PowerShell.Commands.Utility", new string('x', 40), new string('y', 99), new string('z', 120) };
        var names = new List<string>
        {
            // Shaped like the longest entries in the pwsh 7.6 tarball (up to 132 bytes).
            "var/runtime/powershell/Modules/Microsoft.PowerShell.Security/Microsoft.PowerShell.Security.psd1",
            "var/runtime/powershell/runtimes/linux-musl-x64/lib/netstandard2.0/Microsoft.Management.Infrastructure.Native.dll",
            "var/runtime/powershell/en-US/Microsoft.PowerShell.Commands.Utility.resources.dll/" + new string('r', 30),
        };
        for (int n = 0; n < 2000; n++)
        {
            names.Add(string.Join('/', Enumerable.Range(0, random.Next(1, 7)).Select(_ => parts[random.Next(parts.Length)])));
        }

        int split = 0;
        foreach (string name in names.Where(name => Layer.FitsUstar(name, 0)))
        {
            using var buffer = new MemoryStream();
            using (var writer = new TarWriter(buffer, TarEntryFormat.Ustar, leaveOpen: true))
            {
                writer.WriteEntry(new UstarTarEntry(TarEntryType.RegularFile, name));
            }
            buffer.Position = 0;
            using var reader = new TarReader(buffer);
            TarEntry? entry = reader.GetNextEntry();
            Assert.NotNull(entry);
            Assert.Equal(TarEntryFormat.Ustar, entry.Format);
            Assert.Equal(name, entry.Name);
            if (Encoding.UTF8.GetByteCount(name) > 100)
            {
                split++;
            }
        }
        Assert.True(split >= 100, $"only {split} names used the prefix field");
    }

    [Fact]
    public async Task Layers_with_many_long_names_build_and_read_back()
    {
        // Random deep and long paths: each entry must come out as ustar or PAX and read back with its exact name.
        var random = new Random(2602);
        string[] parts = { "a", "bb", "ü", "dir", new string('x', 40), new string('y', 99), new string('v', 150) };
        string root = Directory.CreateTempSubdirectory("fix-names-").FullName;
        try
        {
            var manifest = new FileManifest();
            var expected = new HashSet<string>(StringComparer.Ordinal);
            for (int n = 0; n < 300; n++)
            {
                string rel = string.Join('/', Enumerable.Range(0, random.Next(1, 5)).Select(_ => parts[random.Next(parts.Length)])) + $"-{n}.txt";
                string local = Path.Combine(root, $"{n}.txt");
                await File.WriteAllTextAsync(local, n.ToString());
                manifest.Add(local, "/" + rel);
                expected.Add(rel);
                if (n % 3 == 0 && rel.Contains('/'))
                {
                    string folder = rel[..rel.LastIndexOf('/')];
                    manifest.AddEmptyDirectory("/" + folder);
                    expected.Add(Encoding.UTF8.GetByteCount(folder) + 1 > 100 ? folder : folder + "/");
                }
            }

            using var blob = new MemoryStream();
            await Layer.CreateAsync(manifest, blob);
            byte[] tarBytes = Gunzip(blob.ToArray());
            AssertNoEmptyNameFields(tarBytes);
            using var tar = new TarReader(new MemoryStream(tarBytes));
            var formats = new Dictionary<TarEntryFormat, int>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            while (tar.GetNextEntry() is { } e)
            {
                names.Add(e.Name);
                formats[e.Format] = formats.GetValueOrDefault(e.Format) + 1;
            }
            Assert.Equal(expected, names);
            Assert.True(formats.GetValueOrDefault(TarEntryFormat.Ustar) > 0 && formats.GetValueOrDefault(TarEntryFormat.Pax) > 0,
                string.Join(", ", formats.Select(f => $"{f.Key}={f.Value}")));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>Fails if any ustar header leaves the name field empty and puts the whole path in the prefix.</summary>
    private static void AssertNoEmptyNameFields(byte[] tarBytes)
    {
        for (int offset = 0; offset + 512 <= tarBytes.Length; offset += 512)
        {
            ReadOnlySpan<byte> header = tarBytes.AsSpan(offset, 512);
            if (!header.Slice(257, 5).SequenceEqual("ustar"u8))
            {
                continue;
            }
            bool emptyName = header[0] == 0;
            bool hasPrefix = header[345] != 0;
            Assert.False(emptyName && hasPrefix, $"header at {offset} has an empty name field and a prefix");
        }
    }

    private static byte[] Gunzip(byte[] gz)
    {
        using var input = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    [Fact]
    public void Layer_falls_back_to_PAX_only_for_entries_ustar_cannot_hold()
    {
        UnixFileMode mode = FileManifestEntry.ExecutableMode;
        Assert.Equal(TarEntryFormat.Ustar, Layer.CreateEntry(TarEntryType.RegularFile, "a/big.bin", mode, (1L << 33) - 1).Format);
        Assert.Equal(TarEntryFormat.Pax, Layer.CreateEntry(TarEntryType.RegularFile, "a/big.bin", mode, 1L << 33).Format);
        Assert.Equal(TarEntryFormat.Pax, Layer.CreateEntry(TarEntryType.RegularFile, new string('n', 101), mode, 1).Format);

        // A long directory name loses its trailing slash instead of leaving the ustar name field empty.
        TarEntry dir = Layer.CreateEntry(TarEntryType.Directory, "opt/" + new string('d', 99) + "/", mode, 0);
        Assert.Equal(TarEntryFormat.Ustar, dir.Format);
        Assert.Equal("opt/" + new string('d', 99), dir.Name);
    }
}
