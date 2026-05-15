// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
#pragma warning disable AWSLAMBDA001 // ILambdaContext.Serializer is preview; this is the test that proves it works.
using System.IO;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Xunit;

namespace Amazon.Lambda.Tests
{
    public class TestLambdaContextSerializerTest
    {
        [Fact]
        public void Serializer_DefaultsToNull()
        {
            var context = new TestLambdaContext();

            Assert.Null(context.Serializer);
        }

        [Fact]
        public void Serializer_RoundTripsThroughTestContext()
        {
            var stub = new StubSerializer();
            var context = new TestLambdaContext { Serializer = stub };

            ILambdaContext asInterface = context;
            Assert.Same(stub, asInterface.Serializer);
        }

        private sealed class StubSerializer : ILambdaSerializer
        {
            public T Deserialize<T>(Stream requestStream) => default;
            public void Serialize<T>(T response, Stream responseStream) { }
        }
    }
}
