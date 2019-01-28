This sample creates a Lambda function written in PowerShell that can be subscribed
to an SNS Topic. For this sample, the SNS Topic would be subscribed to S3 Events,
for when a new object, or object version is created in a bucket.

For example: S3 Event -> SNS Topic -> Lambda Function.

The script uses a cmdlet from the AWS Tools for PowerShell module
(AWSPowerShell.NetCore) to read the object size and version and output them to
the function logs.

The script has a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.
