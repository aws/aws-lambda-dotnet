namespace BlueprintBaseName.1.Tests

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks

open Xunit
open Amazon.Lambda
open Amazon.Lambda.Core
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.S3Events

open Amazon
open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util

open BlueprintBaseName.1

module FunctionTest =    

    [<Fact>]
    let ``Test getting content type for an event``() = async {

        let s3Client = new AmazonS3Client(RegionEndpoint.USWest2)

        let bucketName = sprintf "lambda-blueprint-basename-%i" DateTime.Now.Ticks;
        let key = "text.txt";

        let! putBucketResponse = s3Client.PutBucketAsync(bucketName) 
                                    |> Async.AwaitTask

        try
            let! putObjectResponse = s3Client.PutObjectAsync(new PutObjectRequest(
                                                                    BucketName = bucketName, 
                                                                    Key = key,
                                                                    ContentBody = "sample data"
                                                                    )
                                                                ) 
                                        |> Async.AwaitTask 

            let s3Event = new S3Event(Records = new List<S3EventNotification.S3EventNotificationRecord>())
            s3Event.Records.Add(new S3EventNotification.S3EventNotificationRecord(
                                        S3 = new S3EventNotification.S3Entity(
                                                    Bucket = new S3EventNotification.S3BucketEntity (Name = bucketName),
                                                    Object = new S3EventNotification.S3ObjectEntity (Key = key)
                                                    )
                                        )
            )
                                                    

            let lambdaContext = new TestLambdaContext()
            let lambdaFunction = new Function(s3Client)
            let contentType = lambdaFunction.FunctionHandler s3Event lambdaContext

            Assert.Equal("text/plain", contentType);

        finally
            AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName) 
                |> Async.AwaitTask 
                |> Async.RunSynchronously
    }

    
    [<EntryPoint>]
    let main argv = 0