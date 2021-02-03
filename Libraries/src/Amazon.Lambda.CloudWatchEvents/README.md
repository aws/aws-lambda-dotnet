# Amazon.Lambda.CloudWatchEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon CloudWatch events.

There are some preexisting CloudWatchEvent detail classes in the corresponding sub-folder(s). To create a new CloudWatchEvent detail type, follow the existing pattern to create model classes for an event and then use `CloudWatchEvent<T>` generic class as a base class for the modeled event. Refer [CloudWatch Events Event Examples From Supported Services](https://docs.aws.amazon.com/AmazonCloudWatch/latest/events/EventTypes.html) for events supported by services.

# Sample Function

Below is a sample class and Lambda function that illustrates how a class `ECSTaskStateChangeEvent` (derived from `CloudWatchEvent<T>`) can be used. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public string Handler(ECSTaskStateChangeEvent ecsTaskStateChangeEvent)
    {
        Console.WriteLine($"ECS Task ARN - {ecsTaskStateChangeEvent.Detail.TaskArn}");
    }
}
```