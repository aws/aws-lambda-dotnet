namespace Amazon.Lambda.TestTool.Services.IO;

/// <inheritdoc cref="IDirectoryManager"/>
public class DirectoryManager : IDirectoryManager
{
    /// <inheritdoc />
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
}