# Amazon.Lambda.LexEvents

This package contains classes that can be used as input and response types for Lambda functions that process Amazon Lex events.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Lex event as an input and returns back a response.)

```csharp
public class Function
{
    public LexResponse Handler(LexEvent lexEvent)
    {
        // Process the event using the lexEvent.CurrentIntent.Slots

        var response = new LexResponse();
        response.DialogAction = new LexResponse.LexDialogAction
        {
            Type = "Close",
            Message = new LexResponse.LexMessage
            {
                ContentType = "PlainText",
                Content = "You are now complete!"
            }
        };

        return response;
    }
}
```
