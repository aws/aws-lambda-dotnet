// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution.Internal;

internal static class LambdaSerializerHelper
{
    private const string MissingSerializerMessage =
        "No ILambdaSerializer is registered on ILambdaContext.Serializer. " +
        "In the class library programming model, register one with " +
        "[assembly: LambdaSerializer(typeof(...))]. In an executable / custom " +
        "runtime, pass it to LambdaBootstrapBuilder.Create(handler, serializer). " +
        "In tests, set TestLambdaContext.Serializer.";

    public static ILambdaSerializer GetRequired(ILambdaContext lambdaContext) =>
        lambdaContext.Serializer ?? throw new InvalidOperationException(MissingSerializerMessage);

    /// <summary>
    /// Serializes a durable operation result. If <paramref name="serializer"/> implements
    /// <see cref="IDurableResultSerializer"/>, the context-aware overload is used so the
    /// serializer can key external storage by operation/execution; otherwise the plain
    /// <see cref="ILambdaSerializer"/> path is used (behavior identical to before).
    /// </summary>
    public static void Serialize<T>(
        ILambdaSerializer serializer, T value, Stream stream, in DurableSerializationContext context)
    {
        if (serializer is IDurableResultSerializer durable)
            durable.Serialize(value, stream, context);
        else
            serializer.Serialize(value, stream);
    }

    /// <summary>
    /// Deserializes a durable operation result. Mirrors <see cref="Serialize{T}"/>:
    /// uses the <see cref="IDurableResultSerializer"/> overload when available, else the
    /// plain <see cref="ILambdaSerializer"/> path.
    /// </summary>
    public static T Deserialize<T>(
        ILambdaSerializer serializer, Stream stream, in DurableSerializationContext context)
    {
        if (serializer is IDurableResultSerializer durable)
            return durable.Deserialize<T>(stream, context);
        return serializer.Deserialize<T>(stream);
    }
}
