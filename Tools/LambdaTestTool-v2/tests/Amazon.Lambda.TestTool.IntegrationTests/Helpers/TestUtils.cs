// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Amazon.Lambda.TestTool.IntegrationTests.Helpers
{
    /// <summary>
    /// Utility class for common test functions.
    /// </summary>
    public static class TestUtils
    {
        /// <summary>
        /// Generates a unique route path for API Gateway testing.
        /// </summary>
        /// <returns>A unique route path starting with '/test-' followed by a GUID.</returns>
        public static string GetUniqueRoutePath() => $"/test-{Guid.NewGuid():N}";
    }
} 