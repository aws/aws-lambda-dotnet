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

using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Internal context class used by ResponseStreamFactory to track per-invocation streaming state.
    /// </summary>
    internal class ResponseStreamContext
    {
        /// <summary>
        /// The AWS request ID for the current invocation.
        /// </summary>
        public string AwsRequestId { get; set; }

        /// <summary>
        /// Maximum allowed response size in bytes (20 MiB).
        /// </summary>
        public long MaxResponseSize { get; set; }

        /// <summary>
        /// Whether CreateStream() has been called for this invocation.
        /// </summary>
        public bool StreamCreated { get; set; }

        /// <summary>
        /// The ResponseStream instance if created.
        /// </summary>
        public ResponseStream Stream { get; set; }

        /// <summary>
        /// The RuntimeApiClient used to start the streaming HTTP POST.
        /// </summary>
        public RuntimeApiClient RuntimeApiClient { get; set; }

        /// <summary>
        /// Cancellation token for the current invocation.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// The Task representing the in-flight HTTP POST to the Runtime API.
        /// Started when CreateStream() is called, completes when the stream is finalized.
        /// </summary>
        public Task SendTask { get; set; }
    }
}
