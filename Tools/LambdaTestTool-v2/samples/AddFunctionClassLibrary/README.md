# AddFunctionClassLibrary

The same "add two numbers" function as [`AddFunctionTopLevel`](../AddFunctionTopLevel), but as a **class library** (a handler method rather than top-level statements). This is the harder setup: the launch profile runs your function assembly under the test tool's copy of the Lambda runtime support library, so `Properties/launchSettings.json` needs a few values filled in.

Handler: `AddFunctionClassLibrary::AddFunctionClassLibrary.Function::Add`

## One-time setup

The committed [`launchSettings.json`](Properties/launchSettings.json) is complete **except** for one placeholder you must replace:

- **`{TEST_TOOL_VERSION}`** (appears **twice** in the `commandLineArgs` `.store` path) — your installed test tool version. Find it with:

  ```
  dotnet lambda-test-tool info
  ```

  (or `dotnet tool list -g`). For example, if the version is `0.15.0`, both `{TEST_TOOL_VERSION}` occurrences become `0.15.0`.

The other values are already set for this project: target framework `net8.0`, deps/runtimeconfig file names, and the handler string. If you adapt this to your own project, also update those.

> This profile is written for **Windows** (`%USERPROFILE%`, backslashes in `workingDirectory`). On **Linux/macOS**, replace `%USERPROFILE%` with `$HOME` and use forward slashes in `workingDirectory` (`./bin/$(Configuration)/net8.0`).

> The runtime support assembly is `Amazon.Lambda.RuntimeSupport.TestTool.dll` — renamed from `Amazon.Lambda.RuntimeSupport.dll` to avoid conflicting with the version your function references.

## Run it

**1. Build the function** so the `.deps.json` / `.runtimeconfig.json` exist:

```
dotnet build
```

**2. Set the API Gateway route** (this directory):

```bash
# Linux/macOS
export APIGATEWAY_EMULATOR_ROUTE_CONFIG='{"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}","Endpoint":"http://localhost:5050"}'
```

```powershell
# Windows (PowerShell)
$env:APIGATEWAY_EMULATOR_ROUTE_CONFIG='{"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}","Endpoint":"http://localhost:5050"}'
```

**3. Start the test tool** (same terminal):

```
dotnet lambda-test-tool start --lambda-emulator-port 5050 --api-gateway-emulator-port 5051 --api-gateway-emulator-mode HttpV2
```

**4. Start the function** with the launch profile (separate terminal, this directory):

```
dotnet run --launch-profile LambdaTestTool
```

**5. Invoke it:**

```
curl "http://localhost:5051/add/5/3"
# => 8
```
