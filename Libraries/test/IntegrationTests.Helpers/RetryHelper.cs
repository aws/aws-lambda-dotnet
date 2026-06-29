// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace IntegrationTests.Helpers
{
    /// <summary>
    /// Helpers for polling on eventually-consistent conditions in integration tests.
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Sends an HTTP request, retrying while API Gateway returns <see cref="HttpStatusCode.Forbidden"/>
        /// on what is expected to be an authorized request. A freshly deployed API Gateway stage can
        /// transiently 403 on the authorizer "allow" path until the Lambda authorizer wiring has fully
        /// propagated; once propagated the request returns a stable non-403 status. Because
        /// <see cref="HttpRequestMessage"/> cannot be resent, the caller supplies a factory that builds a
        /// fresh request for each attempt.
        /// </summary>
        /// <param name="httpClient">The client used to send the request.</param>
        /// <param name="requestFactory">Builds a fresh request for each attempt.</param>
        /// <param name="timeout">Maximum total time to keep retrying. Defaults to 2 minutes.</param>
        /// <param name="pollInterval">Delay between attempts. Defaults to 5 seconds.</param>
        /// <returns>The first non-403 response, or the last 403 response if the timeout elapses.</returns>
        public static async Task<HttpResponseMessage> SendWithRetryOnForbiddenAsync(
            HttpClient httpClient,
            Func<HttpRequestMessage> requestFactory,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
            if (requestFactory == null) throw new ArgumentNullException(nameof(requestFactory));

            var interval = pollInterval ?? TimeSpan.FromSeconds(5);
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(2));

            HttpResponseMessage response;
            while (true)
            {
                response = await httpClient.SendAsync(requestFactory());
                if (response.StatusCode != HttpStatusCode.Forbidden || DateTime.UtcNow >= deadline)
                {
                    return response;
                }

                response.Dispose();
                await Task.Delay(interval);
            }
        }

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
