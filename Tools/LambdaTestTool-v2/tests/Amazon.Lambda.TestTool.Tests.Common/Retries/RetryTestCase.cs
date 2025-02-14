// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Amazon.Lambda.TestTool.Tests.Common.Retries;

public class RetryTestCase : XunitTestCase
{
    int _maxRetries;

    public RetryTestCase(IMessageSink diagnosticMessageSink,
                         TestMethodDisplay defaultMethodDisplay,
                         TestMethodDisplayOptions defaultMethodDisplayOptions,
                         ITestMethod testMethod,
                         int maxRetries)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod)
    {
        _maxRetries = maxRetries;
    }

    // Parameterless constructor needed for de-serialization
    [Obsolete("Called by the de-serializer", true)]
    public RetryTestCase() { }

    public override async Task<RunSummary?> RunAsync(IMessageSink diagnosticMessageSink,
                                                    IMessageBus messageBus,
                                                    object[] constructorArguments,
                                                    ExceptionAggregator aggregator,
                                                    CancellationTokenSource cancellationTokenSource)
    {
        RunSummary? finalSummary = null;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            // Create a fresh aggregator for each attempt
            var attemptAggregator = new ExceptionAggregator();

            // Run the test (each attempt returns its own summary)
            var currentSummary = await base.RunAsync(diagnosticMessageSink,
                messageBus,
                constructorArguments,
                attemptAggregator,
                cancellationTokenSource);

            if (currentSummary.Failed == 0)
            {
                // If the test passed, log a message and return the current summary
                if (attempt > 0)
                {
                    diagnosticMessageSink.OnMessage(new DiagnosticMessage($"Test passed on attempt {attempt + 1}"));
                }
                return currentSummary;
            }

            diagnosticMessageSink.OnMessage(new DiagnosticMessage($"Test failed on attempt {attempt + 1}, retrying..."));
            finalSummary = currentSummary;
        }

        // If none of the attempts passed, return the summary of the final attempt.
        return finalSummary;
    }

    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);
        data.AddValue("MaxRetries", _maxRetries);
    }

    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);
        _maxRetries = data.GetValue<int>("MaxRetries");
    }
}
