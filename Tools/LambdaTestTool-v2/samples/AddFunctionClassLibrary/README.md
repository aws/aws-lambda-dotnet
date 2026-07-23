# AddFunctionClassLibrary

The same "add two numbers" function as [`AddFunctionTopLevel`](../AddFunctionTopLevel), but as a **class library** (a handler method rather than top-level statements). A class-library function is launched by running its assembly under the test tool's copy of the Lambda runtime support library. You can do this from the command line or from an IDE.

Handler: `AddFunctionClassLibrary::AddFunctionClassLibrary.Function::Add`

## Setup (once)

The `.csproj` sets `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` so the function's NuGet dependencies (e.g. `Amazon.Lambda.Core.dll`) are copied next to the output DLL — required for the command-line launch below.

Find your installed test tool version, which you'll substitute for `{TEST_TOOL_VERSION}`:

```
dotnet lambda-test-tool info
```

(or `dotnet tool list -g`). For example, `0.15.0`.

> The runtime support assembly is `Amazon.Lambda.RuntimeSupport.TestTool.dll` — renamed from `Amazon.Lambda.RuntimeSupport.dll` to avoid conflicting with the version your function references.

## Run it (command line)

**1. Build the function:**

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

**4. Start the function** from its build output directory (separate terminal), replacing `{TEST_TOOL_VERSION}` with your installed version:

```bash
# Linux/macOS
cd bin/Debug/net8.0
export AWS_LAMBDA_RUNTIME_API="localhost:5050/AddLambdaFunction"

dotnet exec \
    --depsfile ./AddFunctionClassLibrary.deps.json \
    --runtimeconfig ./AddFunctionClassLibrary.runtimeconfig.json \
    "$HOME/.dotnet/tools/.store/amazon.lambda.testtool/{TEST_TOOL_VERSION}/amazon.lambda.testtool/{TEST_TOOL_VERSION}/content/Amazon.Lambda.RuntimeSupport/net8.0/Amazon.Lambda.RuntimeSupport.TestTool.dll" \
    "AddFunctionClassLibrary::AddFunctionClassLibrary.Function::Add"
```

**5. Invoke it:**

```
curl "http://localhost:5051/add/5/3"
# => 8
```

## Run it (IDE: Visual Studio / Rider)

The committed [`launchSettings.json`](Properties/launchSettings.json) wraps the same command as an `Executable` profile you can launch with F5. Before using it, replace `{TEST_TOOL_VERSION}` (it appears **twice** in the `commandLineArgs` `.store` path) with your installed version.

The other values are already set for this project: target framework `net8.0`, deps/runtimeconfig file names, and the handler string.

> This profile relies on the IDE expanding `$(Configuration)` and resolving `workingDirectory`. Plain `dotnet run --launch-profile` does **not** do this — use the command-line steps above outside an IDE.

> The profile is written for **Windows** (`%USERPROFILE%`, backslashes). On **Linux/macOS**, replace `%USERPROFILE%` with `$HOME` and use forward slashes in `workingDirectory` (`./bin/$(Configuration)/net8.0`).

Then set `APIGATEWAY_EMULATOR_ROUTE_CONFIG`, start the test tool (steps 2–3 above), press F5 on the `LambdaTestTool` profile, and invoke with the step 5 curl.
