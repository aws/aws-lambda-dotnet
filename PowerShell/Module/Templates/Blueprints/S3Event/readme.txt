This sample creates a Lambda function written in PowerShell that responds to an
event from S3 when a new object, or object version, is created in a bucket. The
script uses a cmdlet from the AWS.Tools.S3 module to read the object size and 
version and output them to the function logs.

The script has a Requires statement for the AWS.Tools.Common as an example for how to declare modules on
which your function is dependent and that will be bundled with your function on
deployment. If you do not need to use cmdlets from this module you can safely delete
this statement.
