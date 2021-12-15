/*
 * Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport.Bootstrap;
using Amazon.Lambda.RuntimeSupport.ExceptionHandling;
using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.UnitTests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    [Collection("Bootstrap")]
    public class HandlerTests
    {
        private const string AggregateExceptionTestMarker = "AggregateExceptionTesting";
        private readonly InternalLogger _internalLogger;
        private readonly ITestOutputHelper _output;
        private const string ContextData = "==[Amazon.Lambda.RuntimeSupport.LambdaContext;Request1;Amazon.Lambda.RuntimeSupport.CognitoClientContext;" +
                                           "AppPackageName1;AppTitle1;AppVersionCode1;AppVersionName1;InstallationId1;CustomKey1, CustomKey2;" +
                                           "CustomValue1, CustomValue2;EnvironmentKey1, EnvironmentKey2;EnvironmentValue1, EnvironmentValue2;Name1;" +
                                           "Version1;Id1;Pool1;Arn1;Amazon.Lambda.RuntimeSupport.LambdaConsoleLogger;Group1;Stream1;42;420000000]==";
        private static readonly Action<string> NoOpLoggingAction = message => { };
        private readonly Dictionary<string, IEnumerable<string>> _headers;
        private readonly TestEnvironmentVariables _environmentVariables;

        // private readonly TestRuntimeApiClient _testRuntimeApiClient;

        public HandlerTests(ITestOutputHelper output)
        {
            // setup logger to write to optionally console
            _internalLogger = InternalLogger.GetCustomInternalLogger(output.WriteLine);
            _output = output;

            var testDateTimeHelper = new TestDateTimeHelper();
            var cognitoClientContext = File.ReadAllText("CognitoClientContext.json");
            var cognitoIdentity = File.ReadAllText("CognitoIdentity.json");

            _headers = new Dictionary<string, IEnumerable<string>>
            {
                { RuntimeApiHeaders.HeaderAwsRequestId, new List<string> { "Request1" } },
                { RuntimeApiHeaders.HeaderInvokedFunctionArn, new List<string> { "Arn1" } },
                { RuntimeApiHeaders.HeaderClientContext, new List<string> { cognitoClientContext } },
                { RuntimeApiHeaders.HeaderCognitoIdentity, new List<string> { cognitoIdentity } },
                { RuntimeApiHeaders.HeaderDeadlineMs, new List<string> { $"{(testDateTimeHelper.UtcNow - LambdaContext.UnixEpoch + TimeSpan.FromSeconds(42)).TotalMilliseconds}" } } // (2020, 1, 1) + 42 seconds
            };

            var env = new Dictionary<string, string>
            {
                { LambdaEnvironment.EnvVarFunctionName, "Name1" },
                { LambdaEnvironment.EnvVarFunctionVersion, "Version1" },
                { LambdaEnvironment.EnvVarLogGroupName, "Group1" },
                { LambdaEnvironment.EnvVarLogStreamName, "Stream1" },
                { LambdaEnvironment.EnvVarFunctionMemorySize, "42" },
            };

            _environmentVariables = new TestEnvironmentVariables(env);
        }

        [Fact]
        [Trait("Category", "UserCodeLoader")]
        public async Task PositiveHandlerTestsAsync()
        {
            const string testData = "Test data!";
            var dataIn = $"\"{testData}\"";
            var pocoData = $"{{ \"Data\": \"{testData}\" }}";

            await TestMethodAsync("ZeroInZeroOut");
            await TestMethodAsync("StringInZeroOut", dataIn, testData);
            await TestMethodAsync("StreamInZeroOut", dataIn, dataIn);
            await TestMethodAsync("ContextInZeroOut");
            await TestMethodAsync("ContextAndStringInZeroOut", dataIn, testData);
            await TestMethodAsync("ContextAndStreamInZeroOut", dataIn, dataIn);
            await TestMethodAsync("ContextAndPocoInZeroOut", pocoData, testData);

            await TestMethodAsync("ZeroInStringOut");
            await TestMethodAsync("ZeroInStreamOut");
            await TestMethodAsync("ZeroInMemoryStreamOut");
            await TestMethodAsync("ZeroInPocoOut");
            await TestMethodAsync("StringInStringOut", dataIn, testData);
            await TestMethodAsync("StreamInStreamOut", dataIn, dataIn);
            await TestMethodAsync("PocoInPocoOut", pocoData, testData);
            await TestMethodAsync("PocoInPocoOutStatic", pocoData, testData);
            await TestMethodAsync("ContextAndPocoInPocoOut", pocoData, testData);
            await TestMethodAsync("HandlerTest.CustomerPoco PocoInPocoOut(HandlerTest.CustomerPoco)", pocoData, testData);

            await TestMethodAsync("ZeroInTaskOut");

            await TestMethodAsync("ZeroInTaskStringOut");
            await TestMethodAsync("ZeroInTaskStreamOut");
            await TestMethodAsync("ZeroInTaskPocoOut");
            await TestMethodAsync("ZeroInTask2Out");
            await TestMethodAsync("ZeroInTTask2Out");
            await TestMethodAsync("ZeroInTTask3Out");
            await TestMethodAsync("ZeroInTTask4Out");
            await TestMethodAsync("ZeroInTTask5Out");

            await TestMethodAsync("CustomSerializerMethod");

            await TestHandlerAsync("HandlerTest::HandlerTest.AbstractCustomerType::NonAbstractMethodStringInStringOut", dataIn, testData);
            await TestHandlerAsync("HandlerTest::HandlerTest.SubclassOfGenericCustomerType::TInTOut", dataIn, testData);
            await TestHandlerAsync("HandlerTest::HandlerTest.StaticCustomerType::StaticCustomerMethodZeroOut", null, null, "StaticCustomerType static constructor has run.");

            await TestHandlerAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskOut", null, null, null);
            await TestHandlerAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskStringOut", null, null, null);

            var execInfo = await ExecMethodAsync("StreamInSameStreamOut_NonCommon", dataIn, null);
            Assert.Equal(dataIn, execInfo.DataIn);
            Assert.Equal(dataIn, execInfo.DataOut);
        }

        [Fact]
        [Trait("Category", "UserCodeLoader")]
        public async Task NegativeBootstrapInitTestsAsync()
        {
            var ucl = new UserCodeLoader("NonExistentAssembly::HandlerTest.CustomerType::ZeroInZeroOut", _internalLogger);
            var ex = Assert.Throws<LambdaValidationException>(() => ucl.Init(NoOpLoggingAction));
            Assert.Contains("Could not find the specified handler", ex.Message);

            await TestHandlerFailAsync($"HandlerTest2::Type::Method", "Could not find the specified handler assembly with the file name");

            await TestHandlerFailAsync("HandlerTest::HandlerTest.FakeCustomerType::PocoInPocoOut", "Unable to load type");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.GenericCustomerType`1::PocoInPocoOut", "Handler methods cannot be located in generic types.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::FakeMethod", "Found no methods matching method name 'FakeMethod'");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::HandlerTest.CustomerPoco FakeMethod(HandlerTest.CustomerPoco)", "Found no methods matching method name '");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::OverloadedMethod", "appears to have a number of overloads. To call this method please specify a complete method signature.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.AbstractCustomerType::AbstractMethod", "Please specify a non-abstract handler method.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::GenericMethod", "Handler methods cannot be generic.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::TwoInputsNoContextMethod", "is not supported: the method has 2 parameters, but the second parameter is not of type");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::TooManyInputsMethod", "is not supported: the method has more than 2 parameters.");

            await TestHandlerFailAsync("HandlerTestNoSerializer::HandlerTestNoSerializer.CustomerType::PocoInPocoOut", "To use types other than System.IO.Stream as input/output parameters, the assembly or Lambda function should be annotated with Amazon.Lambda.LambdaSerializerAttribute.");
            await TestHandlerFailAsync("HandlerTestNoSerializer::HandlerTestNoSerializer.CustomerType::PocoInPocoOut", "To use types other than System.IO.Stream as input/output parameters, the assembly or Lambda function should be annotated with Amazon.Lambda.LambdaSerializerAttribute.");

            var noZeroParamTypeEx = await TestHandlerFailAsync("HandlerTest::HandlerTest.NoZeroParamConstructorCustomerType::SimpleMethod", "No parameterless constructor defined");
            Assert.IsAssignableFrom<MissingMethodException>(noZeroParamTypeEx);

            var customerConstructorEx = TestHandlerFailAsync("HandlerTest::HandlerTest.ConstructorExceptionCustomerType::SimpleMethod", "An exception was thrown when the constructor for type");
            Assert.NotNull(customerConstructorEx);

            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::NoZeroParameterConstructorCustomerTypeSerializerMethod", "does not define a public zero-parameter constructor");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::NoInterfaceCustomerTypeSerializerMethod", "it does not implement the 'ILambdaSerializer' interface.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.StaticCustomerTypeThrows::StaticCustomerMethodZeroOut", "StaticCustomerTypeThrows static constructor has thrown an exception.");

        }

        [Fact]
        [Trait("Category", "HandlerInfo")]
        public void NegativeHandlerInfoTests()
        {
            Assert.Throws<LambdaValidationException>(() => new HandlerInfo(null));
            Assert.Throws<LambdaValidationException>(() => new HandlerInfo(" ::B::C"));
            Assert.Throws<LambdaValidationException>(() => new HandlerInfo("A:: ::C"));
            Assert.Throws<LambdaValidationException>(() => new HandlerInfo("A::B:: "));

            var ucl = new HandlerInfo("A::B::C::D");
            Assert.NotNull(ucl);
            Assert.Equal("A", ucl.AssemblyName.Name);
            Assert.Equal("B", ucl.TypeName);
            Assert.Equal("C::D", ucl.MethodName);
        }

        [Fact]
        [Trait("Category", "UserCodeLoader")]
        public async Task NegativeHandlerFailTestsAsync()
        {
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::Varargs", "Please specify a method that is not 'vararg'.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::Params", "Please specify a method that does not use 'params'.");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::MethodThatDoesNotExist", "Found no methods matching method name");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::OverloadedMethod", "The method 'OverloadedMethod' in type 'HandlerTest.CustomerType' appears to have a number of overloads. To call this method please specify a complete method signature. Possible candidates are:\nSystem.String OverloadedMethod(System.String)\nSystem.IO.Stream OverloadedMethod(System.IO.Stream)");
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::AsyncVoid", "Handler methods cannot be 'async void'. Please specify a method that is not 'async void'.");
        }

        [Fact]
        [Trait("Category", "UserCodeLoader")]
        public async Task UnwrapAggregateExceptionFailTestsAsync()
        {
            // unwrap AggregateException
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskOutThrowsException", AggregateExceptionTestMarker, false);

            // AggregateException thrown explicitly, won't get unwrapped whether we tell it to or not.
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskOutThrowsAggregateExceptionExplicitly", AggregateExceptionTestMarker, true);
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskOutThrowsAggregateExceptionExplicitly", AggregateExceptionTestMarker, true);

            // unwrap AggregateException
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskStringOutThrowsException", AggregateExceptionTestMarker, false);

            // AggregateException thrown explicitly, won't get unwrapped whether we tell it to or not.
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskStringOutThrowsAggregateExceptionExplicitly", AggregateExceptionTestMarker, true);
            await TestHandlerFailAsync("HandlerTest::HandlerTest.CustomerType::ZeroInTaskStringOutThrowsAggregateExceptionExplicitly", AggregateExceptionTestMarker, true);
        }

        [Fact]
        [Trait("Category", "UserCodeLoader")]
        public void NegativeILambdaSerializerTests()
        {
            TestILambdaSerializer(typeof(ILSClass), " is not an interface");
            TestILambdaSerializer(typeof(ILSEmpty), "'Deserialize' method not found");

            TestILambdaSerializer(typeof(ILSDeserializeNongeneric), "'Deserialize' method is not generic, expected to be generic");
            TestILambdaSerializer(typeof(ILSDeserializeNoInputs), "'Deserialize' method has '0' parameters, expected '1'");
            TestILambdaSerializer(typeof(ILSDeserializeWrongInput), "'Deserialize' method has parameter of type 'System.String', expected type 'System.IO.Stream'");
            TestILambdaSerializer(typeof(ILSDeserializeWrongGenerics), "'Deserialize' method has '2' generic arguments, expected '1'");
            TestILambdaSerializer(typeof(ILSDeserializeWrongOutput), "'Deserialize' method has return type of 'System.Object', expected 'T'");

            TestILambdaSerializer(typeof(ILSSerializeMissing), "'Serialize' method not found");
            TestILambdaSerializer(typeof(ILSSerializeNotGeneric), "'Serialize' method is not generic, expected to be generic");
            TestILambdaSerializer(typeof(ILSSerializeNotVoid), "'Serialize' method has return type of 'System.Object', expected 'void'");
            TestILambdaSerializer(typeof(ILSSerializeNoInputs), "'Serialize' method has '0' parameters, expected '2'");
            TestILambdaSerializer(typeof(ILSSerializeWrongGenerics), "'Serialize' method has '2' generic arguments, expected '1'");
            TestILambdaSerializer(typeof(ILSSerializeWrongFirstInput), "'Serialize' method's first parameter is of type 'System.Boolean', expected 'T'");
            TestILambdaSerializer(typeof(ILSSerializeWrongSecondInput), "'Serialize' method's second parameter is of type 'System.String', expected 'System.IO.Stream'");
        }

        private void TestILambdaSerializer(Type wrongType, string expectedPartialMessage)
        {
            _output.WriteLine($"Testing ILambdaSerializer {wrongType.FullName}");
            var exception = Assert.ThrowsAny<Exception>(() => UserCodeValidator.ValidateILambdaSerializerType(wrongType));
            Assert.NotNull(exception);
            Common.CheckException(exception, expectedPartialMessage);
        }

        private async Task<Exception> TestHandlerFailAsync(string handler, string expectedPartialMessage, bool? expectAggregateException = null)
        {
            _output.WriteLine($"Testing handler {handler}");

            var testRuntimeApiClient = new TestRuntimeApiClient(_environmentVariables, _headers);

            var userCodeLoader = new UserCodeLoader(handler, _internalLogger);
            var initializer = new UserCodeInitializer(userCodeLoader, _internalLogger);
            var handlerWrapper = HandlerWrapper.GetHandlerWrapper(userCodeLoader.Invoke);
            var bootstrap = new LambdaBootstrap(handlerWrapper, initializer.InitializeAsync)
            {
                Client = testRuntimeApiClient
            };

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var exceptionWaiterTask = Task.Run(() =>
                {
                    _output.WriteLine($"Waiting for an exception.");
                    while (testRuntimeApiClient.LastRecordedException == null)
                    {
                    }
                    _output.WriteLine($"Exception available.");
                    cancellationTokenSource.Cancel();
                    return testRuntimeApiClient.LastRecordedException;
                });

                await Record.ExceptionAsync(async () =>
                {
                    await bootstrap.RunAsync(cancellationTokenSource.Token);
                });

                var exception = await exceptionWaiterTask;
                Assert.NotNull(exception);

                Common.CheckException(exception, expectedPartialMessage);
                Common.CheckForAggregateException(exception, expectAggregateException);
                return exception;
            }
        }

        private async Task<string> InvokeAsync(LambdaBootstrap bootstrap, string dataIn, TestRuntimeApiClient testRuntimeApiClient)
        {
            testRuntimeApiClient.FunctionInput = dataIn != null ? Encoding.UTF8.GetBytes(dataIn) : new byte[0];

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var exceptionWaiterTask = Task.Run(async () =>
                {
                    _output.WriteLine($"Waiting for an output.");
                    while (testRuntimeApiClient.LastOutputStream == null)
                    {
                    }
                    _output.WriteLine($"Output available.");
                    cancellationTokenSource.Cancel();
                    using (var reader = new StreamReader(testRuntimeApiClient.LastOutputStream))
                    {
                        return await reader.ReadToEndAsync();
                    }
                });

                await bootstrap.RunAsync(cancellationTokenSource.Token);
                return await exceptionWaiterTask;
            }
        }

        private Task<ExecutionInfo> TestMethodAsync(string methodName, string dataIn = null, string testData = null)
        {
            var handler = $"HandlerTest::HandlerTest.CustomerType::{methodName}";
            return TestHandlerAsync(handler, dataIn, testData);
        }

        private async Task<ExecutionInfo> TestHandlerAsync(string handler, string dataIn, string testData, string assertLoggedByInitialize = null)
        {
            _output.WriteLine($"Testing handler '{handler}'");
            var execInfo = await ExecHandlerAsync(handler, dataIn, assertLoggedByInitialize);

            var customerMethodInfo = execInfo.UserCodeLoader.CustomerMethodInfo;
            var trueMethodName = customerMethodInfo.Name;
            var isCommon = !trueMethodName.Contains("_NonCommon");

            // Check logged data on common methods
            if (isCommon)
            {
                var fullMethodName = customerMethodInfo.ToString();
                var isContextMethod = trueMethodName.Contains("Context");
                var isVoidMethod = trueMethodName.Contains("ZeroOut") ||
                                   trueMethodName.Contains("TaskOut") ||
                                   trueMethodName.Equals("ZeroInTask2Out");

                Assert.True(execInfo.LoggingActionText.Contains($">>[{trueMethodName}]>>") ||
                            execInfo.LoggingActionText.Contains($">>[{fullMethodName}]>>"),
                    $"Can't find method name in console text for {trueMethodName}");

                if (dataIn != null)
                {
                    Assert.Contains($"<<[{testData}]<<", execInfo.LoggingActionText);
                }

                if (isContextMethod)
                {
                    Assert.Contains(ContextData, execInfo.LoggingActionText);
                }

                if (!isVoidMethod)
                {
                    Assert.NotNull(execInfo.DataOut);
                    Assert.True(execInfo.DataOut.Contains($"(([{trueMethodName}]))") ||
                                execInfo.DataOut.Contains($"(([{fullMethodName}]))"),
                        $"Expecting to find '{trueMethodName}' or '{fullMethodName}' in '{execInfo.DataOut}'");
                }

                Assert.True(execInfo.LoggingActionText.Contains($"__[nullLogger-{trueMethodName}]__") ||
                            execInfo.LoggingActionText.Contains($"__[nullLogger-{fullMethodName}]__"),
                    $"Can't find null logger output in action text for {trueMethodName}");
                Assert.True(execInfo.LoggingActionText.Contains($"__[testLogger-{trueMethodName}]__") ||
                            execInfo.LoggingActionText.Contains($"__[testLogger-{fullMethodName}]__"),
                    $"Can't find test logger output in action text for {trueMethodName}");
                Assert.False(execInfo.LoggingActionText.Contains($"##[nullLogger-{trueMethodName}]##") ||
                             execInfo.LoggingActionText.Contains($"##[nullLogger-{fullMethodName}]##"),
                    $"Found unexpected ILogger output in action text for {trueMethodName}: [{execInfo.LoggingActionText}]");

                Assert.True(execInfo.LoggingActionText.Contains($"^^[{trueMethodName}]^^") ||
                            execInfo.LoggingActionText.Contains($"^^[{fullMethodName}]^^"),
                    $"Can't find LambdaLogger output in action text for {trueMethodName}");
            }

            return execInfo;
        }

        private Task<ExecutionInfo> ExecMethodAsync(string methodName, string dataIn, string assertLoggedByInitialize)
        {
            var handler = $"HandlerTest::HandlerTest.CustomerType::{methodName}";
            return ExecHandlerAsync(handler, dataIn, assertLoggedByInitialize);
        }

        private async Task<ExecutionInfo> ExecHandlerAsync(string handler, string dataIn, string assertLoggedByInitialize)
        {
            // The actionWriter
            using (var actionWriter = new StringWriter())
            {
                var testRuntimeApiClient = new TestRuntimeApiClient(_environmentVariables, _headers);
                var loggerAction = actionWriter.ToLoggingAction();
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(UserCodeLoader.LambdaCoreAssemblyName));
                UserCodeLoader.SetCustomerLoggerLogAction(assembly, loggerAction, _internalLogger);

                var userCodeLoader = new UserCodeLoader(handler, _internalLogger);
                var handlerWrapper = HandlerWrapper.GetHandlerWrapper(userCodeLoader.Invoke);
                var initializer = new UserCodeInitializer(userCodeLoader, _internalLogger);
                var bootstrap = new LambdaBootstrap(handlerWrapper, initializer.InitializeAsync)
                {
                    Client = testRuntimeApiClient
                };

                if (assertLoggedByInitialize != null)
                {
                    Assert.False(actionWriter.ToString().Contains($"^^[{assertLoggedByInitialize}]^^"));
                }

                await bootstrap.InitializeAsync();

                if (assertLoggedByInitialize != null)
                {
                    Assert.True(actionWriter.ToString().Contains($"^^[{assertLoggedByInitialize}]^^"));
                }

                var dataOut = await InvokeAsync(bootstrap, dataIn, testRuntimeApiClient);
                var actionText = actionWriter.ToString();

                return new ExecutionInfo(bootstrap, dataIn, dataOut, actionText, null, userCodeLoader);
            }
        }

        private class ExecutionInfo
        {
            public string DataIn { get; }
            public string DataOut { get; }
            public string LoggingActionText { get; }
            public LambdaBootstrap Bootstrap { get; }
            public Exception Exception { get; }
            public UserCodeLoader UserCodeLoader { get; }

            public ExecutionInfo(LambdaBootstrap bootstrap, string dataIn, string dataOut, string loggingActionTest, Exception exception, UserCodeLoader userCodeLoader)
            {
                Bootstrap = bootstrap;
                DataIn = dataIn;
                DataOut = dataOut;
                LoggingActionText = loggingActionTest;
                Exception = exception;
                UserCodeLoader = userCodeLoader;
            }
        }
    }
}
