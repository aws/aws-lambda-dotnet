using System;
using Xunit;
namespace SnapshotRestore.Registry.Tests;

public class RestoreHooksRegistryTests
{
    private DateTimeOffset? _func1InvokeTime = null;
    private DateTimeOffset? _func2InvokeTime = null;
    
    [Fact]
    public async Task RegisterBeforeSnapshotAsyncShouldAddValueTaskToRegistryAsync()
    {
        // Arrange
        _func1InvokeTime = null;
        _func2InvokeTime = null;
        RestoreHooksRegistry registry = new(Console.WriteLine);
        registry.RegisterBeforeSnapshot(TestFunc1);
        registry.RegisterBeforeSnapshot(TestFunc2);
        
        // Act
        await registry.InvokeBeforeSnapshotCallbacks();

        // Assert
        Assert.NotNull(_func1InvokeTime);
        Assert.NotNull(_func2InvokeTime);
        Assert.True(_func2InvokeTime < _func1InvokeTime, "func2InvokeTime should be less than func1InvokeTime, " +
                                                         "since func2InvokeTime was registered second, and BeforeSnapshot " +
                                                         "tasks are called in the reverse order they were registered.");
    }

    [Fact]
    public async Task RegisterAfterRestoreAsync_ShouldAddValueTaskToRegistryAsync()
    {
        // Arrange
        _func1InvokeTime = null;
        _func2InvokeTime = null;
        RestoreHooksRegistry registry = new(Console.WriteLine);
        registry.RegisterAfterRestore(TestFunc1);
        registry.RegisterAfterRestore(TestFunc2);
        
        // Act
        await registry.InvokeAfterRestoreCallbacks();

        // Assert
        Assert.NotNull(_func1InvokeTime);
        Assert.NotNull(_func2InvokeTime);
        Assert.True(_func1InvokeTime < _func2InvokeTime, "func1InvokeTime should be less than or equal to " +
                                                         "func2InvokeTime, since it was registered first, and AfterRestore " +
                                                        "tasks are called in the order they were registered.");
    }

    [Fact]
    public async Task LoggerIsNotRequired()
    {
        // Arrange
        RestoreHooksRegistry registry = new(logger: null);
        registry.RegisterAfterRestore(TestFunc1);
        registry.RegisterAfterRestore(TestFunc2);

        Exception? exception = null;

        // Act
        try
        {
            await registry.InvokeAfterRestoreCallbacks();
        }
        catch (Exception e)
        {
            exception = e;
        }

        // Assert
        Assert.Null(exception);
    }
    

    private ValueTask TestFunc1() 
    {
        _func1InvokeTime = DateTimeOffset.UtcNow;
        Thread.Sleep(10); // So the times of func1 and func2 aren't ever exactly equal
        return ValueTask.CompletedTask;
    }
    
    private ValueTask TestFunc2() 
    {
        _func2InvokeTime = DateTimeOffset.UtcNow;
        Thread.Sleep(10); // So the times of func1 and func2 aren't ever exactly equal
        return ValueTask.CompletedTask;
    }
}