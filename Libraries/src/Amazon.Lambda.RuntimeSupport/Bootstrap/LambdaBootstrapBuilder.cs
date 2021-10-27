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
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.RuntimeSupport
{
    /// <summary>
    /// Class to communicate with the Lambda Runtime API, handle initialization,
    /// and run the invoke loop for an AWS Lambda function
    /// </summary>
    public class LambdaBootstrapBuilder
    {
        private HandlerWrapper _handlerWrapper;
        private HttpClient _httpClient;
        private LambdaBootstrapInitializer _lambdaBootstrapInitializer;

        private LambdaBootstrapBuilder(HandlerWrapper handlerWrapper)
        {
            this._handlerWrapper = handlerWrapper;
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Action<Stream, ILambdaContext, MemoryStream> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Task> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream, Task> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Func<TInput, Task> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<ILambdaContext, Task> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream, ILambdaContext, Task> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Func<TInput, ILambdaContext, Task> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Task<Stream>> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream, Task<Stream>> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Func<TInput, Task<Stream>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<ILambdaContext, Task<Stream>> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream, ILambdaContext, Task<Stream>> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Func<TInput, ILambdaContext, Task<Stream>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<Stream, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput, TOutput>(Func<TInput, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput, TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<Stream, ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput, TOutput>(Func<TInput, ILambdaContext, Task<TOutput>> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput, TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Action handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Action<Stream> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Action<TInput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Action<ILambdaContext> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Action<Stream, ILambdaContext> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Action<TInput, ILambdaContext> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream, Stream> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Func<TInput, Stream> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<ILambdaContext, Stream> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create(Func<Stream, ILambdaContext, Stream> handler)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper(handler));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput>(Func<TInput, ILambdaContext, Stream> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<TOutput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<Stream, TOutput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput, TOutput>(Func<TInput, TOutput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput, TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TOutput>(Func<Stream, ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TOutput>(handler, serializer));
        }

        /// <summary>
        /// Create a builder for creating the LambdaBootstrap.
        /// </summary>
        /// <param name="handler">The handler that will be called for each Lambda invocation</param>
        /// <param name="serializer">The Lambda serializer that will be used to convert between Lambda's JSON documents and .NET objects.</param>
        /// <returns></returns>
        public static LambdaBootstrapBuilder Create<TInput, TOutput>(Func<TInput, ILambdaContext, TOutput> handler, ILambdaSerializer serializer)
        {
            return new LambdaBootstrapBuilder(HandlerWrapper.GetHandlerWrapper<TInput, TOutput>(handler, serializer));
        }

        /// <summary>
        /// Configure the bootstrap to use a provided HttpClient
        /// </summary>
        /// <param name="httpClient"></param>
        /// <returns></returns>
        public LambdaBootstrapBuilder UseHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            return this;
        }

        /// <summary>
        /// Configure a custom bootstrap initalizer delegate
        /// </summary>
        /// <param name="lambdaBootstrapInitializer"></param>
        /// <returns></returns>
        public LambdaBootstrapBuilder UseBootstrapHandler(LambdaBootstrapInitializer lambdaBootstrapInitializer)
        {
            _lambdaBootstrapInitializer = lambdaBootstrapInitializer;
            return this;
        }

        public LambdaBootstrap Build()
        {
            if(_httpClient == null)
            {
                return new LambdaBootstrap(_handlerWrapper, _lambdaBootstrapInitializer);
            }

            return new LambdaBootstrap(_httpClient, _handlerWrapper, _lambdaBootstrapInitializer);
        }
    }
}
