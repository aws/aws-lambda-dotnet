// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;

namespace IntegrationTests.Helpers
{
    /// <summary>
    /// Helpers for polling on eventually-consistent conditions in integration tests.
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Polls <paramref name="condition"/> until it returns <c>true</c> or <paramref name="timeout"/> elapses.
        /// Useful for gating tests on resources that report ready (e.g. CloudFormation CREATE_COMPLETE)
        /// before they are fully propagated and serving traffic (e.g. API Gateway stages/authorizers).
        /// </summary>
        /// <param name="condition">The condition to evaluate. Returning <c>true</c> ends the wait.</param>
        /// <param name="timeout">Maximum total time to keep polling.</param>
        /// <param name="pollInterval">Delay between attempts. Defaults to 5 seconds.</param>
        /// <returns><c>true</c> if the condition was met before the timeout; otherwise <c>false</c>.</returns>
        public static async Task<bool> WaitForConditionAsync(
            Func<Task<bool>> condition,
            TimeSpan timeout,
            TimeSpan? pollInterval = null)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));

            var interval = pollInterval ?? TimeSpan.FromSeconds(5);
            var deadline = DateTime.UtcNow + timeout;

            while (true)
            {
                try
                {
                    if (await condition())
                    {
                        return true;
                    }
                }
                catch
                {
                    // Swallow transient errors (e.g. connection resets while the endpoint warms up)
                    // and keep polling until the deadline.
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return false;
                }

                await Task.Delay(interval);
            }
        }
    }
}
