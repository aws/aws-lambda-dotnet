using Amazon.Lambda.AspNetCoreServer.Hosting.Internal;
using Amazon.Lambda.AspNetCoreServer.Internal;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.AspNetCore.Hosting.Server;

if (args.Length != 2)
{
    throw new Exception("Incorrect command line arguments: <request-file-path> <response-file-path>");
}

// Grab the request we want to test with
var requestFilePath = args[0];
if(!File.Exists(requestFilePath))
{
    throw new Exception($"Unable to find request path {requestFilePath}");
}
var requestJsonContent = File.ReadAllText(requestFilePath);

var lambdaSerializer = new DefaultLambdaJsonSerializer();
var apiGatewayRequest = lambdaSerializer.Deserialize<APIGatewayProxyRequest>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(requestJsonContent)));



var builder = WebApplication.CreateBuilder(args);

// Inject our own Lambda server replacing Kestrel.
builder.Services.AddSingleton<IServer, LambdaServer>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", () => "Welcome to running ASP.NET Core Minimal API on AWS Lambda");

app.MapPost("/test-post-complex", (Jane jane) =>
{
    return Results.Ok($"works:{jane.TestString}");
});

var source = new CancellationTokenSource();
var runTask = app.RunAsync(source.Token);
await Task.Delay(1000);

// Now that ASP.NET Core has started send the request into ASP.NET Core via Lambda function which will grab the LambdaServer register for IServer and forward the request in.
var lambdaFunction = new APIGatewayRestApiLambdaRuntimeSupportServer.APIGatewayRestApiMinimalApi(app.Services);
var response = await lambdaFunction.FunctionHandlerAsync(apiGatewayRequest, new TestLambdaContext());

var responseFilePath = args[1];
using (var outputStream = File.OpenWrite(responseFilePath))
{
    lambdaSerializer.Serialize(response, outputStream);
}

// Deterministically shut the host down and wait for it to finish. Without this the process relied on
// reaching the end of the top-level statements while the host was still running in the background, which
// does not reliably terminate the process: the host keeps non-background threads alive, so "dotnet run"
// (and therefore the parent "dotnet test" that captures its output) can hang indefinitely. Awaiting the
// stopped host, then explicitly exiting, guarantees the process ends promptly once the response is written.
source.Cancel();
try
{
    await runTask;
}
catch (OperationCanceledException)
{
    // Expected when the host is stopped via the cancellation token.
}

Environment.Exit(0);


public record Jane(string TestString);

