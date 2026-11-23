// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.DurableExecution.ConformanceTests.Otel;

internal static class Otel1Success
{
    public static readonly Scenario Scenario = new(nameof(Otel1Success), "success", async (_, context) =>
        await context.StepAsync((_, _) => Task.FromResult("success"), name: "otel-success"));
}

internal static class Otel2WaitResume
{
    public static readonly Scenario Scenario = new(nameof(Otel2WaitResume), "wait-resume", async (_, context) =>
    {
        await context.WaitAsync(TimeSpan.FromSeconds(1), name: "otel-wait");
        return await context.StepAsync((_, _) => Task.FromResult("resumed"), name: "otel-after-resume");
    });
}

internal static class Otel3Retry
{
    public static readonly Scenario Scenario = new(nameof(Otel3Retry), "retry", async (_, context) =>
        await context.StepAsync(
            (step, _) => step.AttemptNumber == 1
                ? throw new InvalidOperationException("Intentional first-attempt failure")
                : Task.FromResult("retried"),
            name: "otel-retry",
            config: new StepConfig
            {
                RetryStrategy = RetryStrategy.FromDelegate((_, attempt) =>
                    attempt >= 2 ? RetryDecision.DoNotRetry() : RetryDecision.RetryAfter(TimeSpan.FromSeconds(1))),
            }));
}

internal static class Otel4TerminalFailure
{
    public static readonly Scenario Scenario = new(nameof(Otel4TerminalFailure), "terminal-failure", async (_, context) =>
    {
        await context.StepAsync<string>(
            (_, _) => throw new InvalidOperationException("Intentional terminal failure"),
            name: "otel-terminal-failure",
            config: new StepConfig { RetryStrategy = RetryStrategy.None });
        return null;
    });
}
