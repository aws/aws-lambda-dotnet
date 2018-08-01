# Amazon.Lambda.ECSEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon CloudWatch ECS events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how a ECSEvents can be used. The function logs the deatil type of the event. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(ECSEvent ecsEvent)
    {
        Console.WriteLine($"Log content - {ecsEvent.DetailType}");
    }
}
```