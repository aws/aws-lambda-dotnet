// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution.Instrumentation;

namespace Amazon.Lambda.DurableExecution.OpenTelemetry;

/// <summary>Finds the trace context of a durable execution.</summary>
public static class ContextExtractors
{
    /// <summary>
    /// Reads <see cref="InvocationInfo.XRayTraceId"/>. The durable service keeps the X-Ray <c>Root</c> the same for
    /// every invocation of an execution, so its trace id anchors the whole execution.
    /// </summary>
    public static ExtractedContext? XRay(InvocationInfo info) => ParseXRayHeader(info.XRayTraceId);

    /// <summary>
    /// Reads <see cref="InvocationInfo.TraceParent"/>, the W3C <c>traceparent</c> the invoke carried on
    /// <c>clientContext.w3c</c>. Use it when the caller propagates W3C context instead of an X-Ray header. Like any
    /// extractor it must return the same context on every invocation of an execution, or the execution splits
    /// across traces.
    /// </summary>
    public static ExtractedContext? W3C(InvocationInfo info) => ParseTraceParent(info.TraceParent);

    /// <summary>
    /// Parses a W3C <c>traceparent</c>: <c>version-traceid-parentid-flags</c>, lowercase hex. A zero trace id or
    /// parent id, a <c>ff</c> version, or a malformed field returns null. Version <c>00</c> has exactly four
    /// fields; a later version may add more after them. The sampled flag is the low bit of the flags.
    /// </summary>
    internal static ExtractedContext? ParseTraceParent(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
            return null;

        var parts = traceParent.Trim().Split('-');
        if (parts.Length < 4)
            return null;

        var (version, traceId, parentId, flags) = (parts[0], parts[1], parts[2], parts[3]);
        if (version.Length != 2 || !DeterministicIds.IsLowerHex(version) || version == "ff" || (version == "00" && parts.Length != 4))
            return null;
        if (!DeterministicIds.IsValidTraceId(traceId) || !DeterministicIds.IsValidSpanId(parentId))
            return null;
        if (flags.Length != 2 || !DeterministicIds.IsLowerHex(flags))
            return null;

        return new ExtractedContext
        {
            TraceId = traceId,
            ParentSpanId = parentId,
            Sampling = (Convert.ToInt32(flags, 16) & 1) != 0 ? UpstreamSampling.Sampled : UpstreamSampling.NotSampled,
        };
    }

    /// <summary>
    /// Parses an <c>X-Amzn-Trace-Id</c> header. <c>Root</c>, <c>Parent</c> and <c>Sampled</c> are read independently;
    /// an all-zero or malformed <c>Root</c> or <c>Parent</c> is ignored. Returns null without a valid <c>Root</c>.
    /// </summary>
    internal static ExtractedContext? ParseXRayHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        string? root = null, parent = null, sampled = null;
        foreach (var part in header.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            switch (key)
            {
                case "Root": root = value; break;
                case "Parent": parent = value; break;
                case "Sampled": sampled = value; break;
            }
        }

        if (root == null)
            return null;

        var rootValue = root.StartsWith("1-", StringComparison.Ordinal) ? root[2..] : root;
        var traceId = rootValue.Replace("-", string.Empty).ToLowerInvariant();
        if (!DeterministicIds.IsValidTraceId(traceId))
            return null;

        var parentSpanId = parent?.ToLowerInvariant();
        return new ExtractedContext
        {
            TraceId = traceId,
            ParentSpanId = parentSpanId != null && DeterministicIds.IsValidSpanId(parentSpanId) ? parentSpanId : null,
            Sampling = sampled switch
            {
                "1" => UpstreamSampling.Sampled,
                "0" => UpstreamSampling.NotSampled,
                _ => UpstreamSampling.Undecided,
            },
        };
    }
}
