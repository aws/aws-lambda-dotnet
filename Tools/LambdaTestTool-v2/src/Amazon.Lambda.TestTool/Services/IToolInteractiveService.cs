namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Defines methods for interacting with a tool's user interface through messages and error outputs.
/// </summary>
public interface IToolInteractiveService
{
    /// <summary>
    /// Writes a message to the standard output.
    /// </summary>
    /// <param name="message">The message to write. If <c>null</c>, a blank line is written.</param>
    void WriteLine(string? message);

    /// <summary>
    /// Writes an error message to the standard error output.
    /// </summary>
    /// <param name="message">The error message to write. If <c>null</c>, a blank line is written to the error output.</param>
    void WriteErrorLine(string? message);
}