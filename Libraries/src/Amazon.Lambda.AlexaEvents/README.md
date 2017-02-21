# Amazon.Lambda.AlexaEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Alexa events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how a AlexaEvents can be used. The function logs an Alexa intent (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public string Handler(AlexaEvent alexaEvent)
    {
		Console.WriteLine($"Intent = {alexaEvent.Intent}");
    }
}
```