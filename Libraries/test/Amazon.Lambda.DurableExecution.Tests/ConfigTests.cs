// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.Lambda.DurableExecution;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

public class ConfigTests
{
    [Fact]
    public void InvokeConfig_Defaults()
    {
        var config = new InvokeConfig();
        Assert.Null(config.TenantId);
    }

    [Fact]
    public void InvokeConfig_RoundTripsProperties()
    {
        var config = new InvokeConfig
        {
            TenantId = "tenant-42"
        };

        Assert.Equal("tenant-42", config.TenantId);
    }
}
