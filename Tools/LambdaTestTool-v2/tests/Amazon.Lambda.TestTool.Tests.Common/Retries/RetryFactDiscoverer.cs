// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Amazon.Lambda.TestTool.Tests.Common.Retries;

public class RetryFactDiscoverer : IXunitTestCaseDiscoverer
{
    readonly IMessageSink diagnosticMessageSink;

    public RetryFactDiscoverer(IMessageSink diagnosticMessageSink)
    {
        this.diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        // Read the MaxRetries property from the attribute.
        int maxRetries = factAttribute.GetNamedArgument<int>("MaxRetries");

        yield return new RetryTestCase(diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod,
            maxRetries);
    }
}
