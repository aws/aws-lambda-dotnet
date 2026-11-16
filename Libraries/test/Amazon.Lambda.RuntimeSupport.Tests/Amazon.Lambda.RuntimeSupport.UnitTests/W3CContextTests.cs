// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace Amazon.Lambda.RuntimeSupport.UnitTests
{

    public class W3CContextTests
    {
        private readonly TestEnvironmentVariables _environmentVariables = new TestEnvironmentVariables();

        private LambdaContext BuildContext(string clientContextJson)
        {
            var deadlineMs = DateTimeOffset.UtcNow.AddHours(1)
                .ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            var headers = new Dictionary<string, IEnumerable<string>>
            {
                ["Lambda-Runtime-Aws-Request-Id"] = new[] { Guid.NewGuid().ToString() },
                ["Lambda-Runtime-Deadline-Ms"] = new[] { deadlineMs }
            };
            if (clientContextJson != null)
            {
                headers["Lambda-Runtime-Client-Context"] = new[] { clientContextJson };
            }

            var runtimeApiHeaders = new RuntimeApiHeaders(headers);
            var env = new LambdaEnvironment(_environmentVariables);
            return new LambdaContext(runtimeApiHeaders, env,
                new Helpers.LogLevelLoggerWriter(new SystemEnvironmentVariables()));
        }

        [Fact]
        public void W3C_ReturnsEmpty_WhenClientContextHeaderAbsent()
        {
            var context = BuildContext(clientContextJson: null);
            Assert.Empty(context.W3C);
        }

        [Fact]
        public void W3C_ReturnsEmpty_WhenClientContextHasNoW3CKey()
        {
            var context = BuildContext("{\"custom\":{\"value\":\"test\"}}");
            Assert.Empty(context.W3C);
            // ClientContext is still populated — w3c is just not there.
            Assert.NotNull(context.ClientContext);
            Assert.Equal("test", context.ClientContext.Custom["value"]);
        }

        [Fact]
        public void W3C_ReturnsBaggageOnly()
        {
            var context = BuildContext("{\"w3c\":{\"baggage\":\"userId=alice\"}}");
            var w3c = context.W3C;
            Assert.Single(w3c);
            Assert.Equal("userId=alice", w3c["baggage"]);
        }

        [Fact]
        public void W3C_ReturnsAllThreeAllowlistedFields()
        {
            const string json = @"{
                ""custom"": { ""value"": ""test"" },
                ""w3c"": {
                    ""traceparent"": ""00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"",
                    ""tracestate"": ""rojo=00f067aa0ba902b7"",
                    ""baggage"": ""userId=alice""
                }
            }";
            var context = BuildContext(json);
            var w3c = context.W3C;
            Assert.Equal(3, w3c.Count);
            Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", w3c["traceparent"]);
            Assert.Equal("rojo=00f067aa0ba902b7", w3c["tracestate"]);
            Assert.Equal("userId=alice", w3c["baggage"]);
        }

        [Fact]
        public void W3C_StripsSource_WhenReadFromClientContext()
        {
            const string json = @"{
                ""custom"": { ""value"": ""test"" },
                ""w3c"": {
                    ""traceparent"": ""00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"",
                    ""baggage"": ""userId=alice""
                }
            }";
            var context = BuildContext(json);

            // W3C fields are surfaced through W3C
            Assert.Equal(2, context.W3C.Count);
            // but IClientContext has no way to read them back
            Assert.NotNull(context.ClientContext);
            Assert.Null(context.ClientContext.Environment);
            Assert.Null(context.ClientContext.Client);
            Assert.NotNull(context.ClientContext.Custom);
            Assert.Equal("test", context.ClientContext.Custom["value"]);
            Assert.False(context.ClientContext.Custom.ContainsKey("w3c"));
            Assert.False(context.ClientContext.Custom.ContainsKey("traceparent"));
        }

        [Fact]
        public void W3C_DropsAllowlistedFields_WithNonStringValues()
        {
            const string json = @"{
                ""w3c"": {
                    ""baggage"": ""abc"",
                    ""traceparent"": 42,
                    ""tracestate"": null
                }
            }";
            var context = BuildContext(json);
            var w3c = context.W3C;
            Assert.Single(w3c);
            Assert.Equal("abc", w3c["baggage"]);
        }

        [Fact]
        public void W3C_TreatsNonObjectValueAsEmpty()
        {
            var context = BuildContext("{\"w3c\":\"not-an-object\"}");
            Assert.Empty(context.W3C);
        }

        [Fact]
        public void W3C_TreatsArrayValueAsEmpty()
        {
            var context = BuildContext("{\"w3c\":[\"baggage=abc\"]}");
            Assert.Empty(context.W3C);
        }

        [Fact]
        public void W3C_ResultIsReadOnly()
        {
            var context = BuildContext("{\"w3c\":{\"baggage\":\"abc\"}}");
            var w3c = context.W3C;

            Assert.IsType<System.Collections.ObjectModel.ReadOnlyDictionary<string, string>>(w3c);

            var mutable = (ICollection<KeyValuePair<string, string>>)w3c;
            Assert.True(mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() =>
                mutable.Add(new KeyValuePair<string, string>("baggage", "tampered")));

            Assert.Equal("abc", context.W3C["baggage"]);
        }

        [Fact]
        public void W3C_AllowlistDrops_NonAllowlistedKeys_EvenWhenValueIsString()
        {
            const string json = @"{
                ""w3c"": {
                    ""baggage"": ""keep=me"",
                    ""unknownField"": ""should-not-appear"",
                    ""x-custom-trace"": ""should-not-appear""
                }
            }";
            var context = BuildContext(json);
            var w3c = context.W3C;
            Assert.Single(w3c);
            Assert.Equal("keep=me", w3c["baggage"]);
            Assert.False(w3c.ContainsKey("unknownField"));
        }

        [Fact]
        public void W3C_OmitsAbsentAllowlistedKeys()
        {
            var context = BuildContext("{\"w3c\":{\"baggage\":\"abc\"}}");
            var w3c = context.W3C;
            Assert.True(w3c.ContainsKey("baggage"));
            Assert.False(w3c.ContainsKey("traceparent"));
            Assert.False(w3c.ContainsKey("tracestate"));
        }

        [Fact]
        public void W3C_ReturnsEmpty_WhenAllAllowlistedValuesAreNonStrings()
        {
            const string json = @"{
                ""w3c"": {
                    ""traceparent"": 42,
                    ""tracestate"": null,
                    ""baggage"": { ""nested"": ""no"" }
                }
            }";
            var context = BuildContext(json);
            Assert.Empty(context.W3C);
        }

        [Fact]
        public void W3C_ReturnsEmpty_WhenClientContextJsonIsMalformed()
        {
            Assert.Empty(W3CTraceContext.FromClientContextJson("{ not valid json"));
        }
    }
}
