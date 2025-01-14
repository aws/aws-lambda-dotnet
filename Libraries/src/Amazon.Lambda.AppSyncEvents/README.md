# Amazon.Lambda.AppSyncEvents

This package contains classes that can be used as input types for Lambda functions that process AppSync events.

# Sample Function

The following is a sample class and Lambda function that receives AppSync event record data as an input and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(AppSyncEvent appSyncEvent)
    {
        var input = JsonSerializer.Serialize(appSyncEvent.Arguments);
        Console.WriteLine($"AppSync request payload {input}.");
    }
}
```
