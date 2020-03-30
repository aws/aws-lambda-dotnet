This sample creates a Lambda function written in PowerShell that can be subscribed
to an SQS Queue. For this sample, the SQS Queue would be subscribed to an SNS Topic.

For example: SNS Topic -> SQS Queue -> Lambda Function.

Assuming the Lambda function does not throw an exception, the SQS Message will be
removed from the SQS Queue by AWS. For more information, please review "Using AWS
Lambda with Amazon SQS" (https://docs.aws.amazon.com/lambda/latest/dg/with-sqs.html).

The script has a Requires statement for the AWS.Tools.Common as an example for how to declare modules on
which your function is dependent and that will be bundled with your function on
deployment. If you do not need to use cmdlets from this module you can safely delete
this statement.