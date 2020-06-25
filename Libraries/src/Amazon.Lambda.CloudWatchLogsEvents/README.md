# Amazon.Lambda.CloudWatchLogsEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon CloudWatch Logs events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how an CloudWatchLogsEvents can be used. The function logs a summary of the events it received, including the type and time of event, bucket, and key. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(CloudWatchLogsEvent cloudWatchLogsEvent)
    {
        Console.WriteLine($"Log content - {cloudWatchLogsEvent.Awslogs.DecodeData()}");
    }
}
```