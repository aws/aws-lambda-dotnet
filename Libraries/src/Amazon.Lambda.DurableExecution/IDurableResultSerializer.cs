// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Optional capability interface a per-operation serializer may implement to receive a
/// <see cref="DurableSerializationContext"/> when a durable operation result is
/// (de)serialized. When the serializer configured on an operation (for example
/// <c>StepConfig.Serializer</c>) implements this interface, the durable runtime invokes
/// these context-aware overloads; otherwise it falls back to the plain
/// <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> methods.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> conveys only the value and a stream —
/// it has no way to tell the serializer which operation or execution a value belongs to.
/// Serializers that offload results to external storage (for example
/// <c>FileSystemSerializer</c>) need that identity to build a stable, unique
/// location and to avoid different operations clobbering one another. Serializers that
/// do not need it simply implement <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> and
/// are used unchanged.
/// </para>
/// <para>
/// A type typically implements <b>both</b> <see cref="Amazon.Lambda.Core.ILambdaSerializer"/>
/// and this interface. The durable runtime prefers this interface when present.
/// </para>
/// </remarks>
public interface IDurableResultSerializer
{
    /// <summary>
    /// Serializes <paramref name="value"/> to <paramref name="stream"/>, using
    /// <paramref name="context"/> to identify the operation/execution.
    /// </summary>
    void Serialize<T>(T value, Stream stream, DurableSerializationContext context);

    /// <summary>
    /// Deserializes a value of type <typeparamref name="T"/> from <paramref name="stream"/>,
    /// using <paramref name="context"/> to identify the operation/execution.
    /// </summary>
    T Deserialize<T>(Stream stream, DurableSerializationContext context);
}
