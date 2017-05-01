# Amazon.Lambda.ConfigEvents

This package contains classes that can be used as input types for Lambda functions that process AWS Config events.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Cognito event record data as an input and writes the record data to CloudWatch Logs. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public void Handler(ConfigEvent configEvent)
    {
        Console.WriteLine($"AWS Config rule - {configEvent.ConfigRuleName}");
        Console.WriteLine($"Invoking event JSON - {configEvent.InvokingEvent}");
        Console.WriteLine($"Event version - {configEvent.Version}");
    }
}
```
