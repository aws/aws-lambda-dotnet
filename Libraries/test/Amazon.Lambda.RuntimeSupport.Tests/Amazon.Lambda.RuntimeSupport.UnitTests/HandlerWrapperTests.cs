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
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class HandlerWrapperTests
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private static readonly byte[] EmptyBytes = null;

        private static readonly byte[] InputBytes = new byte[5] { 0, 1, 2, 3, 4 };
        private static readonly byte[] OutputBytes = null;

        private const string StringInput = "mIxEd CaSe StRiNg";
        private static readonly byte[] StringInputBytes = null;
        private const string StringOutput = "MIXED CASE STRING";
        private static readonly byte[] StringOutputBytes = null;

        private static readonly PocoInput PocoInput = new PocoInput
        {
            InputInt = 0,
            InputString = "xyz"
        };
        private static readonly byte[] PocoInputBytes = null;
        private static readonly PocoOutput PocoOutput = new PocoOutput
        {
            OutputInt = 10,
            OutputString = "XYZ"
        };
        private static readonly byte[] PocoOutputBytes = null;

        static HandlerWrapperTests()
        {
            EmptyBytes = new byte[0];

            OutputBytes = new byte[InputBytes.Length];
            for (int i = 0; i < InputBytes.Length; i++)
            {
                OutputBytes[i] = (byte)(InputBytes[i] + 10);
            }

            MemoryStream tempStream;

            tempStream = new MemoryStream();
            Serializer.Serialize(StringInput, tempStream);
            StringInputBytes = new byte[tempStream.Length];
            tempStream.Position = 0;
            tempStream.Read(StringInputBytes, 0, StringInputBytes.Length);

            tempStream = new MemoryStream();
            Serializer.Serialize(StringOutput, tempStream);
            StringOutputBytes = new byte[tempStream.Length];
            tempStream.Position = 0;
            tempStream.Read(StringOutputBytes, 0, StringOutputBytes.Length);

            tempStream = new MemoryStream();
            Serializer.Serialize(PocoInput, tempStream);
            PocoInputBytes = new byte[tempStream.Length];
            tempStream.Position = 0;
            tempStream.Read(PocoInputBytes, 0, PocoInputBytes.Length);

            tempStream = new MemoryStream();
            Serializer.Serialize(PocoOutput, tempStream);
            PocoOutputBytes = new byte[tempStream.Length];
            tempStream.Position = 0;
            tempStream.Read(PocoOutputBytes, 0, PocoOutputBytes.Length);
        }

        private LambdaEnvironment _lambdaEnvironment;
        private RuntimeApiHeaders _runtimeApiHeaders;
        private Checkpoint _checkpoint;

        public HandlerWrapperTests()
        {
            var environmentVariables = new TestEnvironmentVariables();
            _lambdaEnvironment = new LambdaEnvironment(environmentVariables);

            var headers = new Dictionary<string, IEnumerable<string>>();
            headers.Add(RuntimeApiHeaders.HeaderAwsRequestId, new List<string>() { "request_id" });
            headers.Add(RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string>() { "invoked_function_arn" });
            _runtimeApiHeaders = new RuntimeApiHeaders(headers);
            _checkpoint = new Checkpoint();
        }

        [Fact]
        public async Task TestTask()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async () =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamTask()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputTask()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestILambdaContextTask()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamILambdaContextTask()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputILambdaContextTask()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestTaskOfStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async () =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestStreamTaskOfStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestPocoInputTaskOfStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return new MemoryStream(OutputBytes);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestContextTaskOfStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestStreamContextTaskOfStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestPocoInputContextTaskOfStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestTaskOfPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async () =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamTaskOfPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputTaskOfPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestILambdaContextTaskOfPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamILambdaContextTaskOfPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputILambdaContextTaskOfPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestVoid()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(() =>
            {
                _checkpoint.Check();
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamVoid()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((input) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputVoid()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestILambdaContextVoid()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((context) =>
            {
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamILambdaContextVoid()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((input, context) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputILambdaContextVoid()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>((input, context) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, EmptyBytes, false);
            }
        }

        [Fact]
        public async Task TestVoidStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(() =>
            {
                _checkpoint.Check();
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestStreamStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((input) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestPocoInputStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return new MemoryStream(OutputBytes);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestILambdaContextStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((context) =>
            {
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestStreamILambdaContextStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((input, context) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestPocoInputILambdaContextStream()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput>((input, context) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, OutputBytes, true);
            }
        }

        [Fact]
        public async Task TestVoidPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper(() =>
            {
                _checkpoint.Check();
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((input) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestILambdaContextPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((context) =>
            {
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, EmptyBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestStreamILambdaContextPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper((input, context) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, InputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestPocoInputILambdaContextPocoOutput()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<PocoInput, PocoOutput>((input, context) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, PocoInputBytes, PocoOutputBytes, false);
            }
        }

        [Fact]
        public async Task TestSerializtionOfString()
        {
            using (var handlerWrapper = HandlerWrapper.GetHandlerWrapper<string, string>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(StringInput, input);
                return StringOutput;
            }, Serializer))
            {
                await TestHandlerWrapper(handlerWrapper, StringInputBytes, StringOutputBytes, false);
            }
        }

        private async Task TestHandlerWrapper(HandlerWrapper handlerWrapper, byte[] input, byte[] expectedOutput, bool expectedDisposeOutputStream)
        {
            // run twice to make sure wrappers that reuse the output stream work correctly
            for (int i = 0; i < 2; i++)
            {
                var invocation = new InvocationRequest
                {
                    InputStream = new MemoryStream(input ?? new byte[0]),
                    LambdaContext = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment, new Helpers.SimpleLoggerWriter())
                };

                var invocationResponse = await handlerWrapper.Handler(invocation);

                Assert.True(_checkpoint.IsChecked);
                Assert.Equal(expectedDisposeOutputStream, invocationResponse.DisposeOutputStream);
                AssertEqual(expectedOutput, invocationResponse.OutputStream);
            }
        }

        private void AssertEqual(byte[] expected, Stream actual)
        {
            Assert.NotNull(actual);
            var actualBytes = new byte[actual.Length];
            actual.Read(actualBytes, 0, actualBytes.Length);
            AssertEqual(expected, actualBytes);
        }

        private void AssertEqual(byte[] expected, byte[] actual)
        {
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            Assert.True(expected != null && actual != null);
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }

        public class Checkpoint
        {
            public bool IsChecked { get; set; }

            public void Check()
            {
                IsChecked = true;
            }
        }
    }
}
