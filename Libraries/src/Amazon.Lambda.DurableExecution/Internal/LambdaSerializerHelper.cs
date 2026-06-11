// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

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
}
