This sample creates a Lambda function written in PowerShell that can be subscribed
to an SQS Queue. For this sample, the SQS Queue would be subscribed to an SNS Topic,
which would be subscribed to S3 Events, for when a new object, or object version
is created in a bucket.

For example: S3 Event -> SNS Topic -> SQS Queue -> Lambda Function.

The SNS Subscription can be configured for "Raw Message Delivery" or not, this sample
is configured to handle both options.

The script has a Requires statement for the AWS.Tools.Common as an example for how to declare modules on
which your function is dependent and that will be bundled with your function on
deployment. If you do not need to use cmdlets from this module you can safely delete
this statement.

The script has a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.
