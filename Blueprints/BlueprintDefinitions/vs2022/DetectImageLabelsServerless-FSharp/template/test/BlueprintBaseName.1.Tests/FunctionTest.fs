namespace BlueprintBaseName._1.Tests


open Xunit
open Amazon.Lambda.S3Events
open Amazon.Lambda.TestUtilities

open Amazon.Rekognition

open Amazon
open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util

open System
open System.Collections.Generic

open BlueprintBaseName._1

module FunctionTest =
    [<Fact>]
    let ``Test Detecting Images with Sample Pic``() = task {
        let fileName = "sample-pic.jpg"
        let bucketName = sprintf "lambda-blueprint-basename-%i" DateTime.Now.Ticks
        use s3Client = new AmazonS3Client(RegionEndpoint.USWest2)
        use rekognitionClient = new AmazonRekognitionClient(RegionEndpoint.USWest2)

        let! putBucketResponse =
            s3Client.PutBucketAsync(bucketName)
            |> Async.AwaitTask

        try
            let! putObjectResponse =
                PutObjectRequest(BucketName = bucketName, FilePath = fileName)
                |> s3Client.PutObjectAsync
                |> Async.AwaitTask

            let eventRecords = [
                S3EventNotification.S3EventNotificationRecord(
                    S3 = S3EventNotification.S3Entity(
                        Bucket = S3EventNotification.S3BucketEntity (Name = bucketName),
                        Object = S3EventNotification.S3ObjectEntity (Key = fileName)
                    )
                )
            ]

            let s3Event = S3Event(Records = List(eventRecords))
            let lambdaContext = TestLambdaContext()
            let lambdaFunction = Function(s3Client, rekognitionClient, 70.0f)
            lambdaFunction.FunctionHandler s3Event lambdaContext 
                |> Async.AwaitTask
                |> ignore

            let! getTagsResponse =
                GetObjectTaggingRequest(BucketName = bucketName, Key = fileName)
                |> s3Client.GetObjectTaggingAsync
                |> Async.AwaitTask

            Assert.True(getTagsResponse.Tagging.Count > 0)
        finally
        AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    }

    [<EntryPoint>]
    let main _ = 0
