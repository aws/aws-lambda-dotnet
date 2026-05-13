# Amazon.Lambda.DynamoDBEvents.SDK.Convertor

This package provides helper extension methods to use alongside Amazon.Lambda.DynamoDBEvents in order to transform Lambda input event model objects into SDK-compatible output model objects (eg. DynamodbEvent to a List of records writable back to DynamoDB through the AWS DynamoDB SDK for .NET).

## Overview

The `DynamodbAttributeValueConvertor` and `DynamodbStreamRecordConvertor` classes provide methods to convert DynamoDB event data from the Lambda format to the SDK format. This is useful when you need to process DynamoDB events in a Lambda function and interact with the AWS SDK for .NET.

## Usage

### Converting AttributeValue

The following example demonstrates how to convert a `DynamoDBEvent.AttributeValue` to an `Amazon.DynamoDBv2.Model.AttributeValue`:


The following is a sample class and Lambda function that receives Amazon DynamoDB event record data as an input and writes some of the incoming event data to CloudWatch Logs. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public class Function
{
    public void Handler(DynamoDBEvent ddbEvent)
    {
        foreach (var record in ddbEvent.Records)
        {
            var ddbRecord = record.Dynamodb;
            var sdkAttributeValue = ddbRecord.NewImage["exampleKey"].ConvertToSdkAttribute();
            Console.WriteLine($"Converted AttributeValue: {sdkAttributeValue.S}");
        }
    }
}
```


### Converting StreamRecord

The following example demonstrates how to convert a `DynamoDBEvent.StreamRecord` to an `Amazon.DynamoDBv2.Model.StreamRecord`:

```csharp

public class Function
{
    public void Handler(DynamoDBEvent ddbEvent)
    {
        foreach (var record in ddbEvent.Records)
        {
            var sdkStreamRecord = record.Dynamodb.ConvertToSdkStreamRecord();
            Console.WriteLine($"Converted StreamRecord: {sdkStreamRecord.SequenceNumber}");
        }
    }
}
```

### Converting Identity

The following example demonstrates how to convert a `DynamoDBEvent.Identity` to an `Amazon.DynamoDBv2.Model.Identity`:

```csharp
public class Function
{
    public void Handler(DynamoDBEvent ddbEvent)
    {
        foreach (var record in ddbEvent.Records)
        {
            var sdkIdentity = record.UserIdentity.ConvertToSdkIdentity();
            Console.WriteLine($"Converted Identity: {sdkIdentity.PrincipalId}");
        }
  }
}
```

