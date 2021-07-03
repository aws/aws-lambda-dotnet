using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }
        HttpClient OriginalSourceHttpClient { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
            OriginalSourceHttpClient = new HttpClient();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        /// <param name="originalSourceHttpClient"></param>
        public Function(IAmazonS3 s3Client, HttpClient originalSourceHttpClient)
        {
            this.S3Client = s3Client;
            this.OriginalSourceHttpClient = originalSourceHttpClient;
        }
        
        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(S3ObjectLambdaEvent evnt, ILambdaContext context)
        {
            // Fetch the original object
            context.Logger.LogLine($"Fetching original object from: {evnt.GetObjectContext.InputS3Url}");
            var originalText = await this.OriginalSourceHttpClient.GetStringAsync(evnt.GetObjectContext.InputS3Url);
            context.Logger.LogLine($"Length of original string is {originalText.Length}");
			
            // Transform the original object.
            var transformedObject = originalText.ToUpper();

            // Send transformed object to S3 which will send the transformed object to the caller using
            // the making the S3 GET request.
            await S3Client.WriteGetObjectResponseAsync(new WriteGetObjectResponseRequest
            {
                Body = new MemoryStream(UTF8Encoding.UTF8.GetBytes(transformedObject)),
                RequestRoute = evnt.GetObjectContext.OutputRoute,
                RequestToken = evnt.GetObjectContext.OutputToken
            });
        }
    }
}
