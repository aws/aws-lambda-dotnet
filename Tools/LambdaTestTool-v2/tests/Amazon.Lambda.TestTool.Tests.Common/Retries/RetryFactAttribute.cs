// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit;
using Xunit.Sdk;

namespace Amazon.Lambda.TestTool.Tests.Common.Retries;

[XunitTestCaseDiscoverer("Amazon.Lambda.TestTool.Tests.Common.Retries.RetryFactDiscoverer", "Amazon.Lambda.TestTool.Tests.Common")]
public class RetryFactAttribute : FactAttribute
{
    /// <summary>
    /// Number of additional attempts (not counting the initial try)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    public RetryFactAttribute() { }
}
