// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// Tracks waits, chained invokes and callbacks that finished outside the invocation, while the execution was
/// suspended. They have no span of their own in the invocation that notices, so the plugin exports one segment
/// for each, once, in the invocation that observes the completion.
/// </summary>
internal sealed class ExternalCompletions
{
    private readonly Dictionary<string, OperationInfo> _pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> _exported = new(StringComparer.Ordinal);

    internal static bool IsExternal(OperationInfo info) =>
        info.Type is OperationTypes.Wait or OperationTypes.ChainedInvoke or OperationTypes.Callback;

    internal static bool IsFinished(string? status) =>
        status is OperationStatuses.Succeeded or OperationStatuses.Failed or OperationStatuses.TimedOut
            or OperationStatuses.Stopped or OperationStatuses.Cancelled;

    /// <summary>Operations still waiting to be exported, in the order they were observed.</summary>
    internal IReadOnlyList<OperationInfo> Pending => _pending.Values.ToList();

    internal bool IsPending(string id) => _pending.ContainsKey(id);

    internal void Observe(
        IReadOnlyDictionary<string, OperationInfo> updates, IReadOnlyDictionary<string, OperationInfo> operations)
    {
        foreach (var info in updates.Values)
        {
            if (!IsExternal(info) || !IsFinished(info.Status) || _exported.Contains(info.Id))
                continue;

            // A callback of WaitForCallback has no name of its own. Name it after its parent so it can be found.
            var completion = info;
            if (info.Type == OperationTypes.Callback && info.Name == null && info.ParentId != null
                && operations.TryGetValue(info.ParentId, out var parent)
                && parent.Type == OperationTypes.Context && parent.SubType == OperationSubTypes.WaitForCallback
                && parent.Name != null)
            {
                completion = new OperationInfo
                {
                    Id = info.Id, Name = parent.Name + "-callback", Type = info.Type, SubType = info.SubType,
                    ParentId = info.ParentId, Status = info.Status, StartTimestamp = info.StartTimestamp,
                    EndTimestamp = info.EndTimestamp, Attempt = info.Attempt, IsReplay = info.IsReplay,
                };
            }
            _pending[info.Id] = completion;
        }
    }

    /// <summary>
    /// True when an end report must not produce a span: the operation was exported already, or it is a replay of
    /// one whose completion this invocation has not seen.
    /// </summary>
    internal bool ShouldSkip(OperationInfo info) =>
        IsExternal(info) && (_exported.Contains(info.Id) || (info.IsReplay && !_pending.ContainsKey(info.Id)));

    internal void MarkExported(OperationInfo info)
    {
        if (!IsExternal(info))
            return;

        _exported.Add(info.Id);
        _pending.Remove(info.Id);
    }

    internal OperationInfo? Get(string id) => _pending.TryGetValue(id, out var info) ? info : null;

    internal void Clear()
    {
        _pending.Clear();
        _exported.Clear();
    }
}
