# Amazon.Lambda.DynamoDBEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon DynamoDB events.

# Sample Function

The following is a sample class and Lambda function that receives Amazon DynamoDB event record data as an input and writes some of the incoming event data to CloudWatch Logs. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public void Handler(DynamoDBEvent ddbEvent)
    {
        foreach (var record in ddbEvent.Records)
        {
            var ddbRecord = record.Dynamodb;
            var keys = string.Join(", ", ddbRecord.Keys.Keys);
            Console.WriteLine($"{record.EventID} - Keys = [{keys}], Size = {ddbRecord.SizeBytes} bytes");
        }
        Console.WriteLine($"Successfully processed {ddbEvent.Records.Count} records.");
    }
}
```

The following is a sample class and Lambda function that receives Amazon DynamoDB event record data as an input and uses `StreamsEventResponse` object to return batch item failures, if any. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public StreamsEventResponse Handler(DynamoDBEvent ddbEvent)
    {
        var batchItemFailures = new List<StreamsEventResponse.BatchItemFailure>();
        string curRecordSequenceNumber = string.Empty;

        foreach (var record in ddbEvent.Records)
        {
            try
            {
                //Process your record
                var ddbRecord = record.Dynamodb;
                var keys = string.Join(", ", ddbRecord.Keys.Keys);
                curRecordSequenceNumber = ddbRecord.SequenceNumber;
                Console.WriteLine($"{record.EventID} - Keys = [{keys}], Size = {ddbRecord.SizeBytes} bytes");
            }
            catch (Exception e)
            {
                //Return failed record's sequence number
                batchItemFailures.Add(new StreamsEventResponse.BatchItemFailure() { ItemIdentifier = curRecordSequenceNumber });
                return new StreamsEventResponse() { BatchItemFailures = batchItemFailures };
            }
        }
        Console.WriteLine($"Successfully processed {ddbEvent.Records.Count} records.");
        return new StreamsEventResponse() { BatchItemFailures = batchItemFailures };
    }
}
```

The following is a sample class and Lambda function that receives Amazon DynamoDB event when using time windows as an input and uses `DynamoDBTimeWindowResponse` object to demonstrate how to aggregate and then process the final state. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public DynamoDBTimeWindowResponse Handler(DynamoDBTimeWindowEvent ddbTimeWindowEvent)
    {
        Console.WriteLine($"Incoming event, Source Arn: {ddbTimeWindowEvent.EventSourceArn}, Shard Id: {ddbTimeWindowEvent.ShardId}");
        Console.WriteLine($"Incoming state: {string.Join(';', ddbTimeWindowEvent.State.Select(s => s.Key + "=" + s.Value))}");

        //Check if this is the end of the window to either aggregate or process.
        if (ddbTimeWindowEvent.IsFinalInvokeForWindow.HasValue && ddbTimeWindowEvent.IsFinalInvokeForWindow.Value)
        {
            // Logic to handle final state of the window
            Console.WriteLine("Destination invoke");
        }
        else
        {
            Console.WriteLine("Aggregate invoke");
        }

        //Check for early terminations
        if (ddbTimeWindowEvent.IsWindowTerminatedEarly.HasValue && ddbTimeWindowEvent.IsWindowTerminatedEarly.Value)
        {
            Console.WriteLine("Window terminated early");
        }

        // Aggregation logic
        var state = ddbTimeWindowEvent.State;
        foreach (var record in ddbTimeWindowEvent.Records)
        {
            int id;
            if (!state.ContainsKey(record.Dynamodb.NewImage["Id"].N) || int.TryParse(record.Dynamodb.NewImage["Id"].N, out id))
            {
                state[record.Dynamodb.NewImage["Id"].N] = "1";
            }
            else
            {
                state[record.Dynamodb.NewImage["Id"].N] = (id + 1).ToString();
            }
        }

        Console.WriteLine($"Returning state: {string.Join(';', state.Select(s => s.Key + "=" + s.Value))}");
        return new DynamoDBTimeWindowResponse()
        {
            State = state
        };
    }
}
```
