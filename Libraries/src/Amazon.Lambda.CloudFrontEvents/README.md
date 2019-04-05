# Amazon.Lambda.CloudFrontEvents

This package contains classes that can be used as input types for Lambda@Edge functions that process Amazon CloudFront events.

# Sample Function

Below is a sample class and Lambda@Edge function that illustrates how an CloudFrontEvent can be used. The function logs a summary of the events it received, including the distribution id, the event type, and the uri of each event. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(CloudFrontEvent cloudFrontEvent)
    {
        foreach (var record in cloudFrontEvent.Records)
        {
            var cf = record.Cf;
            Console.WriteLine($"[{cf.Config.DistributionId} {cf.Config.EventType}] Uri = {cf.Request.Uri}");
        }
    }
}
```