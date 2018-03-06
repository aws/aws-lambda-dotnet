using System;
using Xunit;
using Amazon.Lambda.Serialization.Json;
using System.Collections.Generic;
using Newtonsoft.Json;
using AmazonJsonSerializer = Amazon.Lambda.Serialization.Json.JsonSerializer;

public class JsonSerializerTest
{
    [Fact]
    public void JsonSerializerDoesntThrowWithNullArg ()
    {
        IEnumerable<JsonConverter> converters = null;
        var ex = Record.Exception(() => new AmazonJsonSerializer(converters));
        // No exception in general and no NullReference in particular
        Assert.Null(ex);
    }
}