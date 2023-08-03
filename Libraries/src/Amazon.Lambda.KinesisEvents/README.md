# Amazon.Lambda.KinesisEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Kinesis events.

This package has a dependency on the [AWS SDK for .NET package AWSSDK.Kinesis](https://www.nuget.org/packages/AWSSDK.Kinesis/) in order to use the `Amazon.Kinesis.Model.Record` type. 

# Serialization

If you are using this package with Amazon Lambda but are not also using `Amazon.Lambda.Serialization.Json`, be aware that one property requires custom serialization.

This property is `Data` on the type `Amazon.Lambda.KinesisEvents.KinesisEvent+Record`. This is a `MemoryStream` object that should be populated by converting the JSON string from base64 to an array of bytes, then constructing a `MemoryStream` object from these bytes. Here is a code sample showing this deserialization logic.
```csharp
string dataBase64 = GetJsonString();
byte[] dataBytes = Convert.FromBase64String(dataBase64);
MemoryStream stream = new MemoryStream(dataBytes);
```

A Newtonsoft.Json `IContractResolver` implementation which handles this custom serialization is located in [Amazon.Lambda.Serialization.Json\AwsResolver.cs](../Amazon.Lambda.Serialization.Json/AwsResolver.cs), consult this source for more information.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Kinesis event record data as an input and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
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

The following is a sample class and Lambda function that receives Amazon Kinesis event record data as an input and uses `StreamsEventResponse` object to return batch item failures, if any. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public StreamsEventResponse Handler(KinesisEvent kinesisEvent)
    {
        var batchItemFailures = new List<StreamsEventResponse.BatchItemFailure>();
        string curRecordSequenceNumber = string.Empty;

        foreach (var record in kinesisEvent.Records)
        {
            try
            {
                //Process your record
                var kinesisRecord = record.Kinesis;
                curRecordSequenceNumber = kinesisRecord.SequenceNumber;
            }
            catch (Exception e)
            {
                /* Since we are working with streams, we can return the failed item immediately.
                   Lambda will immediately begin to retry processing from this failed item onwards. */
                batchItemFailures.Add(new StreamsEventResponse.BatchItemFailure() { ItemIdentifier = curRecordSequenceNumber });
                return new StreamsEventResponse() { BatchItemFailures = batchItemFailures };
            }
        }

        return new StreamsEventResponse() { BatchItemFailures = batchItemFailures };
    }
}
```

The following is a sample class and Lambda function that receives Amazon Kinesis event when using time windows as an input and uses `KinesisTimeWindowResponse` object to demonstrate how to aggregate and then process the final state. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public KinesisTimeWindowResponse Handler(KinesisTimeWindowEvent kinesisTimeWindowEvent)
    {
        Console.WriteLine($"Incoming event, Source Arn: {kinesisTimeWindowEvent.EventSourceARN}, Shard Id: {kinesisTimeWindowEvent.ShardId}");
        Console.WriteLine($"Incoming state: {string.Join(';', kinesisTimeWindowEvent.State.Select(s => s.Key + "=" + s.Value))}");

        //Check if this is the end of the window to either aggregate or process.
        if (kinesisTimeWindowEvent.IsFinalInvokeForWindow.HasValue && kinesisTimeWindowEvent.IsFinalInvokeForWindow.Value)
        {
            // Logic to handle final state of the window
            Console.WriteLine("Destination invoke");
        }
        else
        {
            Console.WriteLine("Aggregate invoke");
        }

        //Check for early terminations
        if (kinesisTimeWindowEvent.IsWindowTerminatedEarly.HasValue && kinesisTimeWindowEvent.IsWindowTerminatedEarly.Value)
        {
            Console.WriteLine("Window terminated early");
        }

        // Aggregation logic
        var state = kinesisTimeWindowEvent.State;
        foreach (var record in kinesisTimeWindowEvent.Records)
        {
            int id;
            if (!state.ContainsKey(record.Kinesis.PartitionKey) || int.TryParse(record.Kinesis.PartitionKey, out id))
            {
                state[record.Kinesis.PartitionKey] = "1";
            }
            else
            {
                state[record.Kinesis.PartitionKey] = (id + 1).ToString();
            }
        }

        Console.WriteLine($"Returning state: {string.Join(';', state.Select(s => s.Key + "=" + s.Value))}");
        return new KinesisTimeWindowResponse()
        {
            State = state
        };
    }
}
```
