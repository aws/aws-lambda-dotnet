# AddFunctionTopLevel

The [Quick Start](../../README.md#quick-start) function: a top-level-statements Lambda that adds two numbers, invoked through the API Gateway emulator. Uses the HTTP API v2 request shape, so the API Gateway emulator runs in `HttpV2` mode.

## Run it

**1. In this directory, set the API Gateway route** the emulator should expose:

```bash
# Linux/macOS
export APIGATEWAY_EMULATOR_ROUTE_CONFIG='{"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}","Endpoint":"http://localhost:5050"}'
```

```powershell
# Windows (PowerShell)
$env:APIGATEWAY_EMULATOR_ROUTE_CONFIG='{"LambdaResourceName":"AddLambdaFunction","HttpMethod":"Get","Path":"/add/{x}/{y}","Endpoint":"http://localhost:5050"}'
```

**2. Start the test tool** (same terminal):

```
dotnet lambda-test-tool start --lambda-emulator-port 5050 --api-gateway-emulator-port 5051 --api-gateway-emulator-mode HttpV2
```

**3. Start the function** (separate terminal, this directory):

```
dotnet run --launch-profile AddLambdaFunction
```

**4. Invoke it:**

```
curl "http://localhost:5051/add/5/3"
# => 8
```

You can also invoke the function directly (without API Gateway) from the web UI that opens at `http://localhost:5050`.
