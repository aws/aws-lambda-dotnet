using Amazon.Lambda.TestTool.Services;
using Xunit.Abstractions;


namespace Amazon.Lambda.TestTool.Tests.Common;

public class TestOutputToolInteractiveService(ITestOutputHelper testOutputHelper) : IToolInteractiveService
{
    public void WriteErrorLine(string? message)
    {
        try
        {
            testOutputHelper.WriteLine("Error: " + message);
        }
        catch (Exception)
        {
            // This can happen when Xunit thinks there is no active test
        }
    }
    public void WriteLine(string? message)
    {
        try
        {
            testOutputHelper.WriteLine(message);
        }
        catch(Exception)
        {
            // This can happen when Xunit thinks there is no active test
        }
    }
}
