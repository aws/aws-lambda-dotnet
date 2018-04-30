namespace BlueprintBaseName._1

open System
open System.Collections.Generic

open Amazon.Lambda.Core
open Amazon.Lambda.S3Events


open Amazon.Rekognition
open Amazon.Rekognition.Model

open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util
open System.IO

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function(s3Client:IAmazonS3, rekognitionClient:IAmazonRekognition, minConfidence:float32) =

    let supportedImageTypes = Set.empty.Add(".png").Add(".jpg").Add(".jpeg")


    new() =

        let mutable minConfidence = 70.0f

        let environmentMinConfidence = System.Environment.GetEnvironmentVariable("MinConfidence")
        if not( environmentMinConfidence = null) then
            minConfidence <- Single.Parse(environmentMinConfidence)
            printfn "Setting minimum confidence to %f" minConfidence

        Function(new AmazonS3Client(), new AmazonRekognitionClient(), minConfidence)


        

    /// <summary>
    /// A function for responding to S3 create events. It will determine if the object is an image and use Amazon Rekognition
    /// to detect labels and add the labels as tags on the S3 object.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member _this.FunctionHandler (input: S3Event) (context: ILambdaContext) =
        
        let processRecordAsync (record:S3EventNotification.S3EventNotificationRecord) (context:ILambdaContext) = async {

            let extension = Path.GetExtension(record.S3.Object.Key)
            if not(extension = null || supportedImageTypes.Contains(extension.ToLowerInvariant())) then
                context.Logger.LogLine(sprintf "Object %s:%s is not a supported image type" record.S3.Bucket.Name record.S3.Object.Key)
            else                
                context.Logger.LogLine(sprintf "Looking for labels in image %s:%s" record.S3.Bucket.Name record.S3.Object.Key);

                let detectRequest = new DetectLabelsRequest(
                                        MinConfidence = minConfidence,
                                        Image = new Image(
                                                S3Object = new Amazon.Rekognition.Model.S3Object(
                                                            Bucket = record.S3.Bucket.Name,
                                                            Name = record.S3.Object.Key
                                                            )
                                                )
                                        )

                let! detectResponse = rekognitionClient.DetectLabelsAsync(detectRequest) |> Async.AwaitTask

                let s3Tags = detectResponse.Labels 
                                |> Seq.truncate 10 
                                |> Seq.map(fun x -> 
                                        context.Logger.LogLine(sprintf "\tFound Label %s with confidence %f" x.Name x.Confidence)
                                        new Tag(Key = x.Name, Value = x.Confidence.ToString()))
            
                let putTags = new PutObjectTaggingRequest(
                                BucketName = record.S3.Bucket.Name, 
                                Key = record.S3.Object.Key,
                                Tagging = new Tagging(TagSet = new List<Tag>(s3Tags))
                                )

                let! putResponse = s3Client.PutObjectTaggingAsync(putTags) |> Async.AwaitTask
                context.Logger.LogLine("Tags put on S3 object")
        }

        input.Records
            |> Seq.iter(fun x -> processRecordAsync x context |> Async.RunSynchronously)
