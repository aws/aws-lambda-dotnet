// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

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

    #region InvokeException tree

    [Fact]
    public void InvokeException_IsDurableExecutionException()
    {
        var ex = new InvokeException("invoke failed");
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
        Assert.Equal("invoke failed", ex.Message);
    }

    [Fact]
    public void InvokeException_ParameterlessCtor()
    {
        var ex = new InvokeException();
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
    }

    [Fact]
    public void InvokeException_WrapsInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new InvokeException("outer", inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void InvokeException_HasInvokeProperties()
    {
        var ex = new InvokeException("boom")
        {
            FunctionName = "arn:aws:lambda:us-east-1:123:function:fn:prod",
            ErrorType = "System.TimeoutException",
            ErrorData = "{\"detail\":\"x\"}",
            OriginalStackTrace = new[] { "at A.B()" }
        };

        Assert.Equal("arn:aws:lambda:us-east-1:123:function:fn:prod", ex.FunctionName);
        Assert.Equal("System.TimeoutException", ex.ErrorType);
        Assert.Equal("{\"detail\":\"x\"}", ex.ErrorData);
        Assert.Single(ex.OriginalStackTrace!);
    }

    [Fact]
    public void InvokeFailedException_IsInvokeException()
    {
        var ex = new InvokeFailedException("boom") { FunctionName = "fn:prod" };
        Assert.IsAssignableFrom<InvokeException>(ex);
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
        Assert.Equal("boom", ex.Message);
        Assert.Equal("fn:prod", ex.FunctionName);
    }

    [Fact]
    public void InvokeFailedException_AllCtorOverloads()
    {
        var inner = new InvalidOperationException("inner");
        Assert.IsAssignableFrom<DurableExecutionException>(new InvokeFailedException());
        Assert.Equal("m", new InvokeFailedException("m").Message);
        Assert.Same(inner, new InvokeFailedException("m", inner).InnerException);
    }

    [Fact]
    public void InvokeTimedOutException_IsInvokeException()
    {
        var ex = new InvokeTimedOutException("timed out");
        Assert.IsAssignableFrom<InvokeException>(ex);
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
        Assert.Equal("timed out", ex.Message);
    }

    [Fact]
    public void InvokeTimedOutException_AllCtorOverloads()
    {
        var inner = new TimeoutException("inner");
        Assert.IsAssignableFrom<DurableExecutionException>(new InvokeTimedOutException());
        Assert.Equal("m", new InvokeTimedOutException("m").Message);
        Assert.Same(inner, new InvokeTimedOutException("m", inner).InnerException);
    }

    [Fact]
    public void InvokeStoppedException_IsInvokeException()
    {
        var ex = new InvokeStoppedException("stopped");
        Assert.IsAssignableFrom<InvokeException>(ex);
        Assert.IsAssignableFrom<DurableExecutionException>(ex);
        Assert.Equal("stopped", ex.Message);
    }

    [Fact]
    public void InvokeStoppedException_AllCtorOverloads()
    {
        var inner = new InvalidOperationException("inner");
        Assert.IsAssignableFrom<DurableExecutionException>(new InvokeStoppedException());
        Assert.Equal("m", new InvokeStoppedException("m").Message);
        Assert.Same(inner, new InvokeStoppedException("m", inner).InnerException);
    }

    [Fact]
    public void InvokeException_SubclassesCaughtByBase()
    {
        // Verifies the documented pattern-matching contract: catch
        // (InvokeException) catches all three subclasses.
        Exception failed = new InvokeFailedException("fail");
        Exception timedOut = new InvokeTimedOutException("timeout");
        Exception stopped = new InvokeStoppedException("stop");

        Assert.True(failed is InvokeException);
        Assert.True(timedOut is InvokeException);
        Assert.True(stopped is InvokeException);
    }

    #endregion
}
