using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Xunit;

namespace Amazon.Lambda.DurableExecution.Tests;

/// <summary>
/// Direct tests for UpperSnakeCaseEnumConverter via a sample enum, exercising
/// every branch (Read with multi-word value, Read with single word, Read with
/// null/unparsable, plus the Write path for outbound serialization).
/// </summary>
public class UpperSnakeCaseEnumConverterTests
{
    public enum Sample
    {
        None,
        FooBar,
        BazQuxQuux
    }

    public class Holder
    {
        [JsonConverter(typeof(UpperSnakeCaseEnumConverter<Sample>))]
        public Sample Value { get; set; }
    }

    [Theory]
    [InlineData("\"FOO_BAR\"", Sample.FooBar)]
    [InlineData("\"BAZ_QUX_QUUX\"", Sample.BazQuxQuux)]
    [InlineData("\"NONE\"", Sample.None)]
    public void Read_UpperSnakeCase_ReturnsExpectedEnum(string json, Sample expected)
    {
        var holder = JsonSerializer.Deserialize<Holder>($"{{\"Value\":{json}}}")!;
        Assert.Equal(expected, holder.Value);
    }

    [Fact]
    public void Read_NullValue_ReturnsDefault()
    {
        var holder = JsonSerializer.Deserialize<Holder>("{\"Value\":null}")!;
        Assert.Equal(Sample.None, holder.Value);
    }

    [Fact]
    public void Read_CamelCase_ParsesCaseInsensitively()
    {
        // The converter first tries snake→pascal, then a raw case-insensitive parse.
        // A camel-case input like "fooBar" hits the fallback path.
        var holder = JsonSerializer.Deserialize<Holder>("{\"Value\":\"fooBar\"}")!;
        Assert.Equal(Sample.FooBar, holder.Value);
    }

    [Fact]
    public void Read_UnparsableValue_ThrowsJsonException()
    {
        // Unknown wire values must surface as JsonException rather than
        // silently coercing to default(T) — otherwise an unrecognized
        // service status would be indistinguishable from the zero value.
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Holder>("{\"Value\":\"NOT_A_REAL_VALUE\"}"));
    }

    [Fact]
    public void Write_PascalCase_EmitsUpperSnake()
    {
        var json = JsonSerializer.Serialize(new Holder { Value = Sample.FooBar });
        Assert.Contains("\"FOO_BAR\"", json);
    }

    [Fact]
    public void Write_MultiWord_EmitsUpperSnake()
    {
        var json = JsonSerializer.Serialize(new Holder { Value = Sample.BazQuxQuux });
        Assert.Contains("\"BAZ_QUX_QUUX\"", json);
    }

    [Fact]
    public void Write_SingleWord_EmitsUpperWithoutUnderscores()
    {
        var json = JsonSerializer.Serialize(new Holder { Value = Sample.None });
        Assert.Contains("\"NONE\"", json);
    }
}
