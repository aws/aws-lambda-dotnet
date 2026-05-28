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

    [Fact]
    public void CallbackException_BaseClassCtors()
    {
        var empty = new CallbackException();
        Assert.IsAssignableFrom<DurableExecutionException>(empty);

        var withMsg = new CallbackException("cb error");
        Assert.Equal("cb error", withMsg.Message);

        var inner = new InvalidOperationException("inner");
        var wrapping = new CallbackException("outer", inner);
        Assert.Same(inner, wrapping.InnerException);
    }

    [Fact]
    public void CallbackException_InitProperties()
    {
        var ex = new CallbackException("rejected")
        {
            CallbackId = "cb-1",
            ErrorType = "ExternalSystemError",
            ErrorData = "{\"reviewer\":\"jane\"}",
            OriginalStackTrace = new[] { "at A.B()" }
        };

        Assert.Equal("cb-1", ex.CallbackId);
        Assert.Equal("ExternalSystemError", ex.ErrorType);
        Assert.Equal("{\"reviewer\":\"jane\"}", ex.ErrorData);
        Assert.Single(ex.OriginalStackTrace!);
    }

    [Fact]
    public void CallbackFailedException_IsCallbackException()
    {
        var ex = new CallbackFailedException("rejected") { CallbackId = "cb-1" };
        Assert.IsAssignableFrom<CallbackException>(ex);
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
        Assert.Equal("rejected", ex.Message);
        Assert.Equal("cb-1", ex.CallbackId);
    }

    [Fact]
    public void CallbackFailedException_AllCtors()
    {
        Assert.NotNull(new CallbackFailedException());
        Assert.Equal("m", new CallbackFailedException("m").Message);
        var inner = new Exception("inner");
        Assert.Same(inner, new CallbackFailedException("m", inner).InnerException);
    }

    [Fact]
    public void CallbackTimeoutException_IsCallbackException()
    {
        var ex = new CallbackTimeoutException("timed out") { CallbackId = "cb-1" };
        Assert.IsAssignableFrom<CallbackException>(ex);
        Assert.Equal("timed out", ex.Message);
    }

    [Fact]
    public void CallbackTimeoutException_AllCtors()
    {
        Assert.NotNull(new CallbackTimeoutException());
        Assert.Equal("m", new CallbackTimeoutException("m").Message);
        var inner = new Exception("inner");
        Assert.Same(inner, new CallbackTimeoutException("m", inner).InnerException);
    }

    [Fact]
    public void CallbackSubmitterException_IsCallbackException()
    {
        var inner = new StepException("submitter failed");
        var ex = new CallbackSubmitterException("submitter failed", inner);
        Assert.IsAssignableFrom<CallbackException>(ex);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void CallbackSubmitterException_AllCtors()
    {
        Assert.NotNull(new CallbackSubmitterException());
        Assert.Equal("m", new CallbackSubmitterException("m").Message);
    }
}
