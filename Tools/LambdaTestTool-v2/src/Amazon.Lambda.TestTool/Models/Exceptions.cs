namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Represents a base exception that is thrown by the test tool.
/// </summary>
/// <param name="message"></param>
/// <param name="innerException"></param>
public abstract class TestToolException(string message, Exception? innerException = null)
    : Exception(message, innerException);