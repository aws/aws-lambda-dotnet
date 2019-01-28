This sample creates a Lambda function written in PowerShell that can be subscribed
to an SQS Queue. For this sample, the SQS Queue would be subscribed to an SNS Topic.

For example: SNS Topic -> SQS Queue -> Lambda Function.

Assuming the Lambda function does not throw an exception, the SQS Message will be
removed from the SQS Queue by AWS. For more information, please review "Using AWS
Lambda with Amazon SQS" (https://docs.aws.amazon.com/lambda/latest/dg/with-sqs.html).

The scripts have a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.