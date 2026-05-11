using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class AssemblyLoadTests
{
    [Fact]
    public void DurableExecutionAssembly_Loads()
    {
        var assembly = typeof(AssemblyMarker).Assembly;
        Assert.Equal("Amazon.Lambda.DurableExecution", assembly.GetName().Name);
    }
}
