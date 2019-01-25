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

namespace Amazon.Lambda.RuntimeSupport.Test
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
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async () =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
            });

            await TestHandler(handler, EmptyBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestStreamTask()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
            });

            await TestHandler(handler, InputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestPocoInputTask()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestILambdaContextTask()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
            });

            await TestHandler(handler, EmptyBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestStreamILambdaContextTask()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
            });

            await TestHandler(handler, InputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestPocoInputILambdaContextTask()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestTaskOfStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async () =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, EmptyBytes, OutputBytes);
        }

        [Fact]
        public async Task TestStreamTaskOfStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, InputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestPocoInputTaskOfStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return new MemoryStream(OutputBytes);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestContextTaskOfStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, EmptyBytes, OutputBytes);
        }

        [Fact]
        public async Task TestStreamContextTaskOfStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, InputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestPocoInputContextTaskOfStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestTaskOfPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async () =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, EmptyBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestStreamTaskOfPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, InputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestPocoInputTaskOfPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput, PocoOutput>(async (input) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestILambdaContextTaskOfPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, EmptyBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestStreamILambdaContextTaskOfPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, InputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestPocoInputILambdaContextTaskOfPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput, PocoOutput>(async (input, context) =>
            {
                await Task.Delay(0);
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestVoid()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(() =>
            {
                _checkpoint.Check();
            });

            await TestHandler(handler, EmptyBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestStreamVoid()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((input) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
            });

            await TestHandler(handler, InputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestPocoInputVoid()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestILambdaContextVoid()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((context) =>
            {
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
            });

            await TestHandler(handler, EmptyBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestStreamILambdaContextVoid()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((input, context) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
            });

            await TestHandler(handler, InputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestPocoInputILambdaContextVoid()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>((input, context) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, EmptyBytes);
        }

        [Fact]
        public async Task TestVoidStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(() =>
            {
                _checkpoint.Check();
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, EmptyBytes, OutputBytes);
        }

        [Fact]
        public async Task TestStreamStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((input) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, InputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestPocoInputStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return new MemoryStream(OutputBytes);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestILambdaContextStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((context) =>
            {
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, EmptyBytes, OutputBytes);
        }

        [Fact]
        public async Task TestStreamILambdaContextStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((input, context) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            });

            await TestHandler(handler, InputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestPocoInputILambdaContextStream()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput>((input, context) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return new MemoryStream(OutputBytes);
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, OutputBytes);
        }

        [Fact]
        public async Task TestVoidPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler(() =>
            {
                _checkpoint.Check();
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, EmptyBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestStreamPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((input) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, InputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestPocoInputPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput, PocoOutput>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestILambdaContextPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((context) =>
            {
                _checkpoint.Check();
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, EmptyBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestStreamILambdaContextPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler((input, context) =>
            {
                _checkpoint.Check();
                AssertEqual(InputBytes, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, InputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestPocoInputILambdaContextPocoOutput()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<PocoInput, PocoOutput>((input, context) =>
            {
                _checkpoint.Check();
                Assert.Equal(PocoInput, input);
                Assert.NotNull(context.AwsRequestId);
                return PocoOutput;
            }, Serializer);

            await TestHandler(handler, PocoInputBytes, PocoOutputBytes);
        }

        [Fact]
        public async Task TestSerializtionOfString()
        {
            var handler = HandlerWrapper.GetLambdaBootstrapHandler<string, string>((input) =>
            {
                _checkpoint.Check();
                Assert.Equal(StringInput, input);
                return StringOutput;
            }, Serializer);

            await TestHandler(handler, StringInputBytes, StringOutputBytes);
        }

        private async Task TestHandler(LambdaBootstrapHandler handler, byte[] input, byte[] expectedOutput)
        {
            var invocation = new InvocationRequest
            {
                InputStream = new MemoryStream(input ?? new byte[0]),
                LambdaContext = new LambdaContext(_runtimeApiHeaders, _lambdaEnvironment)
            };

            var outputStream = await handler(invocation);

            Assert.True(_checkpoint.IsChecked);
            AssertEqual(expectedOutput, outputStream);
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
