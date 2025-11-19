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
using System;
using System.IO;
using System.Threading.Tasks;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// This class provides methods that help you wrap existing C# Lambda implementations with LambdaBootstrapHandler delegates.
    /// This makes serialization and deserialization simpler and allows you to use existing functions them with an instance of LambdaBootstrap.
    /// </summary>
    public class HandlerWrapper : IDisposable
    {
        private static readonly InvocationResponse EmptyInvocationResponse =
            new InvocationResponse(new MemoryStream(0), false);

        private readonly IOutputStreamFactory _outputStreamFactory;

        /// <summary>
        /// The handler that will be called for each event.
        /// </summary>
        public LambdaBootstrapHandler Handler { get; private set; }

        private HandlerWrapper(LambdaBootstrapHandler handler)
        {
            Handler = handler;

            if (Helpers.Utils.IsUsingMultiConcurrency(new SystemEnvironmentVariables()))
                _outputStreamFactory = new MultiConcurrencyOutputStreamFactory();
            else
                _outputStreamFactory = new OnDemandOutputStreamFactory();
        }

        private HandlerWrapper()
        {
            if (Helpers.Utils.IsUsingMultiConcurrency(new SystemEnvironmentVariables()))
                _outputStreamFactory = new MultiConcurrencyOutputStreamFactory();
            else
                _outputStreamFactory = new OnDemandOutputStreamFactory();
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given delegate on function invocation.
        /// </summary>
        /// <param name="invokeDelegate">Action called for each invocation of the Lambda function</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Action<Stream, ILambdaContext, MemoryStream> invokeDelegate)
        {

            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = invocation =>
            {
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                invokeDelegate(invocation.InputStream, invocation.LambdaContext, outputStream);
                outputStream.Position = 0;
                var response = new InvocationResponse(outputStream, false);
                return Task.FromResult(response);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler();
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Task> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                await handler();
                return EmptyInvocationResponse;
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream, Task> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                await handler(invocation.InputStream);
                return EmptyInvocationResponse;
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Func<TInput, Task> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                await handler(input);
                return EmptyInvocationResponse;
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<ILambdaContext, Task> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                await handler(invocation.LambdaContext);
                return EmptyInvocationResponse;
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream, ILambdaContext, Task> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                await handler(invocation.InputStream, invocation.LambdaContext);
                return EmptyInvocationResponse;
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Func<TInput, ILambdaContext, Task> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                await handler(input, invocation.LambdaContext);
                return EmptyInvocationResponse;
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;Stream&gt; Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Task<Stream>> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                return new InvocationResponse(await handler());
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;Stream&gt; Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream, Task<Stream>> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                return new InvocationResponse(await handler(invocation.InputStream));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;Stream&gt; Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Func<TInput, Task<Stream>> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return new InvocationResponse(await handler(input));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;Stream&gt; Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<ILambdaContext, Task<Stream>> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                return new InvocationResponse(await handler(invocation.LambdaContext));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;Stream&gt; Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream, ILambdaContext, Task<Stream>> handler)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                return new InvocationResponse(await handler(invocation.InputStream, invocation.LambdaContext));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;Stream&gt; Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Func<TInput, ILambdaContext, Task<Stream>> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper(async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return new InvocationResponse(await handler(input, invocation.LambdaContext));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;PocoOut&gt; Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = async (invocation) =>
            {
                TOutput output = await handler();
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return new InvocationResponse(outputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;PocoOut&gt; Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<Stream, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = async (invocation) =>
            {
                TOutput output = await handler(invocation.InputStream);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return new InvocationResponse(outputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;PocoOut&gt; Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput, TOutput>(Func<TInput, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = await handler(input);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return new InvocationResponse(outputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;PocoOut&gt; Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = async (invocation) =>
            {
                TOutput output = await handler(invocation.LambdaContext);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0; ;
                return new InvocationResponse(outputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;PocoOut&gt; Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<Stream, ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = async (invocation) =>
            {
                TOutput output = await handler(invocation.InputStream, invocation.LambdaContext);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return new InvocationResponse(outputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&lt;PocoOut&gt; Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput, TOutput>(Func<TInput, ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = await handler(input, invocation.LambdaContext);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return new InvocationResponse(outputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler()
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Action handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                handler();
                return Task.FromResult(EmptyInvocationResponse);
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(Stream)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Action<Stream> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                handler(invocation.InputStream);
                return Task.FromResult(EmptyInvocationResponse);
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Action<TInput> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper((invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                handler(input);
                return Task.FromResult(EmptyInvocationResponse);
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Action<ILambdaContext> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                handler(invocation.LambdaContext);
                return Task.FromResult(EmptyInvocationResponse);
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Action<Stream, ILambdaContext> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                handler(invocation.InputStream, invocation.LambdaContext);
                return Task.FromResult(EmptyInvocationResponse);
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Action<TInput, ILambdaContext> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper((invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                handler(input, invocation.LambdaContext);
                return Task.FromResult(EmptyInvocationResponse);
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                return Task.FromResult(new InvocationResponse(handler()));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream, Stream> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                return Task.FromResult(new InvocationResponse(handler(invocation.InputStream)));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Func<TInput, Stream> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper((invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return Task.FromResult(new InvocationResponse(handler(input)));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<ILambdaContext, Stream> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                return Task.FromResult(new InvocationResponse(handler(invocation.LambdaContext)));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper(Func<Stream, ILambdaContext, Stream> handler)
        {
            return new HandlerWrapper((invocation) =>
            {
                return Task.FromResult(new InvocationResponse(handler(invocation.InputStream, invocation.LambdaContext)));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput>(Func<TInput, ILambdaContext, Stream> handler, ILambdaSerializer serializer)
        {
            return new HandlerWrapper((invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return Task.FromResult(new InvocationResponse(handler(input, invocation.LambdaContext)));
            });
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<TOutput> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = (invocation) =>
            {
                TOutput output = handler();
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(outputStream, false));
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<Stream, TOutput> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = (invocation) =>
            {
                TOutput output = handler(invocation.InputStream);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(outputStream, false));
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput, TOutput>(Func<TInput, TOutput> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = handler(input);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(outputStream, false));
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = (invocation) =>
            {
                TOutput output = handler(invocation.LambdaContext);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0; ;
                return Task.FromResult(new InvocationResponse(outputStream, false));
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TOutput>(Func<Stream, ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = (invocation) =>
            {
                TOutput output = handler(invocation.InputStream, invocation.LambdaContext);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(outputStream, false));
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A HandlerWrapper</returns>
        public static HandlerWrapper GetHandlerWrapper<TInput, TOutput>(Func<TInput, ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            var handlerWrapper = new HandlerWrapper();
            handlerWrapper.Handler = (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = handler(input, invocation.LambdaContext);
                var outputStream = handlerWrapper._outputStreamFactory.CreateOutputStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(outputStream, false));
            };
            return handlerWrapper;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Dispose the HandlerWrapper
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _outputStreamFactory.Dispose();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Dispose the HandlerWrapper
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        interface IOutputStreamFactory : IDisposable
        {
            MemoryStream CreateOutputStream();
        }

        /// <summary>
        /// In on demand mode there is never a more then one invocation happening at a time within the process
        /// so the same memory stream can be reused.
        /// </summary>
        class OnDemandOutputStreamFactory : IOutputStreamFactory
        {
            private readonly MemoryStream OutputStream = new MemoryStream();
            private bool _disposedValue;

            public MemoryStream CreateOutputStream()
            {
                OutputStream.SetLength(0);
                return OutputStream;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                        OutputStream.Dispose();
                    }

                    _disposedValue = true;
                }
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// In multi concurrency mode multiple invocations can happen at the same time within the process
        /// so we need to make sure each invocation gets its own output stream.
        /// </summary>
        class MultiConcurrencyOutputStreamFactory : IOutputStreamFactory
        {
            public MemoryStream CreateOutputStream()
            {
                return new MemoryStream();
            }

            public void Dispose()
            {
                // Technically we are creating MemoryStreams that have a Dispose method but that is inherited from the base
                // class. A MemoryStream is fully managed and doesn't have anything to dispose so it is okay to not worry
                // about disposing any of the MemoryStreams created from the CreateOutputStream call.
            }
        }
    }
}
