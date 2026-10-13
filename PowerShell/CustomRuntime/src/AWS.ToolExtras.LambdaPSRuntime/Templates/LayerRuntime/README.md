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
Functions then use runtime `provided.al2023` with this layer attached, their script name as the
handler, and a timeout and memory that fit pwsh (Lambda's defaults, 3 seconds and 128 MB, are too
small): `Publish-LMFunction -Runtime provided.al2023 -Layer <layer-version-arn> -Handler handler.ps1
-Timeout __TIMEOUT__ -MemorySize __MEMORY_SIZE__`.
A function's architecture must match the layer's: Lambda accepts a mismatch, and every invoke then
fails.
