This sample creates a Lambda function written in PowerShell that responds to an
event from S3 when a new object, or object version, is created in a bucket. The
script then uses the Amazon Rekognition service to detect objects of interest in
the image (called 'labels') with a minimum confidence level which it then applies
(up to maximum of 10) as tags to the S3 object. The confidence level can be customized
by setting the environment variable MinConfidence to the required value.

To work with Amazon S3 and Amazon Rekognition the script uses cmdlets from the
AWS.Tools.S3 and AWS.Tools.Rekognition modules. To run successfully the
function must be deployed with a role allowing access to the following service operations:

rekognition:DetectLabels
s3:PutObjectTagging

The script contains a Requires statement for the latest version of the AWS Tools for
PowerShell module. If you modify this example to not need cmdlets from that
module you can safely delete this statement.
