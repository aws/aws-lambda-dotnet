// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Implemented by a per-operation serializer that wraps an inner
/// <see cref="ILambdaSerializer"/> but may have been constructed without one, asking the
/// durable runtime to supply the globally-registered serializer as its inner. The runtime
/// calls <see cref="WithDefaultInner"/> when it resolves the effective serializer for an
/// operation (see the serializer resolution in <c>DurableContext</c>).
/// </summary>
internal interface IDefaultInnerSerializer
{
    /// <summary>
    /// Returns a serializer bound to <paramref name="inner"/> as its inner serializer. An
    /// implementation that already has an explicitly-provided inner returns itself unchanged.
    /// </summary>
    ILambdaSerializer WithDefaultInner(ILambdaSerializer inner);
}
