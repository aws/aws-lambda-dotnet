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
    /// This class provides methods that help you wrap legacy C# Lambda implementations with LambdaBootstrapHandler delegates.
    /// This allows you to use them with an instance of LambdaBootstrap.
    /// </summary>
    public static class HandlerWrapper
    {
        private static Stream GetEmptyStream() => new MemoryStream();
        private static Task<Stream> GetEmptyStreamTask() => Task.FromResult(GetEmptyStream());

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler();
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrapHandler</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Task> handler)
        {
            return async (invocation) =>
            {
                await handler();
                return GetEmptyStream();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream, Task> handler)
        {
            return async (invocation) =>
            {
                await handler(invocation.InputStream);
                return GetEmptyStream();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Func<TInput, Task> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                await handler(input);
                return GetEmptyStream();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<ILambdaContext, Task> handler)
        {
            return async (invocation) =>
            {
                await handler(invocation.LambdaContext);
                return GetEmptyStream();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream, ILambdaContext, Task> handler)
        {
            return async (invocation) =>
            {
                await handler(invocation.InputStream, invocation.LambdaContext);
                return GetEmptyStream();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Func<TInput, ILambdaContext, Task> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                await handler(input, invocation.LambdaContext);
                return GetEmptyStream();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltStream&gt Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Task<Stream>> handler)
        {
            return async (invocation) =>
            {
                return await handler();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltStream&gt Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream, Task<Stream>> handler)
        {
            return async (invocation) =>
            {
                return await handler(invocation.InputStream);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltStream&gt Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Func<TInput, Task<Stream>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return await handler(input);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltStream&gt Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<ILambdaContext, Task<Stream>> handler)
        {
            return async (invocation) =>
            {
                return await handler(invocation.LambdaContext);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltStream&gt Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream, ILambdaContext, Task<Stream>> handler)
        {
            return async (invocation) =>
            {
                return await handler(invocation.InputStream, invocation.LambdaContext);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltStream&gt Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Func<TInput, ILambdaContext, Task<Stream>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return await handler(input, invocation.LambdaContext);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TOutput output = await handler();
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return outputStream;
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<Stream, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TOutput output = await handler(invocation.InputStream);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return outputStream;
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput, TOutput>(Func<TInput, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = await handler(input);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return outputStream;
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TOutput output = await handler(invocation.LambdaContext);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0; ;
                return outputStream;
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<Stream, ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TOutput output = await handler(invocation.InputStream, invocation.LambdaContext);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return outputStream;
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Task&ltPocoOut&gt Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput, TOutput>(Func<TInput, ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return async (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = await handler(input, invocation.LambdaContext);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return outputStream;
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler()
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Action handler)
        {
            return (invocation) =>
            {
                handler();
                return GetEmptyStreamTask();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(Stream)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Action<Stream> handler)
        {
            return (invocation) =>
            {
                handler(invocation.InputStream);
                return GetEmptyStreamTask();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Action<TInput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                handler(input);
                return GetEmptyStreamTask();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Action<ILambdaContext> handler)
        {
            return (invocation) =>
            {
                handler(invocation.LambdaContext);
                return GetEmptyStreamTask();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Action<Stream, ILambdaContext> handler)
        {
            return (invocation) =>
            {
                handler(invocation.InputStream, invocation.LambdaContext);
                return GetEmptyStreamTask();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: void Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Action called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Action<TInput, ILambdaContext> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                handler(input, invocation.LambdaContext);
                return GetEmptyStreamTask();
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream> handler)
        {
            return (invocation) =>
            {
                return Task.FromResult(handler());
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream, Stream> handler)
        {
            return (invocation) =>
            {
                return Task.FromResult(handler(invocation.InputStream));
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Func<TInput, Stream> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return Task.FromResult(handler(input));
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<ILambdaContext, Stream> handler)
        {
            return (invocation) =>
            {
                return Task.FromResult(handler(invocation.LambdaContext));
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler(Func<Stream, ILambdaContext, Stream> handler)
        {
            return (invocation) =>
            {
                return Task.FromResult(handler(invocation.InputStream, invocation.LambdaContext));
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: Stream Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput>(Func<TInput, ILambdaContext, Stream> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                return Task.FromResult(handler(input, invocation.LambdaContext));
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler()
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<TOutput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TOutput output = handler();
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult((Stream)outputStream);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(Stream)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<Stream, TOutput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TOutput output = handler(invocation.InputStream);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult((Stream)outputStream);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(PocoIn)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput, TOutput>(Func<TInput, TOutput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = handler(input);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult((Stream)outputStream);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TOutput output = handler(invocation.LambdaContext);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0; ;
                return Task.FromResult((Stream)outputStream);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(Stream, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TOutput>(Func<Stream, ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TOutput output = handler(invocation.InputStream, invocation.LambdaContext);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult((Stream)outputStream);
            };
        }

        /// <summary>
        /// Get a LambdaBootstrapHandler that will call the given method on function invocation.
        /// Note that you may have to cast your handler to its specific type to help the compiler.
        /// Example handler signature: PocoOut Handler(PocoIn, ILambdaContext)
        /// </summary>
        /// <param name="handler">Func called for each invocation of the Lambda function.</param>
        /// <param name="serializer">ILambdaSerializer to use when calling the handler</param>
        /// <returns>A LambdaBootstrap object.</returns>
        public static LambdaBootstrapHandler GetLambdaBootstrapHandler<TInput, TOutput>(Func<TInput, ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            return (invocation) =>
            {
                TInput input = serializer.Deserialize<TInput>(invocation.InputStream);
                TOutput output = handler(input, invocation.LambdaContext);
                var outputStream = new MemoryStream();
                serializer.Serialize(output, outputStream);
                outputStream.Position = 0;
                return Task.FromResult((Stream)outputStream);
            };
        }
    }
}
