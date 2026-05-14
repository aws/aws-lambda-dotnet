using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Implementation of <see cref="IDurableContext"/>. Constructs and dispatches
/// per-operation classes (<see cref="StepOperation{T}"/>, <see cref="WaitOperation"/>);
/// the replay logic lives in those classes.
/// </summary>
internal sealed class DurableContext : IDurableContext
{
    private readonly ExecutionState _state;
    private readonly TerminationManager _terminationManager;
    private readonly OperationIdGenerator _idGenerator;
    private readonly string _durableExecutionArn;
    private readonly CheckpointBatcher? _batcher;

    public DurableContext(
        ExecutionState state,
        TerminationManager terminationManager,
        OperationIdGenerator idGenerator,
        string durableExecutionArn,
        ILambdaContext lambdaContext,
        CheckpointBatcher? batcher = null)
    {
        _state = state;
        _terminationManager = terminationManager;
        _idGenerator = idGenerator;
        _durableExecutionArn = durableExecutionArn;
        _batcher = batcher;
        LambdaContext = lambdaContext;
    }

    // Replay-safe logger ships in a follow-up PR; see IDurableContext.Logger doc.
    public ILogger Logger => NullLogger.Instance;
    public IExecutionContext ExecutionContext => new DurableExecutionContext(_durableExecutionArn);
    public ILambdaContext LambdaContext { get; }

    public Task<T> StepAsync<T>(
        Func<IStepContext, Task<T>> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunStep(func, name, config, cancellationToken);

    public async Task StepAsync(
        Func<IStepContext, Task> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Void steps don't carry a meaningful payload — wrap with an object?-typed
        // step that always returns null. The serializer isn't actually invoked
        // with a non-null value, so any registered ILambdaSerializer suffices.
        await RunStep<object?>(
            async (ctx) => { await func(ctx); return null; },
            name, config, cancellationToken);
    }

    private Task<T> RunStep<T>(
        Func<IStepContext, Task<T>> func,
        string? name,
        StepConfig? config,
        CancellationToken cancellationToken)
    {
        var serializer = LambdaContext.Serializer
            ?? throw new InvalidOperationException(
                "No ILambdaSerializer is registered on ILambdaContext.Serializer. " +
                "Register a serializer via LambdaBootstrapBuilder.Create(handler, serializer) " +
                "(or in tests, set TestLambdaContext.Serializer).");

        var operationId = _idGenerator.NextId();
        var op = new StepOperation<T>(
            operationId, name, func, config, serializer, Logger,
            _state, _terminationManager, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task WaitAsync(
        TimeSpan duration,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        // Service timer granularity is 1 second; sub-second waits would round to 0.
        // WaitOptions.WaitSeconds is integer in [1, 31_622_400] (1 second to ~1 year).
        if (duration < TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Wait duration must be at least 1 second.");

        if (duration > TimeSpan.FromSeconds(31_622_400))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Wait duration must be at most 31,622,400 seconds (~1 year).");

        cancellationToken.ThrowIfCancellationRequested();

        var operationId = _idGenerator.NextId();
        var waitSeconds = (int)Math.Max(1, Math.Ceiling(duration.TotalSeconds));
        var op = new WaitOperation(
            operationId, name, waitSeconds,
            _state, _terminationManager, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }
}

internal sealed class DurableExecutionContext : IExecutionContext
{
    public DurableExecutionContext(string durableExecutionArn)
    {
        DurableExecutionArn = durableExecutionArn;
    }

    public string DurableExecutionArn { get; }
}

internal sealed class StepContext : IStepContext
{
    public StepContext(string operationId, int attemptNumber, ILogger logger)
    {
        OperationId = operationId;
        AttemptNumber = attemptNumber;
        Logger = logger;
    }

    public ILogger Logger { get; }
    public int AttemptNumber { get; }
    public string OperationId { get; }
}
