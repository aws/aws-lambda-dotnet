namespace BlueprintBaseName._1


open Amazon.Lambda.Core
open Amazon.Lambda.S3Events

open Amazon.S3
open Amazon.S3.Util


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()


type Function(s3Client: IAmazonS3) =
    new() = Function(new AmazonS3Client())

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    member __.FunctionHandler (event: S3Event) (context: ILambdaContext) = task {

        let s3Event = event.Records.Item(0).S3

        sprintf "Processing object %s from bucket %s" s3Event.Object.Key s3Event.Bucket.Name
        |> context.Logger.LogInformation

        let! response =
            s3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key)
            |> Async.AwaitTask

        sprintf "Content Type %s" response.Headers.ContentType
        |> context.Logger.LogInformation

        return response.Headers.ContentType
    }