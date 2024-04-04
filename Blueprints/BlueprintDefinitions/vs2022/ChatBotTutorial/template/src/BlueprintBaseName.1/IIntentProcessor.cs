using Amazon.Lambda.Core;
using Amazon.Lambda.LexEvents;

namespace BlueprintBaseName._1;

/// <summary>
/// Represents an intent processor that the Lambda function will invoke to process the event.
/// </summary>
public interface IIntentProcessor
{
    /// <summary>
    /// Main method for processing the Lex event for the intent.
    /// </summary>
    /// <param name="lexEvent">The event coming from the Lex service.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    LexResponse Process(LexEvent lexEvent, ILambdaContext context);
}