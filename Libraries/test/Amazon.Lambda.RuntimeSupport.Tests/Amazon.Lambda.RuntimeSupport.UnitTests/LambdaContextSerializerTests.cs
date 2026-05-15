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
#pragma warning disable AWSLAMBDA001 // ILambdaContext.Serializer is preview; this is the test that proves it works.
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport.Helpers;
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

        private readonly TestEnvironmentVariables _environmentVariables;
        private readonly LambdaEnvironment _lambdaEnvironment;
        private readonly RuntimeApiHeaders _runtimeApiHeaders;
        private readonly Dictionary<string, IEnumerable<string>> _headers;

        public LambdaContextSerializerTests()
        {
            _environmentVariables = new TestEnvironmentVariables();
            _lambdaEnvironment = new LambdaEnvironment(_environmentVariables);

            _headers = new Dictionary<string, IEnumerable<string>>
            {
                [RuntimeApiHeaders.HeaderAwsRequestId] = new[] { "request-id" },
                [RuntimeApiHeaders.HeaderInvokedFunctionArn] = new[] { "invoked-function-arn" }
            };
            _runtimeApiHeaders = new RuntimeApiHeaders(_headers);
        }

        [Fact]
        public void LambdaContext_Serializer_DefaultsToNull()
        {
            var context = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment, new LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            Assert.Null(context.Serializer);
        }

        [Fact]
        public void LambdaContextSerializerIsolated_TrySetSerializer_PopulatesProperty()
        {
            // The Isolated shim is the one place RuntimeSupport touches
            // ILambdaContext.Serializer; everything else routes through this method
            // so a TypeLoadException from a stale user-side Amazon.Lambda.Core can be
            // caught at the call site.
            var context = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment, new LogLevelLoggerWriter(new SystemEnvironmentVariables()));

            LambdaContextSerializerIsolated.TrySetSerializer(context, SharedSerializer);

            Assert.Same(SharedSerializer, context.Serializer);
        }

        [Fact]
        public void LambdaContextSerializerIsolated_TrySetSerializer_NullContext_DoesNotThrow()
        {
            // The shim is called on every invocation; a defensive null-check keeps it
            // total even if a future refactor passes a non-LambdaContext implementation.
            LambdaContextSerializerIsolated.TrySetSerializer(null, SharedSerializer);
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
        public async Task LambdaBootstrap_InvokeOnce_SetsSerializerOnContext()
        {
            // End-to-end: a HandlerWrapper-backed bootstrap invokes once against a test
            // RuntimeApiClient. The user's handler reads context.Serializer mid-invocation
            // and must see the registered instance — proving SetSerializerOnContext fires
            // through the Isolated shim during the invoke loop.
            ILambdaSerializer observed = null;
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(
                (input, ctx) =>
                {
                    observed = ctx.Serializer;
                    return Task.FromResult(new PocoOutput());
                },
                SharedSerializer);

            using var bootstrap = new LambdaBootstrap(handlerWrapper);
            var testClient = new TestRuntimeApiClient(_environmentVariables, _headers)
            {
                FunctionInput = SerializeToBytes(new PocoInput { InputInt = 1, InputString = "x" })
            };
            bootstrap.Client = testClient;

            await bootstrap.InvokeOnceAsync();

            Assert.Same(SharedSerializer, observed);
        }

        [Fact]
        public async Task LambdaBootstrap_InvokeOnce_RawStreamHandler_LeavesSerializerNull()
        {
            // Raw-stream handlers don't register a serializer — context.Serializer must
            // stay null even after the invoke loop runs.
            ILambdaSerializer observed = SharedSerializer; // start non-null to prove it's set to null
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper(
                (Func<Stream, ILambdaContext, Task>)((input, ctx) =>
                {
                    observed = ctx.Serializer;
                    return Task.CompletedTask;
                }));

            using var bootstrap = new LambdaBootstrap(handlerWrapper);
            var testClient = new TestRuntimeApiClient(_environmentVariables, _headers);
            bootstrap.Client = testClient;

            await bootstrap.InvokeOnceAsync();

            Assert.Null(observed);
        }

        private static byte[] SerializeToBytes<T>(T value)
        {
            using var ms = new MemoryStream();
            SharedSerializer.Serialize(value, ms);
            return ms.ToArray();
        }
    }
}
