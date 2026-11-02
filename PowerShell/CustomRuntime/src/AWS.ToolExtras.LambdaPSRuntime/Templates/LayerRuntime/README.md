# PowerShell Lambda runtime layer

This is a buildable PowerShell runtime layer project for the `provided.al2023` runtime, and it is
yours to edit. It holds the `bootstrap` that starts the Runtime API event loop in `runtime.ps1`,
`PowerShellLambdaContext.cs`, and the `pwsh-runtime` helper modules under `modules/`. The pwsh
__PWSH_VERSION__ interpreter is added at build time; no container runtime is needed.

## Customize

- Drop default modules your functions should always see into `modules/`.

## Build and deploy

Build the layer .zip with `Build-LambdaPSRuntimePackage -Path . -OutFile ../layer.zip` (add
`-Architecture arm64` for Graviton), upload it to Amazon S3 (it is larger than Lambda's
direct-upload limit), and publish it with `Publish-LMLayerVersion -LayerName <name>
-CompatibleArchitecture <the architecture you built> -Content_S3Bucket ... -Content_S3Key ...`.
Functions then run on `provided.al2023` with this layer attached. Package a function's folder (its
handler script and any `modules/` it needs) with `Compress-LambdaPSFunctionPackage -Path <folder>
-OutFile ../function.zip`, and create the function with `Publish-LMFunction -FunctionName <name>
-Runtime provided.al2023 -Layer <layer-version-arn> -Handler handler.ps1 -Architecture <the
architecture you built> -Role <exec-role-arn> -Timeout __TIMEOUT__ -MemorySize __MEMORY_SIZE__
-ZipFilename ../function.zip`. Keep the timeout and memory: Lambda's defaults (3 seconds, 128 MB) are
too small for pwsh. A function's architecture must match the layer's: Lambda accepts a mismatch, and
every invoke then fails.
