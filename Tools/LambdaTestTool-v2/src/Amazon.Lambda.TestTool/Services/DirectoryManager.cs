namespace Amazon.Lambda.TestTool.Services;

public class DirectoryManager : IDirectoryManager
{
    public string GetCurrentDirectory() => Directory.GetCurrentDirectory();
}