namespace BlueprintBaseName._1

open Amazon.Lambda.Core
open Amazon.Lambda.S3Events

open Amazon.Rekognition
open Amazon.Rekognition.Model

open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util

open System
open System.Collections.Generic
open System.IO


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


type Function(s3Client: IAmazonS3, rekognitionClient: IAmazonRekognition, minConfidence: float32) =
    let supportedImageTypes = set [".png"; ".jpg"; ".jpeg"]

    new() =
        let environmentMinConfidence = System.Environment.GetEnvironmentVariable("MinConfidence")
        let minConfidence =
            match Single.TryParse(environmentMinConfidence) with
            | false, _ -> 70.0f
            | true, confidence ->
                printfn "Setting minimum confidence to %f" confidence
                confidence

        Function(new AmazonS3Client(), new AmazonRekognitionClient(), minConfidence)

    /// <summary>
    /// A function for responding to S3 create events. It will determine if the object is an image
    /// and use Amazon Rekognition to detect labels and add the labels as tags on the S3 object.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (input: S3Event) (context: ILambdaContext) = task {

        let isSupportedImageType (record: S3EventNotification.S3EventNotificationRecord) =
            match Set.contains (Path.GetExtension record.S3.Object.Key) supportedImageTypes with
            | true -> true
            | false ->
                sprintf "Object %s:%s is not a supported image type" record.S3.Bucket.Name record.S3.Object.Key
                |> context.Logger.LogInformation
                false

        let processRecordAsync (record: S3EventNotification.S3EventNotificationRecord) (context: ILambdaContext) = task {
            sprintf "Looking for labels in image %s:%s" record.S3.Bucket.Name record.S3.Object.Key
            |> context.Logger.LogInformation

            let detectRequest =
                DetectLabelsRequest(
                    MinConfidence = minConfidence,
                    Image = Image(
                        S3Object = Amazon.Rekognition.Model.S3Object(
                            Bucket = record.S3.Bucket.Name,
                            Name = record.S3.Object.Key
                        )
                    )
                )

            let! detectResponse =
                rekognitionClient.DetectLabelsAsync(detectRequest)
                |> Async.AwaitTask

            let s3Tags =
                detectResponse.Labels
                |> Seq.truncate 10
                |> Seq.map (fun x ->
                    sprintf "\tFound Label %s with confidence %f" x.Name x.Confidence |> context.Logger.LogInformation
                    Tag(Key = x.Name, Value = string x.Confidence))
                |> List

            let putTags =
                PutObjectTaggingRequest(
                    BucketName = record.S3.Bucket.Name,
                    Key = record.S3.Object.Key,
                    Tagging = Tagging(TagSet = s3Tags)
                )

            let! putResponse =
                s3Client.PutObjectTaggingAsync(putTags)
                |> Async.AwaitTask

            context.Logger.LogInformation("Tags put on S3 object")
        }

        input.Records
        |> Seq.filter isSupportedImageType
        |> Seq.iter(fun x -> processRecordAsync x context |> Async.AwaitTask |> Async.RunSynchronously)
    }