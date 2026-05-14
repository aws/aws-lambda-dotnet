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
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    /// <summary>
    /// Verifies that the serializer registered with a <see cref="HandlerWrapper"/> /
    /// <see cref="LambdaBootstrapBuilder"/> is exposed on the per-invocation
    /// <see cref="ILambdaContext.Serializer"/> via <see cref="LambdaContext"/>.
    /// </summary>
    public class LambdaContextSerializerTests
    {
        private static readonly JsonSerializer SharedSerializer = new JsonSerializer();

        private readonly LambdaEnvironment _lambdaEnvironment;
        private readonly RuntimeApiHeaders _runtimeApiHeaders;

        public LambdaContextSerializerTests()
        {
            var environmentVariables = new TestEnvironmentVariables();
            _lambdaEnvironment = new LambdaEnvironment(environmentVariables);

            var headers = new Dictionary<string, IEnumerable<string>>
            {
                [RuntimeApiHeaders.HeaderAwsRequestId] = new[] { "request-id" },
                [RuntimeApiHeaders.HeaderInvokedFunctionArn] = new[] { "invoked-function-arn" }
            };
            _runtimeApiHeaders = new RuntimeApiHeaders(headers);
        }

        [Fact]
        public void LambdaContext_Serializer_DefaultsToNull_WhenNotSupplied()
        {
            var context = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.Null(context.Serializer);
        }

        [Fact]
        public void LambdaContext_Serializer_ReturnsConstructorArgument()
        {
            var context = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()), SharedSerializer);

            Assert.Same(SharedSerializer, context.Serializer);
        }

        [Fact]
        public void HandlerWrapper_PocoInOut_ExposesSerializer()
        {
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(
                input => Task.FromResult(new PocoOutput()),
                SharedSerializer);

            Assert.Same(SharedSerializer, handlerWrapper.Serializer);
        }

        [Fact]
        public void HandlerWrapper_RawStreamOverloads_HaveNullSerializer()
        {
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper(
                (Func<Stream, Task<Stream>>)((input) => Task.FromResult<Stream>(new MemoryStream())));

            Assert.Null(handlerWrapper.Serializer);
        }

        [Fact]
        public void HandlerWrapper_AllSerializerOverloads_PropagateSerializer()
        {
            // One sample per overload family (Func/Action × Task/non-Task × in/out × ILambdaContext)
            // is enough — they share the same field-assignment line. This guards against future
            // overloads being added without setting Serializer.

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoInput>((input) => Task.CompletedTask, SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoInput>((input, ctx) => Task.CompletedTask, SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoInput>(
                (Func<PocoInput, Task<Stream>>)((input) => Task.FromResult<Stream>(new MemoryStream())), SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoOutput>(() => Task.FromResult(new PocoOutput()), SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(
                (input, ctx) => Task.FromResult(new PocoOutput()), SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoInput>((Action<PocoInput>)(input => { }), SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);

            using (var w = HandlerWrapper.GetHandlerWrapper<PocoOutput>((Func<PocoOutput>)(() => new PocoOutput()), SharedSerializer))
                Assert.Same(SharedSerializer, w.Serializer);
        }

        [Fact]
        public async Task HandlerWrapper_HandlerSeesSerializerOnContext()
        {
            // End-to-end: invoke a handler through the wrapper machinery and confirm
            // the user delegate sees context.Serializer == registered serializer.
            //
            // This validates the LambdaBootstrap → RuntimeApiClient → LambdaContext path
            // by directly wiring a LambdaContext that carries the serializer (the same
            // shape the bootstrap produces in production).
            ILambdaSerializer observed = null;
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(
                (input, ctx) =>
                {
                    observed = ctx.Serializer;
                    return Task.FromResult(new PocoOutput());
                },
                SharedSerializer);

            var inputBytes = SerializeToBytes(new PocoInput { InputInt = 1, InputString = "x" });
            var invocation = new InvocationRequest
            {
                InputStream = new MemoryStream(inputBytes),
                LambdaContext = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment, new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()), handlerWrapper.Serializer)
            };

            await handlerWrapper.Handler(invocation);

            Assert.Same(SharedSerializer, observed);
        }

        [Fact]
        public void LambdaBootstrap_ConstructedWithHandlerWrapper_PlumbsSerializerToRuntimeApiClient()
        {
            // The bootstrap copies HandlerWrapper.Serializer onto its internal
            // RuntimeApiClient.Serializer; that field is what the per-invocation
            // LambdaContext gets.
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(
                (input, ctx) => Task.FromResult(new PocoOutput()),
                SharedSerializer);

            using var bootstrap = new LambdaBootstrap(handlerWrapper);

            var runtimeApiClient = Assert.IsType<RuntimeApiClient>(bootstrap.Client);
            Assert.Same(SharedSerializer, runtimeApiClient.Serializer);
        }

        [Fact]
        public void LambdaBootstrap_ConstructedWithRawHandler_HasNullSerializerOnRuntimeApiClient()
        {
            // Users who construct LambdaBootstrap directly with a LambdaBootstrapHandler
            // bypass HandlerWrapper. There's no serializer to capture, so the field stays null.
            using var bootstrap = new LambdaBootstrap(_ => Task.FromResult(new InvocationResponse(new MemoryStream(), false)));

            var runtimeApiClient = Assert.IsType<RuntimeApiClient>(bootstrap.Client);
            Assert.Null(runtimeApiClient.Serializer);
        }

        private static byte[] SerializeToBytes<T>(T value)
        {
            using var ms = new MemoryStream();
            SharedSerializer.Serialize(value, ms);
            return ms.ToArray();
        }
    }
}
