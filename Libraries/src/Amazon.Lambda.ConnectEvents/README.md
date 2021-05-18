# Amazon.Lambda.ConnectEvents

This package contains classes that can be used as input and response types for Lambda functions that process Amazon Connect events.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Connect ContactFlow event as an input and returns back a response. The Lambda function response should be a simple string map.

```csharp
public class Function
{
    public IDictionary<string, string> Handler(ContactFlowEvent contactFlowEvent)
    {
        // Process the event

        var response = new Dictionary<string, string>()
        {
            { "Name", "CustomerName" },
            { "Address", "1234 Main Road" },
            { "CallerType", "Patient" }
        };

        return response;
    }
}
```
