# Amazon.Lambda.KafkaEvents

This package contains classes that can be used as input types for Lambda functions that process Apache Kafka events.

# Serialization

If you are using this package with Amazon Lambda but are not also using `Amazon.Lambda.Serialization.Json`, be aware that one property requires custom serialization.

This property is `Value` on the type `Amazon.Lambda.KafkaEvents.KafkaEvent+KafkaEventRecord`. This is a `MemoryStream` object that should be populated by converting the JSON string from base64 to an array of bytes, then constructing a `MemoryStream` object from these bytes. Here is a code sample showing this deserialization logic.
```csharp
string dataBase64 = GetJsonString();
byte[] dataBytes = Convert.FromBase64String(dataBase64);
MemoryStream stream = new MemoryStream(dataBytes);
```

A Newtonsoft.Json `IContractResolver` implementation which handles this custom serialization is located in [Amazon.Lambda.Serialization.Json\AwsResolver.cs](../Amazon.Lambda.Serialization.Json/AwsResolver.cs), consult this source for more information.

# Sample Function

The following is a sample class and Lambda function that receives Apache Kafka event record data as an input and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(KafkaEvent kafkaEvent)
    {
        foreach (var record in kafkaEvent.Records)
        {
            foreach (var eventRecord in record.Value)
	    {
	        var valueBytes = eventRecord.Value.ToArray();
                var valueText = Encoding.UTF8.GetString(valueBytes);
                Console.WriteLine($"[{record.Key}] Value = '{valueText}'.");
            }
        }
    }
}
```
