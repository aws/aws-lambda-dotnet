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

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers
{
    /// <summary>
    /// A no-op implementation of IInternalRuntimeApiClient for unit tests
    /// that need to construct a RuntimeApiClient without real HTTP calls.
    /// </summary>
    internal class NoOpInternalRuntimeApiClient : IInternalRuntimeApiClient
    {
        private static readonly SwaggerResponse<StatusResponse> EmptyStatusResponse =
            new SwaggerResponse<StatusResponse>(200, new Dictionary<string, IEnumerable<string>>(), new StatusResponse());

        public Task<SwaggerResponse<StatusResponse>> ErrorAsync(
            string lambda_Runtime_Function_Error_Type, string errorJson, CancellationToken cancellationToken)
            => Task.FromResult(EmptyStatusResponse);

        public Task<SwaggerResponse<Stream>> NextAsync(CancellationToken cancellationToken)
            => Task.FromResult(new SwaggerResponse<Stream>(200, new Dictionary<string, IEnumerable<string>>(), Stream.Null));

        public Task<SwaggerResponse<StatusResponse>> ResponseAsync(string awsRequestId, Stream outputStream)
            => Task.FromResult(EmptyStatusResponse);

        public Task<SwaggerResponse<StatusResponse>> ResponseAsync(
            string awsRequestId, Stream outputStream, CancellationToken cancellationToken)
            => Task.FromResult(EmptyStatusResponse);

        public Task<SwaggerResponse<StatusResponse>> ErrorWithXRayCauseAsync(
            string awsRequestId, string lambda_Runtime_Function_Error_Type,
            string errorJson, string xrayCause, CancellationToken cancellationToken)
            => Task.FromResult(EmptyStatusResponse);

#if NET8_0_OR_GREATER
        public Task<SwaggerResponse<Stream>> RestoreNextAsync(CancellationToken cancellationToken)
            => Task.FromResult(new SwaggerResponse<Stream>(200, new Dictionary<string, IEnumerable<string>>(), Stream.Null));

        public Task<SwaggerResponse<StatusResponse>> RestoreErrorAsync(
            string lambda_Runtime_Function_Error_Type, string errorJson, CancellationToken cancellationToken)
            => Task.FromResult(EmptyStatusResponse);
#endif
    }
}
