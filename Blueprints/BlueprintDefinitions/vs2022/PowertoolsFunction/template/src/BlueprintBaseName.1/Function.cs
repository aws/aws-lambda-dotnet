using Amazon.Lambda.Core;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using AWS.Lambda.Powertools.Tracing;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

/// <summary>
/// Learn more about Lambda Powertools at https://awslabs.github.io/aws-lambda-powertools-dotnet/
/// Lambda Powertools Logging: https://awslabs.github.io/aws-lambda-powertools-dotnet/core/logging/
/// Lambda Powertools Tracing: https://awslabs.github.io/aws-lambda-powertools-dotnet/core/tracing/
/// Lambda Powertools Metrics: https://awslabs.github.io/aws-lambda-powertools-dotnet/core/metrics/
/// Metrics Namespace and Service can be defined through Environment Variables https://awslabs.github.io/aws-lambda-powertools-dotnet/core/metrics/#getting-started
/// </summary>
public class Function
{

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    [Logging(LogEvent = true, Service = "To_Upper_Function")]
    [Tracing(CaptureMode = TracingCaptureMode.ResponseAndError)]
    public string FunctionHandler(string input, ILambdaContext context)
    {
        var upperCaseString = UpperCaseString(input);

        Logger.LogInformation($"Uppercase of '{input}' is {upperCaseString}");

        return upperCaseString;
    }

    [Metrics(Namespace = "PowertoolsNamespace", CaptureColdStart = true, Service = "To_Upper_Function")]
    [Tracing(SegmentName = "UpperCaseString Method")]
    private static string UpperCaseString(string input)
    {
        try
        {
            Metrics.AddMetric("UpperCaseString_Invocations", 1, MetricUnit.Count);

            return input.ToUpper();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            throw;
        }
    }
}
