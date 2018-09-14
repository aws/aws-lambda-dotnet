This sample creates a Lambda function written in PowerShell that can be subscribed
to an SQS Queue. The script will process SQS Messages, and delete them when complete.
The script uses a cmdlet from the AWS Tools for PowerShell module (AWSPowerShell.NetCore)
to delete the SQS Message after successful processing.

The script has a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.

The example provides sample code to process an SQS Message that is subscribed to an
SNS Topic, and includes sample code for an S3 Event -> SNS Topic -> SQS Queue workflow.
