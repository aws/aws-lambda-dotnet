namespace BlueprintBaseName._1

open System
open System.Text

open Amazon.Lambda.Core
open Amazon.Lambda.S3Events
open Amazon.S3
open Amazon.S3.Util
open System.IO

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
()

type Function(s3Client:IAmazonS3) =

    new() =
        new Function(new AmazonS3Client())

    /// <summary>
    /// A function to process Kinesis events
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member _this.FunctionHandler (evnt: S3Event) (context: ILambdaContext) =

        let fetchContentType (s3Event : S3EventNotification.S3Entity) = async {

            context.Logger.LogLine(sprintf "Processing object %s from bucket %s" s3Event.Object.Key s3Event.Bucket.Name)

            let! response = s3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key) |> Async.AwaitTask
            context.Logger.LogLine((sprintf "Content Type %s" response.Headers.ContentType))
            return response.Headers.ContentType
        }

        fetchContentType (evnt.Records.Item(0).S3) |> Async.RunSynchronously

