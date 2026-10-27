using Amazon.OCI.Builder;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class FileManifestTests
{
    [Fact]
    public void Collection_initializer_adds_entries_in_order()
    {
        var files = new FileManifest
        {
            { "/local/app.dll", "/app/app.dll" },
            { "/local/config", "/app/config" },
        };

        Assert.Equal(2, files.Entries.Count);
        Assert.Equal("/local/app.dll", files.Entries[0].LocalPath);
        Assert.Equal("/app/config", files.Entries[1].ContainerPath);
    }
}
