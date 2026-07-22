// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace Amazon.Lambda.TestTool.UnitTests;

/// <summary>
/// xUnit collection that serializes the durable-execution web-host tests. Each such test starts a
/// full Test Tool web host (Kestrel + Blazor) and a fake function polling the Runtime API every
/// 100ms; running many in parallel starves those poll loops under CPU pressure and causes
/// spurious timeouts. Tests in the same collection do not run concurrently with one another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DurableTestCollection
{
    public const string Name = "DurableExecution web-host tests";
}
