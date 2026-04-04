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

app.MapGet("/oncompleted-test", async (HttpContext context) =>
{
    // Register an OnCompleted callback that writes a marker to a response header.
    // Since headers are sent in the prelude before the body, we use a different approach:
    // write a marker into the body from the OnCompleted callback via a shared flag.
    var completedMarker = new CompletedMarker();
    context.Response.RegisterForDispose(completedMarker);

    context.Response.OnCompleted(async (state) =>
    {
        var marker = (CompletedMarker)state;
        marker.WasExecuted = true;
        // Write to a static so the next request can verify it ran
        CompletedMarkerStore.LastMarkerExecuted = true;
    }, completedMarker);

    context.Response.ContentType = "text/plain";
    context.Response.StatusCode = 200;

    var stream = context.Response.BodyWriter.AsStream();
    using var writer = new StreamWriter(stream, leaveOpen: true);
    await writer.WriteAsync("OnCompleted callback registered");
    await writer.FlushAsync();
});

app.MapGet("/oncompleted-verify", (HttpContext context) =>
{
    // Returns whether the OnCompleted callback from the previous request was executed
    return Results.Json(new { onCompletedExecuted = CompletedMarkerStore.LastMarkerExecuted });
});

app.MapGet("/custom-headers", (HttpContext context) =>
{
    context.Response.StatusCode = 201;
    context.Response.ContentType = "text/plain";
    context.Response.Headers["X-Custom-Header"] = "custom-value";
    context.Response.Headers["X-Another-Header"] = "another-value";
    return Results.Text("Custom headers response", "text/plain", statusCode: 201);
});

app.MapGet("/set-cookie", (HttpContext context) =>
{
    context.Response.Cookies.Append("session", "abc123", new CookieOptions
    {
        Path = "/",
        HttpOnly = true
    });
    context.Response.Cookies.Append("theme", "dark");
    return Results.Text("Cookies set");
});

app.MapPost("/echo-body", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    return Results.Text($"Echo: {body}");
});

app.Run();

class CompletedMarker : IDisposable
{
    public bool WasExecuted { get; set; }
    public void Dispose() { }
}

static class CompletedMarkerStore
{
    public static bool LastMarkerExecuted { get; set; }
}
