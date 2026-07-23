using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

// A minimal function: uppercases the input string.
// Send the string "error" to see how the tool renders a thrown exception.
var handler = (string input, ILambdaContext context) =>
{
    context.Logger.LogLine($"Executing function with input: {input}");

    if (string.Equals("error", input, StringComparison.OrdinalIgnoreCase))
    {
        throw new Exception("Forced error to demonstrate error rendering.");
    }

    return input?.ToUpper();
};

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();
