using System.Collections;

namespace Amazon.Lambda.TestTool.Services.IO;

/// <inheritdoc cref="IEnvironmentManager"/>
public class EnvironmentManager : IEnvironmentManager
{
    /// <inheritdoc />
    public IDictionary GetEnvironmentVariables() => Environment.GetEnvironmentVariables();
}