# PowerShell Lambda self-contained function package

This is a buildable PowerShell function for the `provided.al2023` runtime that carries its own
interpreter, and it is yours to edit. It holds the handler `handler.ps1`, the `bootstrap` that
starts the Runtime API event loop in `runtime.ps1`, `PowerShellLambdaContext.cs`, and the `pwsh-runtime`
helper modules under `modules/`. The pwsh __PWSH_VERSION__ interpreter is added at build time; no
container runtime is needed.

## Customize

- Edit `handler.ps1`. It receives `param($LambdaInput, $LambdaContext)`.
- Drop modules the function needs into `modules/`. `Build-LambdaPSRuntimePackage` also stages each
  module the handler names in `#Requires -Modules`, with its dependencies, except PowerShell's own
  modules. Each one must pin a `RequiredVersion`, even when it is already in `modules/`.

## Build and deploy

Build the function .zip with `Build-LambdaPSRuntimePackage -Path . -OutFile ../function.zip` (add
`-Architecture arm64` for Graviton), upload it to Amazon S3 (it is larger than Lambda's
direct-upload limit), and create the function with `Publish-LMFunction -FunctionName <name>
-Runtime provided.al2023 -Handler handler.ps1 -Architecture <the architecture you built> -Role
<exec-role-arn> -Timeout __TIMEOUT__ -MemorySize __MEMORY_SIZE__ -Code_S3Bucket <bucket> -Code_S3Key
<key>`. Keep the timeout and memory: Lambda's defaults (3 seconds, 128 MB) are too small for pwsh.
The architecture must match the build: Lambda accepts a mismatch, and every invoke then fails.
