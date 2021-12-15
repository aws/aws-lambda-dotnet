namespace BlueprintBaseName._1.Tests


open System
open System.Collections.Generic

open Xunit
open Amazon.Lambda.TestUtilities
open Amazon.Lambda.S3Events

open Amazon
open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util

open BlueprintBaseName._1


module FunctionTest =
    [<Fact>]
    let ``Test getting content type for an event``() = async {
        use s3Client = new AmazonS3Client(RegionEndpoint.USWest2)
        let bucketName = sprintf "lambda-blueprint-basename-%i" DateTime.Now.Ticks
        let key = "text.txt"

        let! putBucketResponse =
            s3Client.PutBucketAsync(bucketName)
            |> Async.AwaitTask

        try
            let! putObjectResponse =
                PutObjectRequest(
                    BucketName = bucketName,
                    Key = key,
                    ContentBody = "sample data"
                )
                |> s3Client.PutObjectAsync
                |> Async.AwaitTask

            let eventRecords = [
                S3EventNotification.S3EventNotificationRecord(
                    S3 = S3EventNotification.S3Entity(
                        Bucket = S3EventNotification.S3BucketEntity (Name = bucketName),
                        Object = S3EventNotification.S3ObjectEntity (Key = key)
                    )
                )
            ]

            let s3Event = S3Event(Records = List(eventRecords))
            let lambdaContext = TestLambdaContext()
            let lambdaFunction = Function(s3Client)
            let contentType = lambdaFunction.FunctionHandler s3Event lambdaContext

            Assert.Equal("text/plain", contentType)

        finally
        AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    }

    [<EntryPoint>]
    let main _ = 0
