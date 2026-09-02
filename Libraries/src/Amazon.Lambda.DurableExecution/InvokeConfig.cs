// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for chained invoke operations.
/// </summary>
/// <remarks>
/// Use with <see cref="IDurableContext.InvokeAsync{TPayload, TResult}(string, TPayload, string?, InvokeConfig?, System.Threading.CancellationToken)"/>
/// to configure a single chained invocation. Payload/result serialization is
/// performed by the <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> registered on
/// <see cref="Amazon.Lambda.Core.ILambdaContext.Serializer"/> (typically configured via
/// <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>), unless overridden for this
/// operation via <see cref="Serializer"/>.
/// </remarks>
public sealed class InvokeConfig
{
    /// <summary>
    /// Optional tenant identifier propagated to the chained invocation via
    /// <c>ChainedInvokeOptions.TenantId</c>. Used to route the invocation to a
    /// tenant-isolated function. Matches the <c>tenantId</c> field on the
    /// Python, JavaScript, and Java SDKs.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Optional serializer for this invoke's payload and result. When <c>null</c>
    /// (default), the globally-registered <see cref="ILambdaSerializer"/> on
    /// <see cref="ILambdaContext.Serializer"/> is used.
    /// </summary>
    /// <remarks>
    /// The chained (callee) function serializes and deserializes with its own registered
    /// serializer, so an override here must produce a form the callee can read (and read a
    /// form the callee produces). Prefer overriding only when both sides agree on the format.
    /// </remarks>
    public ILambdaSerializer? Serializer { get; set; }
}
