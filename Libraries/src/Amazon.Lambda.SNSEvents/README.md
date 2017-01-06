# Amazon.Lambda.SNSEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Simple Notification Service (Amazon SNS) events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how an SNSEvent can be used. The function logs a summary of the events it received, including the event source, the timestamp, and the message of each event. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public string Handler(SNSEvent snsEvent)
    {
        foreach (var record in snsEvent.Records)
        {
            var snsRecord = record.Sns;
            Console.WriteLine($"[{record.EventSource} {snsRecord.Timestamp}] Message = {snsRecord.Message}");
        }
    }
}
```