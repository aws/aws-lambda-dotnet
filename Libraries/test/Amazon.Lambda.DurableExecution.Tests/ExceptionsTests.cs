using Amazon.Lambda.DurableExecution;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ExceptionsTests
{
    [Fact]
    public void DurableExecutionException_IsBaseException()
    {
        var ex = new DurableExecutionException("test error");
        Assert.IsAssignableFrom<Exception>(ex);
        Assert.Equal("test error", ex.Message);
    }

    [Fact]
    public void DurableExecutionException_WrapsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new DurableExecutionException("outer", inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void DurableExecutionException_ParameterlessCtor()
    {
        var ex = new DurableExecutionException();
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void StepException_ParameterlessCtor()
    {
        var ex = new StepException();
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
    }

    [Fact]
    public void StepException_MessageOnlyCtor()
    {
        var ex = new StepException("step blew up");
        Assert.Equal("step blew up", ex.Message);
    }

    [Fact]
    public void StepException_WithInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new StepException("wrapped", inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void StepException_HasErrorProperties()
    {
        var ex = new StepException("step failed")
        {
            ErrorType = "System.TimeoutException",
            ErrorData = "operation timed out",
            OriginalStackTrace = new[] { "at Foo.Bar()", "at Baz.Qux()" }
        };

        Assert.IsAssignableFrom<DurableExecutionException>(ex);
        Assert.Equal("System.TimeoutException", ex.ErrorType);
        Assert.Equal("operation timed out", ex.ErrorData);
        Assert.Equal(2, ex.OriginalStackTrace!.Count);
    }
}
