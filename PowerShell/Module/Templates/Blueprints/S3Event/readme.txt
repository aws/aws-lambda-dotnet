This sample creates a Lambda function written in PowerShell that responds to an
event from S3 when a new object, or object version, is created in a bucket. The
script uses a cmdlet from the AWS Tools for PowerShell module (AWSPowerShell.NetCore)
to read the object size and version and output them to the function logs.

The script has a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.
