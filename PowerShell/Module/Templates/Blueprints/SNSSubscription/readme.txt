This sample creates a Lambda function written in PowerShell that can be subscribed
to an SNS Topic. The script will process each record published to the SNS Topic.

The script has a Requires statement for the AWS.Tools.Common as an example for how to declare modules on
which your function is dependent and that will be bundled with your function on
deployment. If you do not need to use cmdlets from this module you can safely delete
this statement.
