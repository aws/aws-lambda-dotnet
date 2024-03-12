using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.DynamoDBEvents;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using static Amazon.Lambda.DynamoDBEvents.DynamoDBEvent;

namespace Amazon.Lambda.Tests
{
    /// <summary>
    /// Tests converting <see cref="DynamoDBEvent"/> to JSON and AWS SDK types
    /// </summary>
    public class DynamoDBEventTests
    {
        /// <summary>
        /// Internal helper that prepares a Lambda DynamoDB event containing a given DynamoDB item
        /// </summary>
        private DynamoDBEvent PrepareEvent(Dictionary<string, AttributeValue> attributes)
        {
            return new DynamoDBEvent
            {
                Records = new List<DynamodbStreamRecord>
                {
                    new DynamodbStreamRecord
                    {
                        Dynamodb = new StreamRecord()
                        {
                            NewImage = attributes
                        }
                    }
                }
            };
        }

        [Fact]
        public void String_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Message", new AttributeValue {S = "This is a string" } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Message\":\"This is a string\"}", json);
        }

        [Fact]
        public void String_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Message", new AttributeValue {S = "This is a string" } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.NotNull(document["Message"]);
            Assert.Equal("This is a string", document["Message"].AsString());
        }

        [Fact]
        public void Number_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Integer", new AttributeValue {N = "123" } },
                { "Double", new AttributeValue {N = "123.45" } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Integer\":123,\"Double\":123.45}", json);
        }

        [Fact]
        public void Number_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Integer", new AttributeValue {N = "123" } },
                { "Double", new AttributeValue {N = "123.45" } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.Equal(123, document["Integer"].AsInt());
            Assert.Equal(123.45, document["Double"].AsDouble());
        }

        [Fact]
        public void Binary_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Binary", new AttributeValue {B = new MemoryStream(Encoding.UTF8.GetBytes("hello world")) } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Binary\":\"aGVsbG8gd29ybGQ=\"}", json);
        }

        [Fact]
        public void Binary_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Binary", new AttributeValue {B = new MemoryStream(Encoding.UTF8.GetBytes("hello world"))  } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            // Must opt in to binary decoding, since we can't distinguish from strings
            document.DecodeBase64Attributes("Binary");

            Assert.Equal("hello world", Encoding.UTF8.GetString(document["Binary"].AsByteArray()));
        }

        [Fact]
        public void Bool_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "False", new AttributeValue {BOOL = false } },
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"False\":false}", json);
        }

        [Fact]
        public void Bool_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "False", new AttributeValue {BOOL = false } },
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.False(document["False"].AsBoolean());
        }

        [Fact]
        public void Null_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Null", new AttributeValue {NULL = true } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Null\":null}", json);
        }

        [Fact]
        public void Null_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Null", new AttributeValue {NULL = true } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.Equal(DynamoDBNull.Null, document["Null"].AsDynamoDBNull());
        }

        [Fact]
        public void Map_ToJson()
        {
            var map = new Dictionary<string, AttributeValue>
            {
                { "string", new AttributeValue {S = "string"} },
                { "number", new AttributeValue {N = "123.45"} },
                { "boolean", new AttributeValue {BOOL = false} }
            };

            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>
            {
                { "Map", new AttributeValue { M = map } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Map\":{\"string\":\"string\",\"number\":123.45,\"boolean\":false}}", json);
        }

        [Fact]
        public void Map_ToDocument()
        {
            var map = new Dictionary<string, AttributeValue>
            {
                { "string", new AttributeValue {S = "string"} },
                { "number", new AttributeValue {N = "123.45"} },
                { "boolean", new AttributeValue {BOOL = false} }
            };

            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>
            {
                { "Map", new AttributeValue { M = map } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.NotNull(document["Map"]);
            Assert.NotNull(document["Map"].AsDocument());
            Assert.Equal("string", document["Map"].AsDocument()["string"].AsString());
            Assert.Equal(123.45, document["Map"].AsDocument()["number"].AsDouble());
            Assert.Equal(false, document["Map"].AsDocument()["boolean"].AsBoolean());
        }

        [Fact]
        public void EmptyMap_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>
            {
                { "Map", new AttributeValue { M = new Dictionary<string, AttributeValue>() } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Map\":{}}", json);
        }

        [Fact]
        public void EmptyMap_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>
            {
                { "Map", new AttributeValue { M = new Dictionary<string, AttributeValue>() } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.NotNull(document["Map"]);
            Assert.NotNull(document["Map"].AsDocument());
        }

        [Fact]
        public void List_ToJson()
        {
            var list = new List<AttributeValue>
            {
                new AttributeValue { S = "string"},
                new AttributeValue { N = "123"},
                new AttributeValue { BOOL = false}
            };

            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "List", new AttributeValue { L = list } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"List\":[\"string\",123,false]}", json);
        }

        [Fact]
        public void List_ToDocument()
        {
            var list = new List<AttributeValue>
            {
                new AttributeValue { S = "string"},
                new AttributeValue { N = "123"},
                new AttributeValue { BOOL = false}
            };

            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "List", new AttributeValue { L = list } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.NotNull(document["List"].AsDynamoDBList());
            Assert.Equal("string", document["List"].AsDynamoDBList()[0].AsString());
            Assert.Equal(123, document["List"].AsDynamoDBList()[1].AsInt());
            Assert.False(document["List"].AsDynamoDBList()[2].AsBoolean());
        }

        [Fact]
        public void EmptyList_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "List", new AttributeValue { L = new List<AttributeValue>() } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"List\":[]}", json);
        }

        [Fact]
        public void EmptyList_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "List", new AttributeValue { L = new List<AttributeValue>() } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.NotNull(document["List"].AsDynamoDBList());
            Assert.Empty(document["List"].AsDynamoDBList().AsArrayOfDynamoDBEntry());
        }

        [Fact]
        public void StringSet_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "StringSet", new AttributeValue { SS = new List<string> { "Black", "Green", "Red" }}}
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"StringSet\":[\"Black\",\"Green\",\"Red\"]}", json);
        }

        [Fact]
        public void StringSet_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "StringSet", new AttributeValue { SS = new List<string> { "Black", "Green", "Red" }}}
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            var hashSet = document["StringSet"].AsHashSetOfString();
            Assert.NotNull(hashSet);
            Assert.Equal(3, hashSet.Count);
            Assert.True(hashSet.Contains("Black"));
            Assert.True(hashSet.Contains("Green"));
            Assert.True(hashSet.Contains("Red"));
        }

        [Fact]
        public void NumberSet_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "NumberSet", new AttributeValue { NS = new List<string> { "123", "123.45", "-123.45" } } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"NumberSet\":[123,123.45,-123.45]}", json);
        }

        [Fact]
        public void NumberSet_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "NumberSet", new AttributeValue { NS = new List<string> { "123", "123.45", "-123.45" } } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            var list = document["NumberSet"].AsListOfDynamoDBEntry();
            Assert.NotNull(list);
            Assert.Equal(3, list.Count);
            Assert.Equal(123, list[0].AsInt());
            Assert.Equal(123.45, list[1].AsDouble());
            Assert.Equal(-123.45, list[2].AsDouble());
        }

        [Fact]
        public void BinarySet_ToJson()
        {
            var set = new List<MemoryStream>
            {
                new MemoryStream(Encoding.UTF8.GetBytes("hello world")),
                new MemoryStream(Encoding.UTF8.GetBytes("hello world!"))
            };

            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "BinarySet", new AttributeValue { BS = set } }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"BinarySet\":[\"aGVsbG8gd29ybGQ=\",\"aGVsbG8gd29ybGQh\"]}", json);
        }

        [Fact]
        public void BinarySet_ToDocument()
        {
            var set = new List<MemoryStream> 
            {
                new MemoryStream(Encoding.UTF8.GetBytes("hello world")),
                new MemoryStream(Encoding.UTF8.GetBytes("hello world!"))
            };

            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "BinarySet", new AttributeValue { BS = set } }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            document.DecodeBase64Attributes("BinarySet");

            var list = document["BinarySet"].AsListOfDynamoDBEntry();
            Assert.NotNull(list);

            Assert.Equal(2, list.Count);
            Assert.Equal("hello world", Encoding.UTF8.GetString(list[0].AsByteArray()));
            Assert.Equal("hello world!", Encoding.UTF8.GetString(list[1].AsByteArray()));
        }

        [Fact]
        public void NullAttributes_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Null", new AttributeValue {NULL = null } },
                { "Empty", new AttributeValue() }
            });

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{\"Null\":null,\"Empty\":null}", json);
        }

        [Fact]
        public void NullAttributes_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>()
            {
                { "Null", new AttributeValue {NULL = null } },
                { "Empty", new AttributeValue() }
            });

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.Equal(DynamoDBNull.Null, document["Null"].AsDynamoDBNull());
            Assert.Equal(DynamoDBNull.Null, document["Empty"].AsDynamoDBNull());
        }

        [Fact]
        public void NoAttributes_ToJson()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>());

            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();

            Assert.Equal("{}", json);
        }

        [Fact]
        public void NoAttributes_ToDocument()
        {
            var evnt = PrepareEvent(new Dictionary<string, AttributeValue>());

            // Convert the event from the Lambda package to the SDK type
            var json = evnt.Records[0].Dynamodb.NewImage.ToJson();
            var document = Document.FromJson(json);

            Assert.Equal(0, document.Count);
        }
    }
}
