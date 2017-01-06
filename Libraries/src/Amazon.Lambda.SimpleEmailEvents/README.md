# Amazon.Lambda.SimpleEmailEvents

This package contains classes that can be used as input types for Lambda functions that process Amazon Simple Email Service (Amazon SES) events. 

# Sample Function

Below is a sample class and Lambda function that illustrates how a SimpleEmailEvent can be used. The function logs a summary of the events it received, including the event source, the timestamp, and the message of each event. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```
public class Function
{
    public string Handler(SimpleEmailEvent sesEvent)
    {
		foreach (var record in sesEvent.Records)
		{
			var sesRecord = record.Ses;
			Console.WriteLine($"[{record.EventSource} {sesRecord.Mail.Timestamp}] Subject = {sesRecord.Mail.CommonHeaders.Subject}");
		}
    }
}
```