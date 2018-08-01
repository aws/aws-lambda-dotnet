# Amazon.Lambda.ScheduledEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon CloudWatch Scheduled events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how a ScheduledEvents can be used. The function logs the deatil type of the event. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(ScheduledEvent scheduledEvent)
    {
        Console.WriteLine($"Log content - {scheduledEvent.DetailType}");
    }
}
```