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

using Amazon.Lambda.Core;

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HandlerTest
{
    public class CustomerType
    {
        private const string AggregateExceptionTestMarker = "AggregateExceptionTesting";

        [LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
        public async Task<CustomerPocoOut> AsyncPocosContextDefaultSerializer(CustomerPocoIn data, ILambdaContext context)
        {
            Console.WriteLine($"Context.RemainingTime: '{context.RemainingTime}'");
            Console.WriteLine($"Sleeping for {delayTime}...");
            await Task.Delay(delayTime);
            Console.WriteLine($"Context.RemainingTime: '{context.RemainingTime}'");

            return new CustomerPocoOut($"Hi '{data.Data}'!");
        }

        public Stream HelloWorld(Stream input)
        {
            var loggerFactory = Common.LoggerFactory;
            var logger = loggerFactory.CreateLogger("HelloWorld");

            logger.LogInformation($"Current time: '{DateTime.Now}'");
            logger.LogDebug($"Sleeping for {delayTime}...");
            Task.Delay(delayTime).Wait();
            logger.LogWarning($"Current time: '{DateTime.Now}'");

            var bytes = System.Text.Encoding.UTF8.GetBytes("Hello World!");
            var stream = new MemoryStream(bytes);
            return stream;
        }


        private static TimeSpan delayTime = TimeSpan.FromMilliseconds(100);

        // void-out methods
        public void ZeroInZeroOut()
        {
            Common.LogCommonData("ZeroInZeroOut");
        }
        public void ZeroInZeroOutThrowsException()
        {
            throw new Exception();
        }
        public void StringInZeroOut(string data)
        {
            Common.LogCommonData("StringInZeroOut", data);
        }
        public void StreamInZeroOut(Stream data)
        {
            Common.LogCommonData("StreamInZeroOut", Common.GetString(data));
        }
        public void PocoInZeroOut(CustomerPoco data)
        {
            Common.LogCommonData("PocoInZeroOut", data.Data);
        }
        public void ContextInZeroOut(ILambdaContext context)
        {
            Common.LogCommonData("ContextInZeroOut", context);
        }
        public void ContextAndStringInZeroOut(string data, ILambdaContext context)
        {
            Common.LogCommonData("ContextAndStringInZeroOut", data, context);
        }
        public void ContextAndStreamInZeroOut(Stream data, ILambdaContext context)
        {
            Common.LogCommonData("ContextAndStreamInZeroOut", Common.GetString(data), context);
        }
        public void ContextAndPocoInZeroOut(CustomerPoco poco, ILambdaContext context)
        {
            Common.LogCommonData("ContextAndPocoInZeroOut", poco.Data, context);
        }

        // T-out methods
        public string ZeroInStringOut()
        {
            Common.LogCommonData("ZeroInStringOut");
            return "(([ZeroInStringOut]))";
        }
        public Stream ZeroInStreamOut()
        {
            Common.LogCommonData("ZeroInStreamOut");
            return Common.GetStream("(([ZeroInStreamOut]))");
        }
        public MemoryStream ZeroInMemoryStreamOut()
        {
            Common.LogCommonData("ZeroInMemoryStreamOut");
            return Common.GetStream("(([ZeroInMemoryStreamOut]))");
        }
        public CustomerPoco ZeroInPocoOut()
        {
            Common.LogCommonData("ZeroInPocoOut");
            return new CustomerPoco("(([ZeroInPocoOut]))");
        }
        public string StringInStringOut(string data)
        {
            Common.LogCommonData("StringInStringOut", data);
            return "(([StringInStringOut]))";
        }
        public Stream StreamInStreamOut(Stream data)
        {
            Common.LogCommonData("StreamInStreamOut", Common.GetString(data));
            return Common.GetStream("(([StreamInStreamOut]))");
        }
        public CustomerPoco PocoInPocoOut(CustomerPoco data)
        {
            Common.LogCommonData("PocoInPocoOut", data.Data);
            return new CustomerPoco("(([PocoInPocoOut]))");
        }
        public CustomerPoco ContextAndPocoInPocoOut(CustomerPoco data, ILambdaContext context)
        {
            Common.LogCommonData("ContextAndPocoInPocoOut", data.Data, context);
            return new CustomerPoco("(([ContextAndPocoInPocoOut]))");
        }

        public static CustomerPocoOut PocoInPocoOutStatic(CustomerPocoIn data)
        {
            Common.LogCommonData("PocoInPocoOutStatic", data.Data);
            return new CustomerPocoOut("(([PocoInPocoOutStatic]))");
        }

        // Task-out methods
        public async Task ZeroInTaskOut()
        {
            await Task.Delay(delayTime);
            Common.LogCommonData("ZeroInTaskOut");
        }
        public async Task ZeroInTaskOutThrowsException()
        {
            await Task.Delay(delayTime);
            throw new Exception(AggregateExceptionTestMarker);
        }
        public async Task ZeroInTaskOutThrowsAggregateExceptionExplicitly()
        {
            await Task.Delay(delayTime);
            throw new AggregateException(new Exception(AggregateExceptionTestMarker));
        }
        public Task ZeroInTaskOutSync()
        {
            return Task.Run(() =>
            {
                Task.Delay(delayTime).Wait();
                Common.LogCommonData("ZeroInTaskOutSync");
            });
        }

        // TaskT-out methods
        public async Task<string> ZeroInTaskStringOut()
        {
            await Task.Delay(delayTime);
            Common.LogCommonData("ZeroInTaskStringOut");
            return "(([ZeroInTaskStringOut]))";
        }
        public async Task<string> ZeroInTaskStringOutThrowsException()
        {
            await Task.Delay(delayTime);
            throw new Exception(AggregateExceptionTestMarker);
        }
        public async Task<string> ZeroInTaskStringOutThrowsAggregateExceptionExplicitly()
        {
            await Task.Delay(delayTime);
            throw new AggregateException(new Exception(AggregateExceptionTestMarker));
        }
        public async Task<Stream> ZeroInTaskStreamOut()
        {
            await Task.Delay(delayTime);
            Common.LogCommonData("ZeroInTaskStreamOut");
            return Common.GetStream("(([ZeroInTaskStreamOut]))");
        }
        public async Task<CustomerPoco> ZeroInTaskPocoOut()
        {
            await Task.Delay(delayTime);
            Common.LogCommonData("ZeroInTaskPocoOut");
            return new CustomerPoco("(([ZeroInTaskPocoOut]))");
        }

        // Generic methods
        public T GenericMethod<T>(T input)
        {
            return input;
        }

        // Overloaded methods
        public string OverloadedMethod(string input)
        {
            return input;
        }
        public Stream OverloadedMethod(Stream input)
        {
            return input;
        }

        // Custom serializer methods
        [LambdaSerializer(typeof(ConstructorExceptionCustomerTypeSerializer))]
        public CustomerPocoOut ErrorSerializerMethod(CustomerPocoIn input)
        {
            return new CustomerPocoOut(input.Data);
        }
        [LambdaSerializer(typeof(SpecialCustomerTypeSerializer))]
        public SpecialCustomerPoco CustomSerializerMethod(SpecialCustomerPoco input)
        {
            Common.LogCommonData("CustomSerializerMethod", input.Data);
            return new SpecialCustomerPoco("(([CustomSerializerMethod]))");
        }
        [LambdaSerializer(typeof(NoZeroParameterConstructorCustomerTypeSerializer))]
        public SpecialCustomerPoco NoZeroParameterConstructorCustomerTypeSerializerMethod(SpecialCustomerPoco input)
        {
            Common.LogCommonData("NoZeroParameterConstructorCustomerTypeSerializerMethod", input.Data);
            return new SpecialCustomerPoco("(([NoZeroParameterConstructorCustomerTypeSerializerMethod]))");
        }
        [LambdaSerializer(typeof(ExceptionInConstructorCustomerTypeSerializer))]
        public SpecialCustomerPoco ExceptionInConstructorCustomerTypeSerializerMethod(SpecialCustomerPoco input)
        {
            Common.LogCommonData("ExceptionInConstructorCustomerTypeSerializerMethod", input.Data);
            return new SpecialCustomerPoco("(([ExceptionInConstructorCustomerTypeSerializerMethod]))");
        }
        [LambdaSerializer(typeof(NoInterfaceCustomerTypeSerializer))]
        public SpecialCustomerPoco NoInterfaceCustomerTypeSerializerMethod(SpecialCustomerPoco input)
        {
            Common.LogCommonData("NoZeroParameterConstructorCustomerTypeSerializerMethod", input.Data);
            return new SpecialCustomerPoco("(([NoZeroParameterConstructorCustomerTypeSerializerMethod]))");
        }

        // Too many inputs
        public void TwoInputsNoContextMethod(int a, int b)
        {
        }
        public void TooManyInputsMethod(int a, int b, int c)
        {
        }

        // params inputs
        public void Params(params string[] args)
        {

        }
        public void Varargs(__arglist)
        {

        }


        // LambdaLogger.Log method
        public Stream StreamInSameStreamOut_NonCommon(Stream value)
        {
            return value;
        }

        // async void
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async void AsyncVoid()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {

        }

        // Task subclasses
        public Task2 ZeroInTask2Out()
        {
            var t = new Task2(() =>
            {
                Task.Delay(delayTime).Wait();
                Common.LogCommonData("ZeroInTask2Out");
            });
            t.RunSynchronously();
            return t;
        }
        public Task2<string> ZeroInTTask2Out()
        {
            var t = new Task2<string>(() =>
            {
                Task.Delay(delayTime).Wait();
                Common.LogCommonData("ZeroInTTask2Out");
                return "(([ZeroInTTask2Out]))";
            });
            t.RunSynchronously();
            return t;
        }
        public Task3 ZeroInTTask3Out()
        {
            var t = new Task3(() =>
            {
                Task.Delay(delayTime).Wait();
                Common.LogCommonData("ZeroInTTask3Out");
                return "(([ZeroInTTask3Out]))";
            });
            t.RunSynchronously();
            return t;
        }
        public Task4<string, int> ZeroInTTask4Out()
        {
            var t = new Task4<string, int>(() =>
            {
                Task.Delay(delayTime).Wait();
                Common.LogCommonData("ZeroInTTask4Out");
                return "(([ZeroInTTask4Out]))";
            });
            t.RunSynchronously();
            return t;
        }
        public Task5<int> ZeroInTTask5Out()
        {
            var t = new Task5<int>(() =>
            {
                Task.Delay(delayTime).Wait();
                Common.LogCommonData("ZeroInTTask5Out");
                return "(([ZeroInTTask5Out]))";
            });
            t.RunSynchronously();
            return t;
        }
    }

    public abstract class AbstractCustomerType
    {
        public abstract string AbstractMethod(string input);
        public static string NonAbstractMethodStringInStringOut(string input)
        {
            Common.LogCommonData("NonAbstractMethodStringInStringOut", input);
            return "(([NonAbstractMethodStringInStringOut]))";
        }
    }
    public class ConstructorExceptionCustomerType
    {
        public ConstructorExceptionCustomerType()
        {
            throw new Exception();
        }
        public void SimpleMethod()
        {

        }
    }
    public class GenericCustomerType<T>
    {
        public CustomerPoco PocoInPocoOut(CustomerPoco data)
        {
            Common.LogCommonData("PocoInPocoOut", data.Data);
            return new CustomerPoco("(([PocoInPocoOut]))");
        }
        public T TInTOut(T input)
        {
            return Worker(input);
        }
        protected virtual T Worker(T input)
        {
            throw new NotImplementedException();
        }
    }
    public class SubclassOfGenericCustomerType : GenericCustomerType<string>
    {
        protected override string Worker(string input)
        {
            Common.LogCommonData("TInTOut", input);
            return "(([TInTOut]))";
        }
    }
    public class NoZeroParamConstructorCustomerType
    {
        public NoZeroParamConstructorCustomerType(int data)
        {

        }
        public void SimpleMethod()
        {

        }
    }

    public static class StaticCustomerTypeThrows
    {
        static StaticCustomerTypeThrows()
        {
            throw new Exception(nameof(StaticCustomerTypeThrows) + " static constructor has thrown an exception.");
        }

        public static void StaticCustomerMethodZeroOut()
        {
            Common.LogCommonData(nameof(StaticCustomerMethodZeroOut));
        }
    }

    public static class StaticCustomerType
    {
        static StaticCustomerType()
        {
            Common.LogCommonData(nameof(StaticCustomerType) + " static constructor has run.");
        }

        public static void StaticCustomerMethodZeroOut()
        {
            Common.LogCommonData(nameof(StaticCustomerMethodZeroOut));
        }
    }
}
