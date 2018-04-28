namespace BlueprintBaseName._1.Tests

open System
open System.Collections.Generic

open Xunit
open Amazon.Lambda.Core
open Amazon.Lambda.TestUtilities

open Amazon
open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util

open Amazon.Rekognition

open BlueprintBaseName._1
open Amazon.Lambda.S3Events

module FunctionTest =

    [<Fact>]
    let ``Test Detecting Images with Sample Pic``() = async {

        let fileName = "sample-pic.jpg"
        use s3Client = new AmazonS3Client(RegionEndpoint.USWest2)
        use rekognitionClient = new AmazonRekognitionClient(RegionEndpoint.USWest2)      

        let bucketName = sprintf "lambda-blueprint-basename-%i" DateTime.Now.Ticks

        let! putBucketResponse = s3Client.PutBucketAsync(bucketName) 
                                    |> Async.AwaitTask 
        try

            let! putObjectResponse = s3Client.PutObjectAsync(new PutObjectRequest(BucketName = bucketName, FilePath = fileName)) 
                                        |> Async.AwaitTask 

        
            let s3Event = new S3Event(Records = new List<S3EventNotification.S3EventNotificationRecord>())
            s3Event.Records.Add(new S3EventNotification.S3EventNotificationRecord(
                                        S3 = new S3EventNotification.S3Entity(
                                                Bucket = new S3EventNotification.S3BucketEntity (Name = bucketName ),
                                                Object = new S3EventNotification.S3ObjectEntity (Key = fileName )
                                                )
                                      ))

            let lambdaContext = new TestLambdaContext()
            let lambdaFunction = new Function(s3Client, rekognitionClient, 70.0f)
            lambdaFunction.FunctionHandler s3Event lambdaContext

            let! getTagsResponse = s3Client.GetObjectTaggingAsync(new GetObjectTaggingRequest(
                                                                        BucketName = bucketName,
                                                                        Key = fileName
                                                                        )) |> Async.AwaitTask

            Assert.True(getTagsResponse.Tagging.Count > 0);
        finally
            AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName) 
                |> Async.AwaitTask 
                |> Async.RunSynchronously
    }
    
    [<EntryPoint>]
    let main argv = 0