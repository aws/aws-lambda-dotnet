/*
 * Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License").
 * You may not use this file except in compliance with the License.
 * A copy of the License is located at
 *
 *  http://aws.amazon.com/apache2.0
 *
 * or in the "license" file accompanying this file. This file is distributed
 * on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
 * express or implied. See the License for the specific language governing
 * permissions and limitations under the License.
 */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Factory for creating streaming responses in AWS Lambda functions.
    /// Call CreateStream() within your handler to opt into response streaming for that invocation.
    /// </summary>
    public static class ResponseStreamFactory
    {
        // For on-demand mode (single invocation at a time)
        private static ResponseStreamContext _onDemandContext;

        // For multi-concurrency mode (multiple concurrent invocations)
        private static readonly AsyncLocal<ResponseStreamContext> _asyncLocalContext = new AsyncLocal<ResponseStreamContext>();

        /// <summary>
        /// Creates a streaming response for the current invocation.
        /// Can only be called once per invocation.
        /// </summary>
        /// <returns>An IResponseStream for writing response data.</returns>
        /// <exception cref="InvalidOperationException">Thrown if called outside an invocation context.</exception>
        /// <exception cref="InvalidOperationException">Thrown if called more than once per invocation.</exception>
        public static IResponseStream CreateStream()
        {
            var context = GetCurrentContext();

            if (context == null)
            {
                throw new InvalidOperationException(
                    "ResponseStreamFactory.CreateStream() can only be called within a Lambda handler invocation.");
            }

            if (context.StreamCreated)
            {
                throw new InvalidOperationException(
                    "ResponseStreamFactory.CreateStream() can only be called once per invocation.");
            }

            var stream = new ResponseStream(context.MaxResponseSize);
            context.Stream = stream;
            context.StreamCreated = true;

            // Start the HTTP POST to the Runtime API.
            // This runs concurrently — SerializeToStreamAsync will block
            // until the handler finishes writing or reports an error.
            context.SendTask = context.RuntimeApiClient.StartStreamingResponseAsync(
                context.AwsRequestId, stream, context.CancellationToken);

            return stream;
        }

        // Internal methods for LambdaBootstrap to manage state

        internal static void InitializeInvocation(
            string awsRequestId, long maxResponseSize, bool isMultiConcurrency,
            RuntimeApiClient runtimeApiClient, CancellationToken cancellationToken)
        {
            var context = new ResponseStreamContext
            {
                AwsRequestId = awsRequestId,
                MaxResponseSize = maxResponseSize,
                StreamCreated = false,
                Stream = null,
                RuntimeApiClient = runtimeApiClient,
                CancellationToken = cancellationToken
            };

            if (isMultiConcurrency)
            {
                _asyncLocalContext.Value = context;
            }
            else
            {
                _onDemandContext = context;
            }
        }

        internal static ResponseStream GetStreamIfCreated(bool isMultiConcurrency)
        {
            var context = isMultiConcurrency ? _asyncLocalContext.Value : _onDemandContext;
            return context?.Stream;
        }

        /// <summary>
        /// Returns the Task for the in-flight HTTP send, or null if streaming wasn't started.
        /// LambdaBootstrap awaits this after the handler returns to ensure the HTTP request completes.
        /// </summary>
        internal static Task GetSendTask(bool isMultiConcurrency)
        {
            var context = isMultiConcurrency ? _asyncLocalContext.Value : _onDemandContext;
            return context?.SendTask;
        }

        internal static void CleanupInvocation(bool isMultiConcurrency)
        {
            if (isMultiConcurrency)
            {
                _asyncLocalContext.Value = null;
            }
            else
            {
                _onDemandContext = null;
            }
        }

        private static ResponseStreamContext GetCurrentContext()
        {
            // Check multi-concurrency first (AsyncLocal), then on-demand
            return _asyncLocalContext.Value ?? _onDemandContext;
        }
    }
}
