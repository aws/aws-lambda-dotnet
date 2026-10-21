# PowerShell Lambda container-image function

This is a buildable PowerShell function packaged as a container image, and it is yours to edit. It
holds a provided:al2023 `Dockerfile` that installs pwsh (on linux/amd64 from the Microsoft
GPG-signed RPM; on linux/arm64 from the official release tarball, verified against a pinned SHA256),
the handler `handler.ps1`, the `bootstrap` that starts the Runtime API event loop in
`runtime.ps1`, the image's entrypoint `lambda-entrypoint.sh` (it sets the handler from the image's
`CMD` and runs the `bootstrap`), `PowerShellLambdaContext.cs`,
and the `pwsh-runtime` helper modules under `modules/`.

## Customize

- Edit `handler.ps1`. It receives `param($LambdaInput, $LambdaContext)`.
- Put scripts and data files the handler uses beside it, in this folder or a subfolder: the image
  copies the whole project into `/var/task`, except what `.dockerignore` lists (this README, the
  `Dockerfile`, `lambdaps-runtime.json`, `.dockerignore` itself, and `.git`, `.gitignore` and `.zip`
  files in any folder). Add anything else that must not ship to `.dockerignore`.
- Edit the `Dockerfile` to add native OS dependencies.
- Drop modules the function needs into `modules/`; they are copied into the image.
- To make rebuilds reproducible, pin the base image by digest: `FROM public.ecr.aws/lambda/provided:al2023@sha256:<digest>`
  (`docker buildx imagetools inspect public.ecr.aws/lambda/provided:al2023` prints the current one). A
  pinned base no longer picks up OS patches until you move the digest forward.

## Build and deploy

Create an Amazon ECR repository (`New-ECRRepository`), log docker in to it with the user name and
password from `Get-ECRLoginCommand`, and build and push the image with
`Build-LambdaPSRuntimeImage -Path . -ImageUri <uri>` (add `-Architecture arm64` for
Graviton). Then create the function with `Publish-LMFunction -FunctionName <name> -PackageType Image
-Code_ImageUri <uri> -Architecture <the architecture you built> -Role <exec-role-arn> -Timeout
__TIMEOUT__ -MemorySize __MEMORY_SIZE__`. Keep the timeout and memory: Lambda's defaults (3 seconds,
128 MB) are too small for pwsh. The architecture must match the build: Lambda accepts a mismatch,
and every invoke then fails.
