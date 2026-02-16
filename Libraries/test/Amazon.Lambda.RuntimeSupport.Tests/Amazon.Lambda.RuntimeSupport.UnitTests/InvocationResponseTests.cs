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

using System.IO;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class InvocationResponseTests
    {
        private const long MaxResponseSize = 20 * 1024 * 1024;

        /// <summary>
        /// Property 17: InvocationResponse Streaming Flag - Existing constructors set IsStreaming to false.
        /// Validates: Requirements 7.3, 8.1
        /// </summary>
        [Fact]
        public void Constructor_WithStream_IsStreamingIsFalse()
        {
            var response = new InvocationResponse(new MemoryStream());

            Assert.False(response.IsStreaming);
            Assert.Null(response.ResponseStream);
        }

        [Fact]
        public void Constructor_WithStreamAndDispose_IsStreamingIsFalse()
        {
            var response = new InvocationResponse(new MemoryStream(), false);

            Assert.False(response.IsStreaming);
            Assert.Null(response.ResponseStream);
        }

        /// <summary>
        /// Property 17: InvocationResponse Streaming Flag - CreateStreamingResponse sets IsStreaming to true.
        /// Validates: Requirements 7.3, 8.1
        /// </summary>
        [Fact]
        public void CreateStreamingResponse_SetsIsStreamingTrue()
        {
            var stream = new ResponseStream(MaxResponseSize);

            var response = InvocationResponse.CreateStreamingResponse(stream);

            Assert.True(response.IsStreaming);
        }

        [Fact]
        public void CreateStreamingResponse_SetsResponseStream()
        {
            var stream = new ResponseStream(MaxResponseSize);

            var response = InvocationResponse.CreateStreamingResponse(stream);

            Assert.Same(stream, response.ResponseStream);
        }

        [Fact]
        public void CreateStreamingResponse_DoesNotDisposeOutputStream()
        {
            var stream = new ResponseStream(MaxResponseSize);

            var response = InvocationResponse.CreateStreamingResponse(stream);

            Assert.False(response.DisposeOutputStream);
        }
    }
}
