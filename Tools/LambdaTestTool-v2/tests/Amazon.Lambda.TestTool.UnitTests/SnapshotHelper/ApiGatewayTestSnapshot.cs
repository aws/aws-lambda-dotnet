using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Amazon.Lambda.TestTool.UnitTests.SnapshotHelper;

public class HttpResponseMessageConverter : JsonConverter<HttpResponseMessage>
{
    public override HttpResponseMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        var response = new HttpResponseMessage((HttpStatusCode)element.GetProperty("StatusCode").GetInt32());

        // Set content
        var content = element.GetProperty("Content").GetString();
        response.Content = new StringContent(content ?? "");

        // Clear default headers that StringContent adds
        response.Headers.Clear();
        response.Content.Headers.Clear();

        // Set headers
        if (element.TryGetProperty("Headers", out var headersElement))
        {
            foreach (var header in headersElement.EnumerateObject())
            {
                var values = header.Value.EnumerateArray()
                    .Select(v => v.GetString())
                    .Where(v => v != null)
                    .ToList();

                // Try to add to either Headers or Content.Headers
                if (!response.Headers.TryAddWithoutValidation(header.Name, values))
                {
                    response.Content.Headers.TryAddWithoutValidation(header.Name, values);
                }
            }
        }

        return response;
    }

    public override void Write(Utf8JsonWriter writer, HttpResponseMessage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write status code
        writer.WriteNumber("StatusCode", (int)value.StatusCode);

        // Write content
        writer.WriteString("Content", value.Content.ReadAsStringAsync().Result);

        // Write headers
        writer.WriteStartObject("Headers");
        foreach (var header in value.Headers.Concat(value.Content.Headers))
        {
            writer.WriteStartArray(header.Key);
            foreach (var headerValue in header.Value)
            {
                writer.WriteStringValue(headerValue);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }
}
