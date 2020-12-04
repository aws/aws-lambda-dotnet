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
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        private MemoryStream OutputStream = new MemoryStream();

        public LambdaBootstrapHandler Handler { get; private set; }

        private HandlerWrapper(LambdaBootstrapHandler handler)
        {
            Handler = handler;
        }

        private HandlerWrapper() { }

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
                handlerWrapper.OutputStream.SetLength(0);
                invokeDelegate(invocation.InputStream, invocation.LambdaContext, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                var response = new InvocationResponse(handlerWrapper.OutputStream, false);
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
        /// Example handler signature: Task&ltStream&gt Handler()
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
        /// Example handler signature: Task&ltStream&gt Handler(Stream)
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
        /// Example handler signature: Task&ltStream&gt Handler(PocoIn)
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
        /// Example handler signature: Task&ltStream&gt Handler(ILambdaContext)
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
        /// Example handler signature: Task&ltStream&gt Handler(Stream, ILambdaContext)
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
        /// Example handler signature: Task&ltStream&gt Handler(PocoIn, ILambdaContext)
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
        /// Example handler signature: Task&ltPocoOut&gt Handler()
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return new InvocationResponse(handlerWrapper.OutputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(Stream)
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return new InvocationResponse(handlerWrapper.OutputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(PocoIn)
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return new InvocationResponse(handlerWrapper.OutputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(ILambdaContext)
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0; ;
                return new InvocationResponse(handlerWrapper.OutputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(Stream, ILambdaContext)
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return new InvocationResponse(handlerWrapper.OutputStream, false);
            };
            return handlerWrapper;
        }

        /// <summary>
        /// Get a HandlerWrapper that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(PocoIn, ILambdaContext)
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return new InvocationResponse(handlerWrapper.OutputStream, false);
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(handlerWrapper.OutputStream, false));
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(handlerWrapper.OutputStream, false));
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(handlerWrapper.OutputStream, false));
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0; ;
                return Task.FromResult(new InvocationResponse(handlerWrapper.OutputStream, false));
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(handlerWrapper.OutputStream, false));
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
                handlerWrapper.OutputStream.SetLength(0);
                serializer.Serialize(output, handlerWrapper.OutputStream);
                handlerWrapper.OutputStream.Position = 0;
                return Task.FromResult(new InvocationResponse(handlerWrapper.OutputStream, false));
            };
            return handlerWrapper;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    OutputStream.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
