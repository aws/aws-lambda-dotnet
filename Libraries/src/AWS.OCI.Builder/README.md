# AWS.OCI.Builder

A .NET library for building [OCI](https://opencontainers.org/) container images
**programmatically**, without a Docker daemon. It pulls a base image's manifest over the
registry HTTP API, overlays a new layer containing local files at specified container paths,
and pushes the resulting image to **Amazon ECR**.

## Status

This library is designed and supported for use by other AWS libraries and tools.
The version of the library is kept below 1.0.0 to indicate that the API is subject to change based on the needs of AWS libraries and tools.
If you are using this library in a non-AWS context, please be aware that the API may change in future releases.

## Authentication

This library has **no AWS SDK dependency**. Instead of resolving AWS credentials itself, it takes an ECR
authorization token that you obtain and pass to `PushToEcrAsync`. This keeps the library free of
`AWSSDK.Core`, so it will not conflict with the AWS SDK versions a host (for example an AWS Tools for
PowerShell cmdlet) already loads.

The token is the value returned by the ECR
[`GetAuthorizationToken`](https://docs.aws.amazon.com/AmazonECR/latest/APIReference/API_GetAuthorizationToken.html)
API — a base64-encoded `AWS:<password>` string. The library decodes it into the `Basic` username/password the
registry handshake needs. Obtaining and refreshing the token (it is valid for ~12 hours) is the caller's
responsibility; the target region is taken from the ECR host in the target reference.

```csharp
using Amazon.ECR;
using Amazon.ECR.Model;

// Caller obtains the token however it likes — directly via the SDK, or from an AWS Tools for PowerShell cmdlet.
using var ecr = new AmazonECRClient();  // region/credentials from the SDK default chain
var auth = await ecr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
string authorizationToken = auth.AuthorizationData[0].AuthorizationToken;   // base64("AWS:<password>")
```

## Usage

```csharp
using AWS.OCI.Builder;

// Pull the base image (selects the right platform from a manifest list).
using var builder = await OciImageBuilder.FromBaseImageAsync(
    "mcr.microsoft.com/dotnet/aspnet:8.0",
    platform: "linux/amd64");

// Overlay local files as a new layer. Each call adds one layer.
await builder.AddLayerAsync(new FileManifest
{
    { "/local/publish/MyApp.dll", "/app/MyApp.dll" },
    { "/local/publish/config",    "/app/config"    },
});

// Setting Linux file permissions (see "File permissions" below):
var files = new FileManifest();
files.Add("/local/publish/MyApp.dll", "/app/MyApp.dll");                 // default 0644
files.AddExecutable("/local/scripts/entrypoint.sh", "/app/entrypoint.sh"); // 0755
files.Add("/local/secrets/key", "/app/key", UnixPermissions.FromOctal("600")); // explicit 0600
await builder.AddLayerAsync(files);

// Customize the image config.
builder.Config.SetWorkingDirectory("/app");
builder.Config.SetEntrypoint("dotnet", "/app/MyApp.dll");

// Environment variables are merged into the base image's existing config.Env. Setting a key the
// base image already defines (e.g. PATH) overrides it in place rather than duplicating it.
builder.Config.SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:8080");
builder.Config.SetEnvironmentVariable("DOTNET_EnableDiagnostics", "0");

// Obtain an ECR authorization token (the library has no AWS SDK dependency — getting the token is the
// caller's job; see "Authentication" above), then push. The region is taken from the ECR host in the target.
string digest = await builder.PushToEcrAsync(
    "123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:latest",
    authorizationToken);
```

### Tracking push progress

Pass an `IProgress<LayerPushProgress>` via `EcrPushOptions.Progress` to observe the push layer by layer — for
example to render a progress bar. It is entirely opt-in; omit it and the push reports nothing.

```csharp
var progress = new Progress<LayerPushProgress>(p =>
{
    switch (p.Phase)
    {
        case LayerPushPhase.Started:
            Console.WriteLine($"[{p.LayerIndex}/{p.TotalLayers}] {p.Kind} {p.Digest} — uploading {p.LayerSizeBytes:N0} bytes");
            break;
        case LayerPushPhase.Uploading:
            Console.WriteLine($"[{p.LayerIndex}/{p.TotalLayers}] {p.BytesPushed:N0} / {p.LayerSizeBytes:N0} bytes");
            break;
        case LayerPushPhase.Completed when p.IsSkipped:
            Console.WriteLine($"[{p.LayerIndex}/{p.TotalLayers}] {p.Digest} — already present, skipped");
            break;
        case LayerPushPhase.Completed:
            Console.WriteLine($"[{p.LayerIndex}/{p.TotalLayers}] {p.Digest} — done");
            break;
    }
});

string digest = await builder.PushToEcrAsync(
    "123456789012.dkr.ecr.us-west-2.amazonaws.com/myapp:latest",
    authorizationToken,
    new EcrPushOptions { Progress = progress });
```

Each layer reports a `Started` event, zero or more `Uploading` events as its bytes are sent, and a terminal
`Completed` event. A layer the registry already has reports a **single** `Completed` event with `IsSkipped = true`
and `BytesPushed = 0` — so "layer done" is always the one `Completed` phase, whether the layer was uploaded or
skipped. Only image layers are reported (the config blob and manifest are not layers), so `TotalLayers` is exactly
the number of layers.

Every event carries the layer's stable `LayerIndex` (1-based), `TotalLayers`, `Digest` and `Kind`
(`BaseLayer` vs `BuiltLayer`), all fixed before any upload begins. Key your state by `LayerIndex` or `Digest`
rather than the order events arrive: the contract is designed so that if the library later uploads layers in
parallel — interleaving their events — consumers keep working unchanged. A custom `IProgress<T>` implementation
must be thread-safe for that reason.

`System.Progress<T>` raises callbacks on the synchronization context it captured. Where there is none (console
apps, services, PowerShell), it raises them on thread-pool threads, possibly several at once. To read events on
your own thread instead, pass a `LayerPushProgressQueue` and drain it while the push runs:

```csharp
var queue = new LayerPushProgressQueue();
Task<string> push = builder.PushToEcrAsync(target, authorizationToken, new EcrPushOptions { Progress = queue });
while (!push.IsCompleted)
{
    while (queue.TryDequeue(out LayerPushProgress p)) { /* render p */ }
    await Task.WhenAny(push, Task.Delay(200));
}
while (queue.TryDequeue(out LayerPushProgress p)) { /* render p */ }
string digest = await push;
```

### File permissions

Each file in a layer carries a Unix mode. When you don't specify one:

- On **Linux/macOS** the source file's own mode is preserved, so an already-executable file stays executable.
- On **Windows** — which has no Unix mode bits to read — files default to `0644` and directories to `0755`.

Because Windows can't carry an executable bit, set the mode explicitly when you need a script or binary to be
runnable in the container regardless of the build host:

- `FileManifest.AddExecutable(local, container)` — marks the entry `0755`.
- `FileManifest.Add(local, container, mode)` — any `System.IO.UnixFileMode`.
- `UnixPermissions.FromOctal("755")` / `FromOctal(755)` — express a mode in `chmod`-style octal (C# has no octal
  literal, so `FromOctal` is how you write `0755`). For a directory mapping, an explicit mode applies to every file
  it contains; directory entries themselves are always `0755`.
