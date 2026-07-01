// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.Analyzers.Tests
{
    /// <summary>
    /// A faithful (signature-only) source copy of the durable-execution surface the analyzers match
    /// against. Injected into every test compilation as source so tests do not need to reference the
    /// real Amazon.Lambda.DurableExecution package (which pulls AWSSDK and is awkward in the analyzer
    /// test harness). The analyzers resolve these by metadata name, so the stub namespace and member
    /// shapes must match the real SDK exactly.
    /// </summary>
    internal static class DurableStubs
    {
        internal const string Source = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.DurableExecution
{
    public interface IStepContext { int AttemptNumber { get; } string OperationId { get; } }
    public interface IConditionCheckContext { }
    public interface IWaitForCallbackContext { }
    public interface IExecutionContext { string DurableExecutionArn { get; } }
    public interface ICallback<T> { string CallbackId { get; } Task<T> GetResultAsync(CancellationToken ct = default); }

    public sealed class StepConfig { }
    public sealed class ChildContextConfig { }
    public sealed class CallbackConfig { }
    public sealed class WaitForCallbackConfig { }
    public sealed class InvokeConfig { }
    public sealed class ParallelConfig { }
    public sealed class MapConfig { }
    public sealed class WaitForConditionConfig<TState> { }
    public readonly struct DurableBranch<T> { public DurableBranch(string name, Func<IDurableContext, CancellationToken, Task<T>> func) { } }
    public interface IBatchResult<T> { }

    public interface IDurableContext
    {
        IExecutionContext ExecutionContext { get; }

        Task<T> StepAsync<T>(Func<IStepContext, CancellationToken, Task<T>> func, string name = null, StepConfig config = null, CancellationToken cancellationToken = default);
        Task StepAsync(Func<IStepContext, CancellationToken, Task> func, string name = null, StepConfig config = null, CancellationToken cancellationToken = default);

        Task WaitAsync(TimeSpan duration, string name = null, CancellationToken cancellationToken = default);

        Task<T> RunInChildContextAsync<T>(Func<IDurableContext, CancellationToken, Task<T>> func, string name = null, ChildContextConfig config = null, CancellationToken cancellationToken = default);
        Task RunInChildContextAsync(Func<IDurableContext, CancellationToken, Task> func, string name = null, ChildContextConfig config = null, CancellationToken cancellationToken = default);

        Task<ICallback<T>> CreateCallbackAsync<T>(string name = null, CallbackConfig config = null, CancellationToken cancellationToken = default);
        Task<T> WaitForCallbackAsync<T>(Func<string, IWaitForCallbackContext, CancellationToken, Task> submitter, string name = null, WaitForCallbackConfig config = null, CancellationToken cancellationToken = default);

        Task<TResult> InvokeAsync<TPayload, TResult>(string functionName, TPayload payload, string name = null, InvokeConfig config = null, CancellationToken cancellationToken = default);

        Task<TState> WaitForConditionAsync<TState>(Func<TState, IConditionCheckContext, CancellationToken, Task<TState>> check, WaitForConditionConfig<TState> config, string name = null, CancellationToken cancellationToken = default);

        Task<IBatchResult<T>> ParallelAsync<T>(IReadOnlyList<Func<IDurableContext, CancellationToken, Task<T>>> branches, string name = null, ParallelConfig config = null, CancellationToken cancellationToken = default);
        Task<IBatchResult<T>> ParallelAsync<T>(IReadOnlyList<DurableBranch<T>> branches, string name = null, ParallelConfig config = null, CancellationToken cancellationToken = default);

        Task<IBatchResult<TResult>> MapAsync<TItem, TResult>(IReadOnlyList<TItem> items, Func<IDurableContext, TItem, int, IReadOnlyList<TItem>, CancellationToken, Task<TResult>> func, string name = null, MapConfig config = null, CancellationToken cancellationToken = default);
    }
}
";
    }
}
