// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.IntegrationTests.Helpers
{
    public class RetryHelper
    {
        public static async Task<T> RetryOperation<T>(Func<Task<T>> operation, int maxRetries = 3, int delayMilliseconds = 20000)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    Console.WriteLine($"Attempt {i + 1} failed: {ex.Message}. Retrying in {delayMilliseconds}ms...");
                    await Task.Delay(delayMilliseconds);
                }
            }

            // If we've exhausted all retries, run one last time and let any exception propagate
            return await operation();
        }
    }
}
