# Amazon.Lambda.LexV2Events

This package contains classes that can be used as input and response types for Lambda functions that process Amazon Lex V2 events.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Lex V2 event as an input and returns back a response.

```csharp
public class Function
{
    public LexV2Response Handler(LexV2Event lexV2Event)
    {
        // Process the event.

        var tempSessionState = lexV2Event.SessionState;
        tempSessionState.DialogAction = new LexV2DialogAction() { Type = "Close" };

        var response = new LexV2Response
        {
            SessionState = tempSessionState,
            RequestAttributes = lexV2Event.RequestAttributes,
            Messages = new List<LexV2Message>() { 
                new LexV2Message() {
                    ContentType = "ImageResponseCard",
                    ImageResponseCard = new LexV2ImageResponseCard() {
                        Buttons = new List<LexV2Button>() {
                            new LexV2Button(){ Text = "Take Action", Value = "takeaction" }
                        },
                        Title = "Take Action",
                        Subtitle = "Click button to take action"
                    }
                }
            }
        };

        return response;
    }
}
```
