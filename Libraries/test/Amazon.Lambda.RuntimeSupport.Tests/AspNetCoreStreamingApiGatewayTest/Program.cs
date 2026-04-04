#pragma warning disable CA2252

using Amazon.Lambda.AspNetCoreServer.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi, options =>
{
    options.EnableResponseStreaming = true;
});

var app = builder.Build();

app.MapGet("/", () => "Welcome to ASP.NET Core streaming on Lambda");

app.MapGet("/streaming-test", async (HttpContext context) =>
{
    context.Response.ContentType = "text/plain";
    context.Response.StatusCode = 200;

    var stream = context.Response.BodyWriter.AsStream();
    using var writer = new StreamWriter(stream, leaveOpen: true);

    for (var i = 1; i <= 100; i++)
    {
        await writer.WriteLineAsync($"Line {i}");
        if (i % 10 == 0)
        {
            await writer.FlushAsync();
        }
    }
});

app.MapGet("/streaming-error", async (HttpContext context) =>
{
    context.Response.ContentType = "text/plain";
    context.Response.StatusCode = 200;

    var stream = context.Response.BodyWriter.AsStream();
    using var writer = new StreamWriter(stream, leaveOpen: true);

    for (var i = 1; i <= 10; i++)
    {
        await writer.WriteLineAsync($"Line {i}");
    }
    await writer.FlushAsync();

    throw new InvalidOperationException("Midstream error for testing");
});

app.MapGet("/json-response", (HttpContext context) =>
{
    return Results.Json(new { message = "Hello from streaming Lambda", timestamp = DateTime.UtcNow.ToString("o") });
});

app.Run();
