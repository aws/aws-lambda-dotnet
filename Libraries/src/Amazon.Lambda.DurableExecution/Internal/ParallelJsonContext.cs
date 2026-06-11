// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// AOT-friendly <see cref="JsonSerializerContext"/> for the internal
/// <see cref="ParallelSummary"/> payload stored on a parallel parent's CONTEXT
/// checkpoint. Only this internal type — never user T — flows through here, so
/// the source-generated metadata is sufficient.
/// </summary>
[JsonSerializable(typeof(ParallelSummary))]
[JsonSerializable(typeof(ParallelBranchSummary))]
internal sealed partial class ParallelJsonContext : JsonSerializerContext
{
}
