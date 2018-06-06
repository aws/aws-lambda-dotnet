# Amazon.Lambda.SQSEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Simple Queue Service (Amazon SQS) events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how an SQSEvent can be used. The function logs a summary of the events it received, including the event source, the timestamp, and the message of each event. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(SQSEvent sqsEvent)
    {
        foreach (var record in sqsEvent.Records)
        {
            Console.WriteLine($"[{record.EventSource}] Body = {record.Body}");
        }
    }
}
```