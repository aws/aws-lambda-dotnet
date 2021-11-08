using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

using Amazon.Rekognition;

using BlueprintBaseName._1;
using Amazon.Lambda.S3Events;

namespace BlueprintBaseName._1.Tests
{
    public class FunctionTest
    {
        [Fact]
        public async Task IntegrationTest()
        {
            const string fileName = "sample-pic.jpg";
            IAmazonS3 s3Client = new AmazonS3Client(RegionEndpoint.USWest2);
            IAmazonRekognition rekognitionClient = new AmazonRekognitionClient(RegionEndpoint.USWest2);

            var bucketName = "lambda-BlueprintBaseName.1-".ToLower() + DateTime.Now.Ticks;
            await s3Client.PutBucketAsync(bucketName);
            try
            {
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucketName,
                    FilePath = fileName
                });

                // Setup the S3 event object that S3 notifications would create and send to the Lambda function if
                // the bucket was configured as an event source.
                var s3Event = new S3Event
                {
                    Records = new List<S3EventNotification.S3EventNotificationRecord>
                    {
                        new S3EventNotification.S3EventNotificationRecord
                        {
                            S3 = new S3EventNotification.S3Entity
                            {
                                Bucket = new S3EventNotification.S3BucketEntity {Name = bucketName },
                                Object = new S3EventNotification.S3ObjectEntity {Key = fileName }
                            }
                        }
                    }
                };

                // Use test constructor for the function with the service clients created for the test
                var function = new Function(s3Client, rekognitionClient, Function.DEFAULT_MIN_CONFIDENCE);

                var context = new TestLambdaContext();
                await function.FunctionHandler(s3Event, context);

                var getTagsResponse = await s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest
                {
                    BucketName = bucketName,
                    Key = fileName
                });

                Assert.True(getTagsResponse.Tagging.Count > 0);
            }
            finally
            {
                // Clean up the test data
                await AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName);
            }
        }
    }
}
