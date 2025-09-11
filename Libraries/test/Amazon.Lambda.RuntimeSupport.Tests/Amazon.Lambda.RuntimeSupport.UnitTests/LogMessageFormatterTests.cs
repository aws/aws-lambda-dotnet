using Amazon.Lambda.RuntimeSupport.Helpers;
using Amazon.Lambda.RuntimeSupport.Helpers.Logging;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using Xunit;
using static Amazon.Lambda.RuntimeSupport.UnitTests.LogMessageFormatterTests;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{
    public class LogMessageFormatterTests
    {
        [Fact]
        public void ParseLogMessageWithNoProperties()
        {
            var formatter = new JsonLogMessageFormatter();

            var properties = formatter.ParseProperties("No message properties here");
            Assert.Empty(properties);
        }

        [Fact]
        public void ParseLogMessageWithProperties()
        {
            var formatter = new JsonLogMessageFormatter();

            var properties = formatter.ParseProperties("User {user} bought {count:000} items of {@product} with an {{escaped}}");
            Assert.Equal(3, properties.Count);

            Assert.Equal("user", properties[0].Name);
            Assert.Equal(MessageProperty.Directive.Default, properties[0].FormatDirective);

            Assert.Equal("count", properties[1].Name);
            Assert.Equal("000", properties[1].FormatArgument);
            Assert.Equal(MessageProperty.Directive.Default, properties[1].FormatDirective);

            Assert.Equal("product", properties[2].Name);
            Assert.Equal(MessageProperty.Directive.JsonSerialization, properties[2].FormatDirective);
        }

        [Fact]
        public void ParseLogMessageWithOpenBracketAndNoClosing()
        {
            var formatter = new JsonLogMessageFormatter();

            var properties = formatter.ParseProperties("{hello} before { after");
            Assert.Equal(1, properties.Count);

            Assert.Equal("hello", properties[0].Name);
            Assert.Equal(MessageProperty.Directive.Default, properties[0].FormatDirective);
        }

        [Fact]
        public void FormatJsonWithNoMessageProperties()
        {
            var timestamp = DateTime.UtcNow;
            var formattedTimestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                TraceId = "4321",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Simple Log Message",
                MessageArguments = null,
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);
            Assert.Equal("1234", doc.RootElement.GetProperty("requestId").GetString());
            Assert.Equal("4321", doc.RootElement.GetProperty("traceId").GetString());
            Assert.Equal(formattedTimestamp, doc.RootElement.GetProperty("timestamp").GetString());
            Assert.Equal("Warning", doc.RootElement.GetProperty("level").GetString());
            Assert.Equal("Simple Log Message", doc.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public void FormatJsonWithStringMessageProperties()
        {
            var timestamp = DateTime.UtcNow;
            var formattedTimestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Hello {name}",
                MessageArguments = new object[] { "AWS" },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);
            Assert.Equal("1234", doc.RootElement.GetProperty("requestId").GetString());
            Assert.Equal(formattedTimestamp, doc.RootElement.GetProperty("timestamp").GetString());
            Assert.Equal("Warning", doc.RootElement.GetProperty("level").GetString());
            Assert.Equal("Hello AWS", doc.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public void FormatJsonNullMessageTemplate()
        {
            var timestamp = DateTime.UtcNow;
            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                MessageTemplate = null,
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);
            Assert.Equal(string.Empty, doc.RootElement.GetProperty("message").GetString());
            Assert.Equal("1234", doc.RootElement.GetProperty("requestId").GetString());

            // Currently the arguments are ignored because they don't exist in the message template but 
            // having arguments uses a different code path so we need to make sure a NPE doesn't happen.
            state = new MessageState()
            {
                MessageTemplate = null,
                MessageArguments = new object[] { true },
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                TimeStamp = timestamp
            };

            json = formatter.FormatMessage(state);
            Assert.Equal(string.Empty, doc.RootElement.GetProperty("message").GetString());
            Assert.Equal("1234", doc.RootElement.GetProperty("requestId").GetString());
        }

        [Fact]
        public void FormatJsonWithAllPossibleTypes()
        {
            var dateOnly = new DateOnly(2024, 2, 18);
            var timeOnly = new TimeOnly(15, 19, 45, 545);
            var timestamp = DateTime.UtcNow;
            var formattedTimestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

            var product = new Product() { Name = "Widget", Inventory = 100 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "bool: {bool}, byte: {byte}, char: {char}, decimal: {decimal}, double: {double}, float: {float}, " + 
                                  "int: {int}, uint: {uint}, long: {long}, ulong: {ulong}, short: {short}, ushort: {ushort}, null: {null}, " + 
                                  "DateTime: {DateTime}, DateTimeOffset: {DateTimeOffset}, default: {default}, DateOnly {DateOnly}, TimeOnly: {TimeOnly}",
                MessageArguments = new object[] {true, (byte)1, 'a', (decimal)4.5, (double)5.6, (float)7.7, (int)-10, (uint)10, (long)-100, (ulong)100, (short)-50, (ushort)50, null, timestamp, timestamp, product, dateOnly, timeOnly},
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("bool: True, byte: 1, char: a, decimal: 4.5, double: 5.6, float: 7.7, " +
                        "int: -10, uint: 10, long: -100, ulong: 100, short: -50, ushort: 50, null: {null}, " +
                        $"DateTime: {formattedTimestamp}, DateTimeOffset: {formattedTimestamp}, default: Widget 100, DateOnly 2024-02-18, TimeOnly: 15:19:45.545", 
                        doc.RootElement.GetProperty("message").GetString());

            Assert.True(doc.RootElement.GetProperty("bool").GetBoolean());
            Assert.Equal((byte)1, doc.RootElement.GetProperty("byte").GetByte());
            Assert.Equal("a", doc.RootElement.GetProperty("char").GetString());
            Assert.Equal((decimal)4.5, doc.RootElement.GetProperty("decimal").GetDecimal());
            Assert.Equal((double)5.6, doc.RootElement.GetProperty("double").GetDouble());
            Assert.Equal((float)7.7, doc.RootElement.GetProperty("float").GetSingle());
            Assert.Equal((int)-10, doc.RootElement.GetProperty("int").GetInt32());
            Assert.Equal((uint)10, doc.RootElement.GetProperty("uint").GetUInt32());
            Assert.Equal((long)-100, doc.RootElement.GetProperty("long").GetInt64());
            Assert.Equal((ulong)100, doc.RootElement.GetProperty("ulong").GetUInt64());
            Assert.Equal((short)-50, doc.RootElement.GetProperty("short").GetInt16());
            Assert.Equal((ushort)50, doc.RootElement.GetProperty("ushort").GetUInt16());
            Assert.False(doc.RootElement.TryGetProperty("null", out var _));
            Assert.Equal(formattedTimestamp, doc.RootElement.GetProperty("DateTime").GetString());
            Assert.Equal(formattedTimestamp, doc.RootElement.GetProperty("DateTimeOffset").GetString());
            Assert.Equal("Widget 100", doc.RootElement.GetProperty("default").GetString());
            Assert.Equal("2024-02-18", doc.RootElement.GetProperty("DateOnly").GetString());
            Assert.Equal("15:19:45.545", doc.RootElement.GetProperty("TimeOnly").GetString());
        }

        [Fact]
        public void FormatJsonWithPoco()
        {
            var timestamp = DateTime.UtcNow;

            var product = new Product() { Name = "Widget", Inventory = 100 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Our product is {product}",
                MessageArguments = new object[] { product },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Our product is Widget 100", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("product").ValueKind);
            Assert.Equal("Widget 100", doc.RootElement.GetProperty("product").GetString());
        }

        [Fact]
        public void FormatJsonWithPocoSerialized()
        {
            var timestamp = DateTime.UtcNow;

            var product = new Product() { Name = "Widget", Inventory = 100 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Our product is {@product}",
                MessageArguments = new object[] { product },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Our product is Widget 100", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("product").ValueKind);
            Assert.Equal("Widget", doc.RootElement.GetProperty("product").GetProperty("Name").GetString());
            Assert.Equal(100, doc.RootElement.GetProperty("product").GetProperty("Inventory").GetInt32());
        }

        [Fact]
        public void FormatJsonWithList()
        {
            var timestamp = DateTime.UtcNow;

            var product1 = new Product() { Name = "Widget", Inventory = 100 };
            var product2 = new Product() { Name = "Doohickey", Inventory = 200 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Our products are {products}",
                MessageArguments = new object[] { new Product[] { product1, product2 } },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Our products are {products}", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("products").ValueKind);
            Assert.Equal("Widget 100", doc.RootElement.GetProperty("products")[0].GetString());
            Assert.Equal("Doohickey 200", doc.RootElement.GetProperty("products")[1].GetString());
        }

        [Fact]
        public void FormatJsonWithListSerialzied()
        {
            var timestamp = DateTime.UtcNow;

            var product1 = new Product() { Name = "Widget", Inventory = 100 };
            var product2 = new Product() { Name = "Doohickey", Inventory = 200 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Our products are {@products}",
                MessageArguments = new object[] { new Product[] { product1, product2 } },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Our products are {@products}", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("products").ValueKind);
            Assert.Equal("Widget", doc.RootElement.GetProperty("products")[0].GetProperty("Name").GetString());
            Assert.Equal(100, doc.RootElement.GetProperty("products")[0].GetProperty("Inventory").GetInt32());

            Assert.Equal("Doohickey", doc.RootElement.GetProperty("products")[1].GetProperty("Name").GetString());
            Assert.Equal(200, doc.RootElement.GetProperty("products")[1].GetProperty("Inventory").GetInt32());
        }

        [Fact]
        public void FormatJsonWithDictionary()
        {
            var timestamp = DateTime.UtcNow;

            var product1 = new Product() { Name = "Widget", Inventory = 100 };
            var product2 = new Product() { Name = "Doohickey", Inventory = 200 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Our products are {products}",
                MessageArguments = new object[] { new Dictionary<string, Product> { { product1.Name, product1 }, { product2.Name, product2 } } },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Our products are {products}", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("products").ValueKind);
            Assert.Equal("Widget 100", doc.RootElement.GetProperty("products").GetProperty("Widget").GetString());
            Assert.Equal("Doohickey 200", doc.RootElement.GetProperty("products").GetProperty("Doohickey").GetString());
        }

        [Fact]
        public void FormatJsonWithDictionarySerialized()
        {
            var timestamp = DateTime.UtcNow;

            var product1 = new Product() { Name = "Widget", Inventory = 100 };
            var product2 = new Product() { Name = "Doohickey", Inventory = 200 };

            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "Our products are {@products}",
                MessageArguments = new object[] { new Dictionary<string, Product> { { product1.Name, product1 }, { product2.Name, product2 } } },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("Our products are {@products}", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("products").ValueKind);
            Assert.Equal("Widget", doc.RootElement.GetProperty("products").GetProperty("Widget").GetProperty("Name").GetString());
            Assert.Equal(100, doc.RootElement.GetProperty("products").GetProperty("Widget").GetProperty("Inventory").GetInt32());
            Assert.Equal("Doohickey", doc.RootElement.GetProperty("products").GetProperty("Doohickey").GetProperty("Name").GetString());
            Assert.Equal(200, doc.RootElement.GetProperty("products").GetProperty("Doohickey").GetProperty("Inventory").GetInt32());
        }

        [Theory]
        [InlineData("No Properties", new object[] { }, "No Properties")]
        [InlineData("No Properties", null, "No Properties")]
        [InlineData("{name}", new object[] { "aws" } , "aws")]
        [InlineData("{0}", new object[] { "aws" }, "aws")]
        [InlineData("{0} {1}", new object[] { "aws" }, "aws {1}")]
        [InlineData("{0}", new object[] { "aws", "s3" }, "aws")]
        [InlineData("{{name}} {name}", new object[] { "aws" }, "{{name}} aws")]
        [InlineData("Positional test {0} {1} {0}", new object[] {"Arg1", "Arg2" }, "Positional test Arg1 Arg2 Arg1")]
        [InlineData("{category}", new object[] { Product.Category.Electronics }, "Electronics")]
        [InlineData("{hextest}", new object[] { new byte[] { 1, 2, 3 } }, "010203")]
        [InlineData("{}", new object[] { "dummy" }, "{}")] // Log message doesn't really make any sense but we need to make sure we protect ourselves from it.
        public void PropertyReplacementVerification(string message, object[] arguments, string expectedMessage)
        {
            foreach(var formatter in new AbstractLogMessageFormatter[] { new DefaultLogMessageFormatter(false), new DefaultLogMessageFormatter(true), new JsonLogMessageFormatter() })
            {
                var timestamp = DateTime.UtcNow;

                var state = new MessageState()
                {
                    AwsRequestId = "1234",
                    Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                    MessageTemplate = message,
                    MessageArguments = arguments,
                    TimeStamp = timestamp
                };

                var formattedMessage = formatter.FormatMessage(state);

                if(formatter is JsonLogMessageFormatter)
                {
                    var doc = JsonDocument.Parse(formattedMessage);
                    Assert.Equal(expectedMessage, doc.RootElement.GetProperty("message").GetString());
                }
                else if(formatter is DefaultLogMessageFormatter defaultFormatter)
                {
                    if(defaultFormatter.AddPrefix)
                    {
                        Assert.EndsWith(expectedMessage, formattedMessage);
                    }
                    else
                    {
                        Assert.Equal(expectedMessage, formattedMessage);
                    }
                }
            }

            if (arguments?.Length == 1 && arguments[0] is byte[] bytes)
            {
                // Can't create ReadOnlyMemory and Memory as part of the "InlineData" attribute so force check
                // if we are doing the byte[] check and if so repeat for ReadonlyMemory and Memory
                PropertyReplacementVerification(message, new object[] { new ReadOnlyMemory<byte>(bytes) }, expectedMessage);
                PropertyReplacementVerification(message, new object[] { new Memory<byte>(bytes) }, expectedMessage);
            }
        }

        [Fact]
        public void OutOfOrderPositionalArgumentsWithJson()
        {
            var timestamp = DateTime.UtcNow;


            var formatter = new JsonLogMessageFormatter();
            var state = new MessageState()
            {
                AwsRequestId = "1234",
                Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                MessageTemplate = "{1} {0}",
                MessageArguments = new object[] { "arg1", "arg2" },
                TimeStamp = timestamp
            };

            var json = formatter.FormatMessage(state);
            var doc = JsonDocument.Parse(json);

            Assert.Equal("arg2 arg1", doc.RootElement.GetProperty("message").GetString());

            Assert.Equal("arg1", doc.RootElement.GetProperty("0").GetString());
            Assert.Equal("arg2", doc.RootElement.GetProperty("1").GetString()); ;

        }

        [Fact]
        public void FormatLargeByteArray()
        {
            var timestamp = DateTime.UtcNow;

            var bytes = new byte[10000];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = 1;
            }

            foreach (var argument in new object [] { bytes, new ReadOnlyMemory<byte>(bytes), new Memory<byte>(bytes) })
            {


                var state = new MessageState()
                {
                    AwsRequestId = "1234",
                    Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                    MessageTemplate = "Binary data: {bytes}",
                    MessageArguments = new object[] { argument },
                    TimeStamp = timestamp
                };

                var formatter = new JsonLogMessageFormatter();
                var json = formatter.FormatMessage(state);
                var doc = JsonDocument.Parse(json);

                Assert.Equal($"Binary data: 01010101010101010101010101010101... ({bytes.Length} bytes)", doc.RootElement.GetProperty("message").GetString());

                Assert.Equal($"01010101010101010101010101010101... ({bytes.Length} bytes)", doc.RootElement.GetProperty("bytes").GetString());


            }
        }

        [Fact]
        public void FormatJsonException()
        {
            try
            {
                var nre = new NullReferenceException("You got a null");
                throw new ApplicationException("This Will Fail", nre);
            }
            catch(Exception ex)
            {
                var timestamp = DateTime.UtcNow;

                var state = new MessageState()
                {
                    AwsRequestId = "1234",
                    Level = Helpers.LogLevelLoggerWriter.LogLevel.Warning,
                    MessageTemplate = "What does an error look like?",
                    MessageArguments = null,
                    Exception = ex,
                    TimeStamp = timestamp
                };

                var formatter = new JsonLogMessageFormatter();
                var json = formatter.FormatMessage(state);
                var doc = JsonDocument.Parse(json);

                Assert.Equal("What does an error look like?", doc.RootElement.GetProperty("message").GetString());
                Assert.Equal("This Will Fail", doc.RootElement.GetProperty("errorMessage").GetString());
                Assert.Equal("System.ApplicationException", doc.RootElement.GetProperty("errorType").GetString());
                Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("stackTrace").ValueKind);

                var stackLines = ex.ToString().Split('\n').Select(x => x.Trim()).ToList();
                var jsonExArray = doc.RootElement.GetProperty("stackTrace");
                Assert.Equal(stackLines.Count, jsonExArray.GetArrayLength());
                for(var i =  0; i < stackLines.Count; i++)
                {
                    Assert.Equal(stackLines[i], jsonExArray[i].GetString());
                }
            }
        }

        [Theory]
        [InlineData("{0}", true)]
        [InlineData("{1} {0}", true)]
        [InlineData("{1} {10}", false)]
        [InlineData("{1} {2}", false)]
        [InlineData("{0} {5} {6}", false)]
        [InlineData("Arg1 {0} Arg2 {1}", true)]
        [InlineData("Test {user}", false)]
        [InlineData("Arg1 {0} Arg5 {5}", false)] // Not positional because there is a gap in between the numbers
        [InlineData("Arg1 {@0} Arg2 {1:00}", true)]
        public void CheckIfPositional(string message, bool expected)
        {
            var formatter = new DefaultLogMessageFormatter(true);
            var properties = formatter.ParseProperties(message);
            var isPositional = formatter.UsingPositionalArguments(properties);
            Assert.Equal(expected, isPositional);
        }

        public class Product
        {
            public enum Category { Food, Electronics }
            public string Name { get; set; }

            public int Inventory { get; set; }

            public Category? Cat { get; set; }

            public override string ToString()
            {
                return $"{Name} {Inventory}";
            }
        }
    }
}
