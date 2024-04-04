using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Amazon.Lambda.LexEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

public class Function
{

    /// <summary>
    /// Then entry point for the Lambda function that looks at the current intent and calls 
    /// the appropriate intent process.
    /// </summary>
    /// <param name="lexEvent">The event coming from the Lex service.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public LexResponse FunctionHandler(LexEvent lexEvent, ILambdaContext context)
    {
        IIntentProcessor process;

        if (lexEvent.CurrentIntent.Name == "OrderFlowers")
        {       
            process = new OrderFlowersIntentProcessor();                  
        }
        else
        {
            throw new Exception($"Intent with name {lexEvent.CurrentIntent.Name} not supported");
        }


        return process.Process(lexEvent, context);
    }

}