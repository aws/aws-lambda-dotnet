// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// Gives an <see cref="Activity"/> a span id of our choosing.
/// </summary>
/// <remarks>
/// <para>The public API cannot do this: <c>SpanId</c> is read-only, and a parent context or <c>SetParentId</c>
/// only chooses the parent. A durable execution needs it, because spans in one invocation must name spans that
/// another invocation exports (the Workflow span, the first segment of an operation), and nothing is stored
/// between invocations.</para>
/// <para>This writes the private <c>_spanId</c> and <c>_id</c> fields through <see cref="UnsafeAccessorAttribute"/>,
/// which is generated at compile time and is safe for trimming and Native AOT. The fields are not a contract. If
/// a runtime renames them the first call throws, the override switches itself off, and spans get random ids:
/// parenting inside one invocation still works, references across invocations do not. A test fails when the
/// fields are missing, so a new runtime is noticed.</para>
/// <para>Listeners notified when the activity starts have already seen the random id. Export and child spans see
/// the one set here.</para>
/// </remarks>
internal static class ActivityIdOverride
{
    private static volatile bool _disabled;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_spanId")]
    private static extern ref string? SpanIdField(Activity activity);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_id")]
    private static extern ref string? IdField(Activity activity);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_parentSpanId")]
    private static extern ref string? ParentSpanIdField(Activity activity);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_parentId")]
    private static extern ref string? ParentIdField(Activity activity);

    /// <summary>True once an override failed. Spans then have random ids.</summary>
    internal static bool IsDisabled => _disabled;

    /// <summary>Switches the override back on. For tests.</summary>
    internal static void Reset() => _disabled = false;

    /// <summary>
    /// Sets the span id of a started activity, and optionally removes its parent so it exports as a root.
    /// Returns false, and leaves the activity unchanged, when the override is unavailable.
    /// </summary>
    internal static bool TrySet(Activity? activity, string spanId, bool asRoot = false)
    {
        if (activity == null || _disabled)
            return false;

        try
        {
            var traceId = activity.TraceId.ToHexString();
            var flags = (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0 ? "01" : "00";

            SpanIdField(activity) = spanId;
            IdField(activity) = $"00-{traceId}-{spanId}-{flags}";
            if (asRoot)
            {
                ParentSpanIdField(activity) = null;
                ParentIdField(activity) = null;
            }
            return true;
        }
        catch (Exception ex) when (ex is MissingFieldException or MissingMemberException or NullReferenceException)
        {
            _disabled = true;
            return false;
        }
    }
}
