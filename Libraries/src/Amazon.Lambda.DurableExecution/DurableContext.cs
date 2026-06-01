// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

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
        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        var operationId = _idGenerator.NextId();
        var op = new StepOperation<T>(
            operationId, name, _idGenerator.ParentId, func, config, serializer, Logger,
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
            operationId, name, _idGenerator.ParentId, waitSeconds,
            _state, _terminationManager, _durableExecutionArn, _batcher);
        return op.ExecuteAsync(cancellationToken);
    }

    public Task<T> RunInChildContextAsync<T>(
        Func<IDurableContext, Task<T>> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default)
        => RunChildContext(func, name, config, cancellationToken);

    public async Task RunInChildContextAsync(
        Func<IDurableContext, Task> func,
        string? name = null,
        ChildContextConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        // Void child contexts don't carry a meaningful payload; the wrapper
        // returns null so the registered ILambdaSerializer is never asked to
        // serialize a real value.
        await RunChildContext<object?>(
            async (ctx) => { await func(ctx); return null; },
            name, config, cancellationToken);
    }

    private Task<T> RunChildContext<T>(
        Func<IDurableContext, Task<T>> func,
        string? name,
        ChildContextConfig? config,
        CancellationToken cancellationToken)
    {
        var serializer = LambdaSerializerHelper.GetRequired(LambdaContext);

        var operationId = _idGenerator.NextId();

        // Capture this DurableContext's collaborators; the child shares state,
        // termination, batcher, ARN, and Lambda context — but uses a child
        // OperationIdGenerator so its operation IDs are deterministically
        // namespaced under the parent op ID.
        IDurableContext ChildFactory(string parentOpId) => new DurableContext(
            _state, _terminationManager, _idGenerator.CreateChild(parentOpId),
            _durableExecutionArn, LambdaContext, _batcher);

        var op = new ChildContextOperation<T>(
            operationId, name, _idGenerator.ParentId, func, config, serializer, ChildFactory,
            _state, _terminationManager, _durableExecutionArn, _batcher);
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

    public Task<T> WaitForCallbackAsync<T>(
        Func<string, IWaitForCallbackContext, Task> submitter,
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
        Func<string, IWaitForCallbackContext, Task> submitter,
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
        return RunInChildContextAsync<T>(
            async childCtx =>
            {
                var callback = await childCtx.CreateCallbackAsync<T>(
                    name: callbackName,
                    config: callbackConfig,
                    cancellationToken: cancellationToken);

                await childCtx.StepAsync(
                    async (stepCtx) =>
                    {
                        var submitterCtx = new WaitForCallbackContext(stepCtx.Logger);
                        await submitter(callback.CallbackId, submitterCtx);
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
