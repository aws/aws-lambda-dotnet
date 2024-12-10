namespace Amazon.Lambda.TestTool.Services.IO;

/// <summary>
/// Provides functionality to manage and retrieve directory-related information.
/// </summary>
public interface IDirectoryManager
{
    /// <summary>
    /// Gets the current working directory of the application.
    /// </summary>
    /// <returns>The full path of the current working directory.</returns>
    string GetCurrentDirectory();
}