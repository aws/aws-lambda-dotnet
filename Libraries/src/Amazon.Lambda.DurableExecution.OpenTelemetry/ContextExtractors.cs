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
    /// Parses an <c>X-Amzn-Trace-Id</c> header. <c>Root</c>, <c>Parent</c> and <c>Sampled</c> are read independently;
    /// an all-zero or malformed <c>Root</c> or <c>Parent</c> is ignored. Returns null without a valid <c>Root</c>.
    /// </summary>
    public static ExtractedContext? ParseXRayHeader(string? header)
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
