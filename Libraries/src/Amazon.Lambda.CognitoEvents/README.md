# Amazon.Lambda.CognitoEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Cognito events.

# Sample Function

The following is a sample class and Lambda function that receives Amazon Cognito event record data as an input and writes the record data to CloudWatch Logs. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public void Handler(CognitoEvent cognitoEvent)
    {
        foreach(var datasetKVP in cognitoEvent.DatasetRecords)
        {
            var datasetName = datasetKVP.Key;
            var datasetRecord = datasetKVP.Value;

            Console.WriteLine($"[{cognitoEvent.EventType}-{datasetName}] {datasetRecord.OldValue} -> {datasetRecord.Op} -> {datasetRecord.NewValue}");
        }
    }
}
```
