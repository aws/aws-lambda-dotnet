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

/// <summary>Folder entries added with FileManifest.AddDirectory, and paths beneath a mapped file.</summary>
public class AddDirectoryTests
{
    [Fact]
    public async Task AddDirectory_emits_folder_entries()
    {
        string dir = Directory.CreateTempSubdirectory("fix-dirs-").FullName;
        try
        {
            string file = Path.Combine(dir, "bootstrap");
            await File.WriteAllTextAsync(file, "x");
            var manifest = new FileManifest();
            manifest.AddExecutable(file, "/opt/app/newdir/sub/bootstrap");
            manifest.AddDirectory("/opt/app/newdir");
            manifest.AddDirectory("/opt/app/newdir/sub/");

            var entries = await ReadEntriesAsync(manifest);
            Assert.Equal(
                new[] { "Directory opt/app/newdir/ 755", "Directory opt/app/newdir/sub/ 755", "RegularFile opt/app/newdir/sub/bootstrap 755" },
                entries);

            var clash = new FileManifest();
            clash.Add(file, "/opt/app/x");
            clash.AddDirectory("/opt/app/x");
            await Assert.ThrowsAsync<ArgumentException>(() => ReadEntriesAsync(clash));

            var folderUnderFile = new FileManifest();
            folderUnderFile.Add(file, "/a/b");
            folderUnderFile.AddDirectory("/a/b/c");
            await Assert.ThrowsAsync<ArgumentException>(() => ReadEntriesAsync(folderUnderFile));

            var fileUnderFile = new FileManifest { { file, "/a/b" }, { file, "/a/b/c" } };
            await Assert.ThrowsAsync<ArgumentException>(() => ReadEntriesAsync(fileUnderFile));

            var onlyFolders = new FileManifest();
            onlyFolders.AddDirectory("/opt/empty");
            Assert.Equal(new[] { "Directory opt/empty/ 755" }, await ReadEntriesAsync(onlyFolders));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static async Task<List<string>> ReadEntriesAsync(FileManifest manifest)
    {
        using var blob = new MemoryStream();
        await Layer.CreateAsync(manifest, blob);
        blob.Position = 0;
        using var gzip = new GZipStream(blob, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        var result = new List<string>();
        while (tar.GetNextEntry() is { } e) result.Add($"{e.EntryType} {e.Name} {Convert.ToString((int)e.Mode, 8)}");
        return result;
    }
}
