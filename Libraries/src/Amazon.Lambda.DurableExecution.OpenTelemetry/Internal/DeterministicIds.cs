// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>
/// Trace and span ids that every invocation of an execution derives the same way. A span exported by one
/// invocation can then be the parent of, or be linked from, a span exported by another, with nothing stored
/// between them. The derivations match the JavaScript, Python and Java SDKs, so a trace that crosses SDKs stays
/// consistent.
/// </summary>
internal static class DeterministicIds
{
    private const string ZeroSpanId = "0000000000000000";
    private const string ZeroTraceId = "00000000000000000000000000000000";

    internal static bool IsValidTraceId(string? value) =>
        value is { Length: 32 } && value != ZeroTraceId && IsLowerHex(value);

    internal static bool IsValidSpanId(string? value) =>
        value is { Length: 16 } && value != ZeroSpanId && IsLowerHex(value);

    internal static bool IsLowerHex(string value)
    {
        foreach (var c in value)
        {
            if (!(c is >= '0' and <= '9' || c is >= 'a' and <= 'f'))
                return false;
        }
        return true;
    }

    private static string Sha256Hex(string value, int length) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..length];

    /// <summary>The trace id of an execution that has no propagated trace: SHA-256 of the ARN and start time.</summary>
    internal static string TraceIdFromArn(string executionArn, DateTimeOffset? executionStart)
    {
        var identity = executionStart is { } start
            ? $"{executionArn}:{start.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}"
            : executionArn;
        return Sha256Hex(identity, 32);
    }

    /// <summary>The id of the first span of an operation, scoped to the execution.</summary>
    internal static string SpanIdFromOperationId(string operationId, string executionArn) =>
        Sha256Hex(executionArn + ":" + operationId, 16);

    /// <summary>The id of the Workflow span.</summary>
    internal static string WorkflowSpanId(string executionArn) => NonZero(Sha256Hex("workflow:" + executionArn, 16));

    /// <summary>The id of the synthetic root that anchors an execution with no propagated parent.</summary>
    internal static string ExecutionRootSpanId(string executionArn) =>
        NonZero(Sha256Hex("execution-root:" + executionArn, 16));

    private static string NonZero(string spanId) => spanId == ZeroSpanId ? "0000000000000001" : spanId;
}
