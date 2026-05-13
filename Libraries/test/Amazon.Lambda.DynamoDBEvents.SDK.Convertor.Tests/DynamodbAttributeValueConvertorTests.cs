namespace Amazon.Lambda.DynamoDBEvents.SDK.Convertor.Tests
{
    public class DynamodbAttributeValueConvertorTests
    {
        [Fact]
        public void ConvertToSdkAttribute_StringValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { S = "TestString" };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal("TestString", sdkAttribute.S);
        }

        [Fact]
        public void ConvertToSdkAttribute_EmptyStringValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { S = string.Empty };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal(string.Empty, sdkAttribute.S);
        }

        [Fact]
        public void ConvertToSdkAttribute_NumberValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { N = "123" };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal("123", sdkAttribute.N);
        }

        [Fact]
        public void ConvertToSdkAttribute_BoolValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { BOOL = true };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.True(sdkAttribute.BOOL);
        }

        [Fact]
        public void ConvertToSdkAttribute_NullValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { NULL = true };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.True(sdkAttribute.NULL);
        }

        [Fact]
        public void ConvertToSdkAttribute_StringSetValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { SS = new List<string> { "A", "B" } };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal(new List<string> { "A", "B" }, sdkAttribute.SS);
        }

        [Fact]
        public void ConvertToSdkAttribute_NumberSetValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue { NS = new List<string> { "1", "2" } };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal(new List<string> { "1", "2" }, sdkAttribute.NS);
        }

        [Fact]
        public void ConvertToSdkAttribute_ListValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue
            {
                L = new List<DynamoDBEvent.AttributeValue>
                {
                    new() { S = "Item1" },
                    new() { N = "2" }
                }
            };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal(2, sdkAttribute.L.Count);
            Assert.Equal("Item1", sdkAttribute.L[0].S);
            Assert.Equal("2", sdkAttribute.L[1].N);
        }

        [Fact]
        public void ConvertToSdkAttribute_MapValue_ReturnsSdkAttribute()
        {
            var lambdaAttribute = new DynamoDBEvent.AttributeValue
            {
                M = new Dictionary<string, DynamoDBEvent.AttributeValue>
                {
                    { "Key1", new DynamoDBEvent.AttributeValue { S = "Value1" } },
                    { "Key2", new DynamoDBEvent.AttributeValue { N = "2" } }
                }
            };
            var sdkAttribute = lambdaAttribute.ConvertToSdkAttribute();

            Assert.NotNull(sdkAttribute);
            Assert.Equal(2, sdkAttribute.M.Count);
            Assert.Equal("Value1", sdkAttribute.M["Key1"].S);
            Assert.Equal("2", sdkAttribute.M["Key2"].N);
        }

        [Fact]
        public void ConvertToSdkAttributeValueDictionary_ValidDictionary_ReturnsSdkDictionary()
        {
            var lambdaAttributes = new Dictionary<string, DynamoDBEvent.AttributeValue>
            {
                { "Key1", new DynamoDBEvent.AttributeValue { S = "Value1" } },
                { "Key2", new DynamoDBEvent.AttributeValue { N = "2" } }
            };
            var sdkDictionary = lambdaAttributes.ConvertToSdkAttributeValueDictionary();

            Assert.NotNull(sdkDictionary);
            Assert.Equal(2, sdkDictionary.Count);
            Assert.Equal("Value1", sdkDictionary["Key1"].S);
            Assert.Equal("2", sdkDictionary["Key2"].N);
        }

        [Fact]
        public void ConvertToSdkAttributeValueDictionary_NullDictionary_ReturnsEmptySdkDictionary()
        {
            Dictionary<string, DynamoDBEvent.AttributeValue> lambdaAttributes = null!;
            var sdkDictionary = lambdaAttributes.ConvertToSdkAttributeValueDictionary();

            Assert.NotNull(sdkDictionary);
            Assert.Empty(sdkDictionary);
        }
    }
}
