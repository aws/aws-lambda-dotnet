This sample creates a Lambda function written in PowerShell that processes custom
resource events from CloudFormation. It includes a Switch statement with placeholders
for the 3 different Request Types (Create, Update and Delete) that CloudFormation
sends. It also checks the event payload to see if CloudFormation delivered the event
via SNS (useful in case cross-account custom resources are in place) or if
CloudFormation sent the event directly to the Lambda. If the event is from SNS, it
will parse out the CloudFormation event information before processing the request type.

Once the event has been processed, it will send the results back to CloudFormation
via Invoke-WebRequest using the pre-signed URL sent with the original event.

The script contains a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.
