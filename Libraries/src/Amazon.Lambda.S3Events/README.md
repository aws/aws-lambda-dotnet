# Amazon.Lambda.S3Events

This package contains classes that can be used as input types for Lambda functions that process Amazon Simple Storage Service (Amazon S3) events. 

This package has a dependency on the [AWS SDK for .NET package AWSSDK.S3](https://www.nuget.org/packages/AWSSDK.S3/) in order to use the `Amazon.S3.Util.S3EventNotification` type. 

# Serialization

If you are using this package with Amazon Lambda but are not also using `Amazon.Lambda.Serialization.Json`, be aware that two properties require custom serialization. These properties are both part of the `Amazon.S3.Util.S3EventNotification+ResponseElementsEntity` class.
1. `XAmzRequestId` should be treated as `x-amz-request-id`
2. `XAmzId2` should be treated as `x-amz-id-2`

A Newtonsoft.Json `IContractResolver` implementation which handles this custom serialization is located in [Amazon.Lambda.Serialization.Json\AwsResolver.cs](Libraries/src/Amazon.Lambda.Serialization.Json/AwsResolver.cs), consult this source for more information. 

# Sample Function

Below is a sample class and Lambda function that illustrates how an S3Event can be used. The function logs a summary of the events it received, including the type and time of event, bucket, and key. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public string Handler(S3Event s3Event)
    {
        foreach(var record in s3Event.Records)
        {
            var s3 = record.S3;
            Console.WriteLine($"[{record.EventSource} - {record.EventTime}] Bucket = {s3.Bucket.Name}, Key = {s3.Object.Key}");
        }
    }
}
```