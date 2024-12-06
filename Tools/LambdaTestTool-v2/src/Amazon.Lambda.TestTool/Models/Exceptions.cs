namespace Amazon.Lambda.TestTool.Models;

public abstract class TestToolException(string message, Exception? innerException = null)
    : Exception(message, innerException);