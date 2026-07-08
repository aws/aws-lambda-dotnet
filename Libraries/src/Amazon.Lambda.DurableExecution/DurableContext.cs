// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution.Internal;
using Microsoft.Extensions.Logging;

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
    private readonly WorkflowCancellation _workflowCancellation;
    private readonly OperationIdGenerator _idGenerator;
    private readonly string _durableExecutionArn;
    private readonly CheckpointBatcher? _batcher;
    private ILogger _logger;

    public DurableContext(
        ExecutionState state,
        TerminationManager terminationManager,
        WorkflowCancellation workflowCancellation,
        OperationIdGenerator idGenerator,
        string durableExecutionArn,
        ILambdaContext lambdaContext,
        CheckpointBatcher? batcher = null)
    {
        _state = state;
        _terminationManager = terminationManager;
        _workflowCancellation = workflowCancellation;
        _idGenerator = idGenerator;
        _durableExecutionArn = durableExecutionArn;
        _batcher = batcher;
        LambdaContext = lambdaContext;
        _logger = new ReplayAwareLogger(new LambdaCoreLogger(), state, modeAware: true);
    }

    public ILogger Logger => _logger;
    public IExecutionContext ExecutionContext => new DurableExecutionContext(_durableExecutionArn);
    public ILambdaContext LambdaContext { get; }

    public void ConfigureLogger(LoggerConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        // If the user supplies a CustomLogger, wrap it. Otherwise re-wrap the
        // existing inner logger (unwrapping if it was already a ReplayAwareLogger)
        // so toggling ModeAware works without losing the previous custom logger.
        var inner = config.CustomLogger
            ?? (_logger is ReplayAwareLogger existing ? existing.Inner : _logger);
        _logger = new ReplayAwareLogger(inner, _state, config.ModeAware);
    }

    public Task<T> StepAsync<T>(
        Func<IStepContext, CancellationToken, Task<T>> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunStep(func, name, config, cancellationToken);

    public async Task StepAsync(
        Func<IStepContext, CancellationToken, Task> func,
        string? name = null,
        StepConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Void steps don't carry a meaningful payload — wrap with an object?-typed
        // step that always returns null. The serializer isn't actually invoked
        // with a non-null value, so any registered ILambdaSerializer suffices.
        await RunStep<object?>(
            async (ctx, ct) => { await func(ctx, ct); return null; },
            name, config, cancellationToken);
    }

    private Task<T> RunStep<T>(
        Func<IStepContext, CancellationToken, Task<T>> func,
        string? name,
        StepConfig? config,
        CancellationToken cancellationToken)
    {
        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        var operationId = _idGenerator.NextId();
        var op = new StepOperation<T>(
            operationId, name, _idGenerator.ParentId, func, config, serializer, Logger,
            _state, _terminationManager, _workflowCancellation, _durableExecutionArn, _batcher);
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
            operationId, name, _idGenerator.ParentId, waitSeconds,
            _state, _terminationManager, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task<T> RunInChildContextAsync<T>(
        Func<IDurableContext, CancellationToken, Task<T>> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunChildContext(func, name, config, cancellationToken);

    public async Task RunInChildContextAsync(
        Func<IDurableContext, CancellationToken, Task> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Void child contexts don't carry a meaningful payload; the wrapper
        // returns null so the registered ILambdaSerializer is never asked to
        // serialize a real value.
        await RunChildContext<object?>(
            async (ctx, ct) => { await func(ctx, ct); return null; },
            name, config, cancellationToken);
    }

    public Task<TState> WaitForConditionAsync<TState>(
        Func<TState, IConditionCheckContext, CancellationToken, Task<TState>> check,
        WaitForConditionConfig<TState> config,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(config.WaitStrategy);

        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);
        var operationId = _idGenerator.NextId();
        var op = new WaitForConditionOperation<TState>(
            operationId, name, _idGenerator.ParentId, check, config, serializer, Logger,
            _state, _terminationManager, _workflowCancellation, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    private Task<T> RunChildContext<T>(
        Func<IDurableContext, CancellationToken, Task<T>> func,
        string? name,
        ChildContextConfig? config,
        CancellationToken cancellationToken)
    {
        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        var operationId = _idGenerator.NextId();

        var op = new ChildContextOperation<T>(
            operationId, name, _idGenerator.ParentId, func, config, serializer, MakeChildFactory(),
            _state, _terminationManager, _workflowCancellation, _durableExecutionArn, _batcher,
            isVirtual: config?.NestingType == NestingType.Flat);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task<ICallback<T>> CreateCallbackAsync<T>(
        string? name = null,
        CallbackConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunCallback<T>(name, config, cancellationToken);

    private Task<ICallback<T>> RunCallback<T>(
        string? name,
        CallbackConfig? config,
        CancellationToken cancellationToken)
    {
        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        var operationId = _idGenerator.NextId();
        var op = new CallbackOperation<T>(
            operationId, name, _idGenerator.ParentId, config, serializer,
            _state, _terminationManager, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task<IBatchResult<T>> ParallelAsync<T>(
        IReadOnlyList<Func<IDurableContext, CancellationToken, Task<T>>> branches,
        string? name = null,
        ParallelConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunParallel(WrapToDurableBranches(branches), name, config, cancellationToken);

    public Task<IBatchResult<T>> ParallelAsync<T>(
        IReadOnlyList<DurableBranch<T>> branches,
        string? name = null,
        ParallelConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunParallel(branches, name, config, cancellationToken);

    private static IReadOnlyList<DurableBranch<T>> WrapToDurableBranches<T>(
        IReadOnlyList<Func<IDurableContext, CancellationToken, Task<T>>> branches)
    {
        if (branches == null) throw new ArgumentNullException(nameof(branches));

        var result = new DurableBranch<T>[branches.Count];
        for (var i = 0; i < branches.Count; i++)
        {
            var func = branches[i];
            if (func == null)
                throw new ArgumentException($"Branch at index {i} is null.", nameof(branches));
            // Default name is the index — surfaces in execution traces and on
            // IBatchItem<T>.Name. Users wanting custom names use the
            // DurableBranch<T> overload.
            result[i] = new DurableBranch<T>(i.ToString(System.Globalization.CultureInfo.InvariantCulture), func);
        }
        return result;
    }

    private Task<IBatchResult<T>> RunParallel<T>(
        IReadOnlyList<DurableBranch<T>> branches,
        string? name,
        ParallelConfig? config,
        CancellationToken cancellationToken)
    {
        if (branches == null) throw new ArgumentNullException(nameof(branches));
        for (var i = 0; i < branches.Count; i++)
        {
            if (branches[i] == null)
                throw new ArgumentException($"Branch at index {i} is null.", nameof(branches));
            if (branches[i].Func == null)
                throw new ArgumentException($"Branch at index {i} has a null Func.", nameof(branches));
        }

        var effectiveConfig = config ?? new ParallelConfig();

        var serializer = LambdaContext.Serializer
            ?? throw new InvalidOperationException(
                "No ILambdaSerializer is registered on ILambdaContext.Serializer. " +
                "Register a serializer via LambdaBootstrapBuilder.Create(handler, serializer) " +
                "(or in tests, set TestLambdaContext.Serializer).");

        var operationId = _idGenerator.NextId();
        var op = new Internal.ParallelOperation<T>(
            operationId, name, _idGenerator.ParentId, branches, effectiveConfig, serializer, MakeChildFactory(),
            _state, _terminationManager, _workflowCancellation, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task<IBatchResult<TResult>> MapAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<IDurableContext, TItem, int, IReadOnlyList<TItem>, CancellationToken, Task<TResult>> func,
        string? name = null,
        MapConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunMap(items, func, name, config, cancellationToken);

    private Task<IBatchResult<TResult>> RunMap<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<IDurableContext, TItem, int, IReadOnlyList<TItem>, CancellationToken, Task<TResult>> func,
        string? name,
        MapConfig? config,
        CancellationToken cancellationToken)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (func == null) throw new ArgumentNullException(nameof(func));

        var effectiveConfig = config ?? new MapConfig();

        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        var operationId = _idGenerator.NextId();
        var op = new Internal.MapOperation<TItem, TResult>(
            operationId, name, _idGenerator.ParentId, items, func, effectiveConfig, serializer, MakeChildFactory(),
            _state, _terminationManager, _workflowCancellation, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task<T> WaitForCallbackAsync<T>(
        Func<string, IWaitForCallbackContext, CancellationToken, Task> submitter,
        string? name = null,
        WaitForCallbackConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunWaitForCallback<T>(submitter, name, config, cancellationToken);

    /// <summary>
    /// Composes WaitForCallback over RunInChildContextAsync + CreateCallbackAsync
    /// + StepAsync(submitter) + callback.GetResultAsync.
    /// </summary>
    /// <remarks>
    /// Sub-operation naming follows kebab-style: <c>"{name}-callback"</c> and
    /// <c>"{name}-submitter"</c>. When the parent <paramref name="name"/> is null,
    /// the inner ops are also nameless (no leading hyphen).
    /// <para>
    /// <see cref="ChildContextConfig.ErrorMapping"/> remaps a submitter
    /// <see cref="StepException"/> to <see cref="CallbackSubmitterException"/>.
    /// Callback errors (<see cref="CallbackException"/>) pass through unchanged.
    /// </para>
    /// </remarks>
    private Task<T> RunWaitForCallback<T>(
        Func<string, IWaitForCallbackContext, CancellationToken, Task> submitter,
        string? name,
        WaitForCallbackConfig? config,
        CancellationToken cancellationToken)
    {
        var callbackName = name == null ? null : $"{name}-callback";
        var submitterName = name == null ? null : $"{name}-submitter";

        var callbackConfig = config == null ? null : new CallbackConfig
        {
            Timeout = config.Timeout,
            HeartbeatTimeout = config.HeartbeatTimeout,
        };

        var stepConfig = config?.RetryStrategy == null
            ? null
            : new StepConfig { RetryStrategy = config.RetryStrategy };

        // Delegate to RunInChildContextAsync; the inner CreateCallbackAsync and
        // StepAsync calls each pull the registered ILambdaSerializer from
        // ILambdaContext.Serializer, so AOT and reflection-based scenarios share
        // the same code path.
        //
        // Pass the OUTER cancellationToken (not childCtx's linked token) into the
        // inner operations. Each inner operation will re-link the caller's token
        // with the workflow-shutdown CTS itself when it invokes its user Func, so
        // the submitter still observes both signals. Threading the already-linked
        // childToken through here would propagate the workflow-shutdown signal
        // into the inner operations' checkpoint writes (EnqueueAsync uses the
        // cancellationToken parameter directly), which would risk lost START /
        // SUCCEED checkpoints when termination fires mid-flush. See §7 of
        // docs/design/cancellation-design.md.
        return RunInChildContextAsync<T>(
            async (childCtx, _) =>
            {
                var callback = await childCtx.CreateCallbackAsync<T>(
                    name: callbackName,
                    config: callbackConfig,
                    cancellationToken: cancellationToken);

                await childCtx.StepAsync(
                    async (stepCtx, stepToken) =>
                    {
                        var submitterCtx = new WaitForCallbackContext(stepCtx.Logger);
                        await submitter(callback.CallbackId, submitterCtx, stepToken);
                    },
                    name: submitterName,
                    config: stepConfig,
                    cancellationToken: cancellationToken);

                return await callback.GetResultAsync(cancellationToken);
            },
            name,
            new ChildContextConfig
            {
                SubType = OperationSubTypes.WaitForCallback,
                ErrorMapping = MapWaitForCallbackException,
            },
            cancellationToken);
    }

    private static Exception MapWaitForCallbackException(Exception ex)
    {
        // Callback errors are already user-meaningful (CallbackFailed/Timeout
        // from inside the callback await). Pass through.
        if (ex is CallbackException) return ex;

        // The ChildContextOperation wraps thrown exceptions in
        // ChildContextException; unwrap to surface the underlying cause.
        if (ex is ChildContextException childEx)
        {
            // CallbackException thrown from GetResultAsync (callback completed
            // with FAILED/TIMED_OUT) — surface directly.
            //
            // Fresh-execution path: InnerException is the live exception object.
            // Replay path: InnerException is null but ErrorType carries the string.
            if (childEx.InnerException is CallbackException nestedLive)
                return nestedLive;
            if (IsCallbackErrorTypeString(childEx.ErrorType))
            {
                // Replay-side reconstruction: preserve subclass fidelity by
                // dispatching on the stored ErrorType FullName so a stored
                // CallbackTimeoutException remaps to CallbackTimeoutException
                // (not the more generic CallbackFailedException).
                return BuildCallbackExceptionForReplay(childEx);
            }

            // Submitter step exhausted retries → wrap as CallbackSubmitterException.
            // Fresh path: InnerException is the live StepException.
            if (childEx.InnerException is StepException stepLive)
            {
                return new CallbackSubmitterException(stepLive.Message, stepLive)
                {
                    ErrorType = stepLive.ErrorType,
                    ErrorData = stepLive.ErrorData,
                    OriginalStackTrace = stepLive.OriginalStackTrace,
                };
            }
            // Replay path: InnerException is null; ErrorType is the type string.
            if (childEx.ErrorType == typeof(StepException).FullName)
            {
                return new CallbackSubmitterException(childEx.Message, childEx)
                {
                    ErrorType = childEx.ErrorType,
                    ErrorData = childEx.ErrorData,
                    OriginalStackTrace = childEx.OriginalStackTrace,
                };
            }
        }

        // Anything else — surface unchanged so the user sees the original cause.
        return ex;
    }

    private static CallbackException BuildCallbackExceptionForReplay(ChildContextException childEx)
    {
        // Dispatch on the stored ErrorType FullName to preserve the original
        // subclass across replays. Caller has already verified
        // IsCallbackErrorTypeString(childEx.ErrorType) is true.
        if (childEx.ErrorType == typeof(CallbackTimeoutException).FullName)
        {
            return new CallbackTimeoutException(childEx.Message, childEx)
            {
                ErrorType = childEx.ErrorType,
                ErrorData = childEx.ErrorData,
                OriginalStackTrace = childEx.OriginalStackTrace,
            };
        }
        if (childEx.ErrorType == typeof(CallbackSubmitterException).FullName)
        {
            return new CallbackSubmitterException(childEx.Message, childEx)
            {
                ErrorType = childEx.ErrorType,
                ErrorData = childEx.ErrorData,
                OriginalStackTrace = childEx.OriginalStackTrace,
            };
        }
        if (childEx.ErrorType == typeof(CallbackException).FullName)
        {
            return new CallbackException(childEx.Message, childEx)
            {
                ErrorType = childEx.ErrorType,
                ErrorData = childEx.ErrorData,
                OriginalStackTrace = childEx.OriginalStackTrace,
            };
        }
        // CallbackFailedException.FullName (or any future callback subtype not
        // listed above) defaults to CallbackFailedException — the most general
        // "callback failed" surface that preserves user-catchable behavior.
        return new CallbackFailedException(childEx.Message, childEx)
        {
            ErrorType = childEx.ErrorType,
            ErrorData = childEx.ErrorData,
            OriginalStackTrace = childEx.OriginalStackTrace,
        };
    }

    private static bool IsCallbackErrorTypeString(string? errorType) =>
        errorType == typeof(CallbackFailedException).FullName
        || errorType == typeof(CallbackTimeoutException).FullName
        || errorType == typeof(CallbackSubmitterException).FullName
        || errorType == typeof(CallbackException).FullName;

    public Task<TResult> InvokeAsync<TPayload, TResult>(
        string functionName,
        TPayload payload,
        string? name = null,
        InvokeConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunInvoke<TPayload, TResult>(
            functionName, payload,
            name, config, cancellationToken);

    private Task<TResult> RunInvoke<TPayload, TResult>(
        string functionName,
        TPayload payload,
        string? name,
        InvokeConfig? config,
        CancellationToken cancellationToken)
    {
        // Argument validation runs synchronously at the call site (matches the
        // .NET convention of failing fast for misuse). Match Python/JS/Java
        // parity: only check for null/empty here; the durable execution service
        // enforces the qualified-ARN rule and surfaces a precise error when an
        // unqualified identifier is used.
        ArgumentNullException.ThrowIfNull(functionName);
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentException("Function name must not be empty or whitespace.", nameof(functionName));

        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        cancellationToken.ThrowIfCancellationRequested();

        var operationId = _idGenerator.NextId();
        var op = new InvokeOperation<TPayload, TResult>(
            operationId, name, _idGenerator.ParentId, functionName, payload, config,
            serializer,
            _state, _terminationManager, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the factory used by <see cref="ChildContextOperation{T}"/> (and
    /// each <see cref="Internal.ParallelOperation{T}"/> branch) to construct
    /// the inner <see cref="IDurableContext"/>. The child shares state,
    /// termination, workflow cancellation, batcher, ARN, and Lambda context —
    /// but uses a child <see cref="OperationIdGenerator"/> so its operation IDs
    /// are deterministically namespaced under the parent op ID.
    /// </summary>
    /// <summary>
    /// Builds the factory each operation uses to create the inner
    /// <see cref="DurableContext"/> its user function runs against.
    /// </summary>
    /// <remarks>
    /// The delegate takes <c>(operationId, reportedParentId, isVirtual)</c>:
    /// <list type="bullet">
    ///   <item><c>isVirtual == false</c> (the default child-context case): the
    ///       inner context's ID space and reported parent both root at
    ///       <c>operationId</c> via <see cref="OperationIdGenerator.CreateChild"/>;
    ///       <c>reportedParentId</c> is ignored.</item>
    ///   <item><c>isVirtual == true</c> (a <see cref="NestingType.Flat"/> branch):
    ///       inner-op IDs still root at <c>operationId</c> (so sibling branches
    ///       never collide), but inner ops report <c>reportedParentId</c> — the
    ///       parallel/map operation — as their parent, since the virtual branch
    ///       emits no CONTEXT checkpoint to reference.</item>
    /// </list>
    /// </remarks>
    private Func<string, string?, bool, IDurableContext> MakeChildFactory()
    {
        return (operationId, reportedParentId, isVirtual) => new DurableContext(
            _state, _terminationManager, _workflowCancellation,
            isVirtual
                ? _idGenerator.CreateVirtualChild(operationId, reportedParentId)
                : _idGenerator.CreateChild(operationId),
            _durableExecutionArn, LambdaContext, _batcher);
    }
}

internal sealed class WaitForCallbackContext : IWaitForCallbackContext
{
    public WaitForCallbackContext(ILogger logger)
    {
        Logger = logger;
    }

    public ILogger Logger { get; }
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
