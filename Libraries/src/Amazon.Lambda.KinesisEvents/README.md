# Amazon.Lambda.KinesisEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Kinesis events.

This package has a dependency on the [AWS SDK for .NET package AWSSDK.Kinesis](https://www.nuget.org/packages/AWSSDK.Kinesis/) in order to use the `Amazon.Kinesis.Model.Record` type. 

# Serialization

If you are using this package with Amazon Lambda but are not also using `Amazon.Lambda.Serialization.Json`, be aware that one property requires custom serialization.

This property is `Data` on the type `Amazon.Lambda.KinesisEvents.KinesisEvent+Record`. This is a `MemoryStream` object that should be populated by converting the JSON string from base64 to an array of bytes, then constructing a `MemoryStream` object from these bytes. Here is a code sample showing this deserialization logic.
```
string dataBase64 = GetJsonString();
byte[] dataBytes = Convert.FromBase64String(dataBase64);
MemoryStream stream = new MemoryStream(dataBytes);
```

A Newtonsoft.Json `IContractResolver` implementation which handles this custom serialization is located in [Amazon.Lambda.Serialization.Json\AwsResolver.cs](Libraries/src/Amazon.Lambda.Serialization.Json/AwsResolver.cs) and [Amazon.Lambda.Serialization.Json\KinesisEventRecordDataConverter.cs](Libraries/src/Amazon.Lambda.Serialization.Json/KinesisEventRecordDataConverter.cs), consult this source for more information.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Kinesis event record data as an input and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public string Handler(KinesisEvent kinesisEvent)
    {
        foreach (var record in kinesisEvent.Records)
        {
            var kinesisRecord = record.Kinesis;
            var dataBytes = kinesisRecord.Data.ToArray();
            var dataText = Encoding.UTF8.GetString(dataBytes);
            Console.WriteLine($"[{record.EventName}] Data = '{dataText}'.");
        }
    }
}
```
