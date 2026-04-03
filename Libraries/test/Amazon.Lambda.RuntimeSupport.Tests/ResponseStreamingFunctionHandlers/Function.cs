#pragma warning disable CA2252

using Amazon.Lambda.Core;
using Amazon.Lambda.Core.ResponseStreaming;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

// The function handler that will be called for each Lambda event
var handler = async (string input, ILambdaContext context) =>
{
    using var stream = LambdaResponseStreamFactory.CreateStream();

    switch(input)
    {
        case $"{nameof(SimpleFunctionHandler)}":
            await SimpleFunctionHandler(stream, context);
            break;
        case $"{nameof(StreamContentHandler)}":
            await StreamContentHandler(stream, context);
            break;
        case $"{nameof(UnhandledExceptionHandler)}":
            await UnhandledExceptionHandler(stream, context);
            break;
        default:
            throw new ArgumentException($"Unknown handler scenario {input}");
    }
};

async Task SimpleFunctionHandler(Stream stream, ILambdaContext context)
{
    using var writer = new StreamWriter(stream);
    await writer.WriteAsync("Hello, World!");
}

async Task StreamContentHandler(Stream stream, ILambdaContext context)
{
    using var writer = new StreamWriter(stream);

    await writer.WriteLineAsync("Starting stream content...");
    for(var i = 0; i < 10000; i++)
    {
        await writer.WriteLineAsync($"Line {i}");
    }
    await writer.WriteLineAsync("Finish stream content");
}

async Task UnhandledExceptionHandler(Stream stream, ILambdaContext context)
{
    using var writer = new StreamWriter(stream);
    await writer.WriteAsync("This method will fail");
    throw new InvalidOperationException("This is an unhandled exception");
}

await LambdaBootstrapBuilder.Create(handler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();
