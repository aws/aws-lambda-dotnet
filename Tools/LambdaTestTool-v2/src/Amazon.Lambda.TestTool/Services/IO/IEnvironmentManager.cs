using System.Collections;

namespace Amazon.Lambda.TestTool.Services.IO;

/// <summary>
/// Defines methods for managing and retrieving environment-related information.
/// </summary>
public interface IEnvironmentManager
{
    /// <summary>
    /// Retrieves all environment variables for the current process.
    /// </summary>
    IDictionary GetEnvironmentVariables();
}