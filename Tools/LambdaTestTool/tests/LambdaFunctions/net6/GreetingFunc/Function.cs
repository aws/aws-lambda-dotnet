using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace GreetingFunc;

public record Input(string UserName);

public record Output(string GreetingMessage)
{
    public static Output BuildGreeting(string userName) => new($"Hello, {userName}!");
}

public class Function
{
    [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
    public static Output FunctionHandler(Input input, ILambdaContext context)
    {
        context.Logger.LogLine($"Executing greeting for user: {input.UserName}");

        if (string.IsNullOrEmpty(input.UserName))
            throw new Exception("User name is empty");

        return Output.BuildGreeting(input.UserName);
    }
}