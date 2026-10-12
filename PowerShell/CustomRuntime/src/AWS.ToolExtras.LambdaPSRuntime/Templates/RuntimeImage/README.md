# PowerShell Lambda customer-managed runtime image

This is a complete, buildable PowerShell runtime image project, and it is yours to edit. It holds a
provided:al2023 `Dockerfile` that installs pwsh (on linux/amd64 from the Microsoft GPG-signed RPM;
on linux/arm64 from the official release tarball, verified against a pinned SHA256) and relocates the
interpreter into `/var/runtime/powershell`, the `bootstrap` that starts the Runtime API event
loop in `runtime.ps1`, `PowerShellLambdaContext.cs` (precompiled to a DLL during the image build), and the
`pwsh-runtime` helper modules under `modules/`.

## Customize

- Edit the `Dockerfile` to add native OS dependencies (extra `dnf install` lines, etc.).
- Drop default modules your functions should always see into `modules/`.
- The interpreter version is baked into the `Dockerfile` (`ARG PWSH_VERSION=__PWSH_VERSION__`).
- To make rebuilds reproducible, pin the base image by digest: `FROM public.ecr.aws/lambda/provided:al2023@sha256:<digest>`
  (`docker buildx imagetools inspect public.ecr.aws/lambda/provided:al2023` prints the current one). A
  pinned base no longer picks up OS patches until you move the digest forward.

## Build and deploy

`New-LambdaPSRuntimeDeployment` builds this image, pushes it to Amazon ECR, publishes it as a
runtime version and points an alias at it. To do the steps yourself:

1. Create the repository with `New-ECRRepository`, and grant Lambda pull access with
   `Set-ECRRepositoryPolicy`: a statement allowing principal `lambda.amazonaws.com` the actions
   `ecr:BatchGetImage` and `ecr:GetDownloadUrlForLayer`, with a `Condition` that
   `aws:SourceAccount` is your account and `aws:SourceArn` is the runtime's ARN
   (`arn:<partition>:lambda:<region>:<account>:runtime/<name>`; the partition is `aws` in most
   regions), so Lambda pulls it for that runtime only. Without the grant, publishing the version fails.
2. Log docker in to the registry with the user name and password from `Get-ECRLoginCommand`.
3. Build and push with `Build-LambdaPSRuntimeImage -Path . -ImageUri <uri>`. Its result's
   `ImageDigest` identifies exactly the image pushed.
4. Publish with `New-LMCRuntime`, `New-LMCRuntimeVersion -ImageUri <registry>/<repository>@<digest>`
   and `New-LMCRuntimeAlias`.
