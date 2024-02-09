using Amazon.Lambda.Core;

namespace Amazon.Lambda.AspNetCoreServer.Hosting;

/// <summary>
/// Options for configuring AWS Lambda hosting for ASP.NET Core
/// </summary>
public class HostingOptions
{
    /// <summary>
    /// The ILambdaSerializer used by Lambda to convert the incoming event JSON into the .NET event type and serialize the .NET response type
    /// back to JSON to return to Lambda.
    /// </summary>
    public ILambdaSerializer Serializer { get; set; }

    /// <summary>
    /// The encoding response options sent from the Lambda to the API Gateway.
    /// Used to adjust what content types are sent to the API Gatway as binary data
    /// </summary>
    public IEncodingOptions EncodingOptions { get; set; }
}