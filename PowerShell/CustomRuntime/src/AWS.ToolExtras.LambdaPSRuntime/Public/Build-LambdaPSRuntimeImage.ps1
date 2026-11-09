function Build-LambdaPSRuntimeImage {
    <#
    .SYNOPSIS
        Builds a runtime-image or container-function project into a Docker Schema 2 image.
    .DESCRIPTION
        Wraps 'docker buildx build' so callers never have to remember the fragile flags required to
        produce a Lambda-compatible Docker v2 Schema 2 image. It builds a project with a Dockerfile (a
        runtime image from New-LambdaPSRuntimeProject, or a container-image function from
        New-LambdaPSFunctionProject -Target Container) with provenance and SBOM attestations disabled
        and OCI media types turned off, and either loads the image locally or pushes it.

        A Container project's Dockerfile copies the project folder into the image with its hidden files
        (COPY . /var/task, less what .dockerignore lists), so keep secrets such as .env files out of it.

        The image ships the modules in the project's modules/ folder; it does not stage a handler's
        '#Requires -Modules'. Put the modules a container function needs in modules/, with
        Save-LambdaPSModule or New-LambdaPSFunctionProject -IncludeModule.

        The exact recipe issued is:

            docker buildx build --provenance=false --sbom=false --platform <linux/amd64|linux/arm64>
                --load -t <Tag> <Path>                                                 (local)
                --output type=image,oci-mediatypes=false,push=true --metadata-file <temp>
                -t <ImageUri> <Path>                                                   (-ImageUri)

        With -Tag, the image is loaded into the local docker image store under that tag. With
        -ImageUri, it is pushed: tagged ONLY with -ImageUri (a bare local tag would make buildx also
        try to push it to docker.io), and the result reports the pushed manifest's ImageDigest (read
        from buildx's --metadata-file), so the image can be referred to as
        <registry>/<repository>@<digest>. With docker's default builder (the docker driver), the pushed
        image is also stored in the local docker image store under -ImageUri; a docker-container
        builder only pushes it. Remove it with 'docker image rm <ImageUri>' when it is not needed.

        This cmdlet shells out to the docker CLI only and makes no AWS call. It does not create the
        ECR repository, set its policy or log docker in to it: New-LambdaPSRuntimeDeployment does
        all three, or use New-ECRRepository, Set-ECRRepositoryPolicy and Get-ECRLoginCommand.

        -BuildMethod NoDocker (or Auto, when docker or buildx is missing) builds and pushes the image
        without Docker, with the AWS.OCI.Builder library that ships in the module. It builds the recipe
        of the Dockerfile the module scaffolds (see Get-LambdaPSOciImagePlan), so it refuses a project
        whose Dockerfile was edited, and it can only push (-ImageUri). It reads an ECR authorization
        token with Get-ECRAuthorizationToken, honouring -Region, -ProfileName, -ProfileLocation and
        -Credential, instead of using docker login. The image runs with
        DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1, since it has no libicu.

        Errors it can raise, by error ID: ProjectNotFound, DockerfileNotFound, DriveNotFound,
        PathNotFileSystem, PwshArm64HashNotPinned, DockerNotFound and DockerBuildxNotFound (the preflight,
        Test-LambdaPSContainerRuntime), and ExternalToolFailed (docker exited with a non-zero code); for
        a no-Docker build NoDockerLocalBuild, DockerfileModified, PwshX64HashNotPinned (an x86_64 build of a
        pwsh release with no pinned x64 hash), OciBuilderNotFound, OciBuilderConflict
        and OciImagePushFailed.
    .PARAMETER Path
        The project directory containing the Dockerfile. Alias: -ProjectPath.
    .PARAMETER Tag
        The local image tag (for example 'powershell-runtime:v1') to load the image into the local
        docker image store under. Use -Tag or -ImageUri.
    .PARAMETER Architecture
        The target architecture: x86_64 (default, linux/amd64) or arm64 (linux/arm64). arm64 needs a pinned
        SHA256 for the pwsh arm64 tarball in the Dockerfile; a project scaffolded for a pwsh version with
        none is refused with PwshArm64HashNotPinned before docker runs.
    .PARAMETER ImageUri
        The fully-qualified image reference to push the image to (for example an ECR URI). Use
        -ImageUri or -Tag.
    .PARAMETER BuildMethod
        Auto (default): Docker when docker and buildx are installed, else the no-Docker builder (-ImageUri
        only). Docker: always docker buildx. NoDocker: always the no-Docker builder.
    .PARAMETER Region
        No-Docker push only: the region of the ECR token. Defaults to the region in -ImageUri's host.
    .PARAMETER ProfileName
        No-Docker push only: the AWS credential profile to read the ECR token with.
    .PARAMETER ProfileLocation
        No-Docker push only: the credential file that holds -ProfileName.
    .PARAMETER Credential
        No-Docker push only: an Amazon.Runtime.AWSCredentials object to read the ECR token with.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.RuntimeImage

        Tag (a local build) or ImageUri (a push), ImageDigest (the pushed manifest's digest; empty for a
        local build, or when buildx reported none), Architecture, and Pushed. Nothing under -WhatIf.
    .EXAMPLE
        Build-LambdaPSRuntimeImage -Path ./powershell-runtime -Tag powershell-runtime:v1

        Builds the project for x86_64 and loads the image into the local docker store.
    .EXAMPLE
        Build-LambdaPSRuntimeImage -Path ./rt -Architecture arm64 -ImageUri 111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1

        Builds an arm64 image and pushes it to the given ECR URI.
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false, DefaultParameterSetName = 'Local')]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.RuntimeImage')]
    # -Credential is an Amazon.Runtime.AWSCredentials, as on every AWS.Tools cmdlet, not a PSCredential.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUsePSCredentialType', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a PSCredential.')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a password string.')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Alias('ProjectPath')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory, ParameterSetName = 'Local')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Tag,

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture = 'x86_64',

        [Parameter(Mandatory, ParameterSetName = 'Push')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ImageUri,

        [Parameter()]
        [ValidateSet('Auto', 'Docker', 'NoDocker')]
        [string]$BuildMethod = 'Auto',

        [Parameter(ParameterSetName = 'Push')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter(ParameterSetName = 'Push')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileName,

        [Parameter(ParameterSetName = 'Push')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileLocation,

        [Parameter(ParameterSetName = 'Push')]
        [ValidateNotNull()]
        [object]$Credential
    )

    try {
        $push = $PSCmdlet.ParameterSetName -eq 'Push'
        $platform = if ($Architecture -eq 'arm64') { 'linux/arm64' } else { 'linux/amd64' }

        if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.DirectoryNotFoundException]::new("Project directory '$Path' was not found."),
                    'ProjectNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Path))
        }

        $root = Resolve-LambdaPSPath -Path $Path
        $dockerfilePath = [System.IO.Path]::Combine($root, 'Dockerfile')
        if (-not (Test-Path -LiteralPath $dockerfilePath -PathType Leaf)) {
            # A container-free project has no Dockerfile: name the cmdlet that builds it.
            $marker = try { Get-LambdaPSProjectMarker -Path $root } catch { $null }
            $functionManifest = if ($null -eq $marker) { try { Get-LambdaPSFunctionManifest -Path $root } catch { $null } }
            $advice = if ($marker -and @('Layer', 'SelfContainedZip') -contains $marker.Target) {
                " It is a $($marker.Target) project: build it with Build-LambdaPSRuntimePackage."
            }
            elseif ($null -ne $functionManifest) {
                ' It is a function project: package it with Compress-LambdaPSFunctionPackage.'
            }
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.FileNotFoundException]::new("No Dockerfile found in '$Path'.$advice"),
                    'DockerfileNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $dockerfilePath))
        }

        # A version with no pinned arm64 hash is scaffolded amd64-only, with the placeholder left in the
        # Dockerfile; building it for arm64 would only fail inside docker, after the base image pull.
        $dockerfileText = [System.IO.File]::ReadAllText($dockerfilePath)
        if ($Architecture -eq 'arm64' -and $dockerfileText.Contains('__PWSH_ARM64_SHA256__')) {
            $pwshVersion = if ($dockerfileText -match '(?m)^ARG PWSH_VERSION=(\S+)') { $Matches[1] } else { '<version>' }
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        ("The Dockerfile in '$Path' has no SHA256 for the pwsh $pwshVersion arm64 tarball (__PWSH_ARM64_SHA256__ is " +
                         "still a placeholder, because this module pins no arm64 hash for pwsh $pwshVersion), so it builds linux/amd64 " +
                         'only. Build with -Architecture x86_64, scaffold a pwsh version that has a pinned arm64 hash, or replace the ' +
                         "placeholder with the SHA256 of the official powershell-$pwshVersion-linux-arm64.tar.gz.")),
                    'PwshArm64HashNotPinned', [System.Management.Automation.ErrorCategory]::InvalidArgument, $dockerfilePath))
        }

        # The same docker and buildx check as the deploy preflight (DockerNotFound / DockerBuildxNotFound).
        # The build runs the docker program it found, so a docker function or alias cannot stand in for it.
        # Auto falls back to the no-Docker builder only when that check fails and the image is pushed.
        $resolved = Resolve-LambdaPSBuildMethod -BuildMethod $BuildMethod -LocalBuild:(-not $push)
        if ($resolved.Method -eq 'NoDocker') {
            # Every check that reads only local files runs before ShouldProcess, so -WhatIf fails the same way.
            $plan = Get-LambdaPSOciImagePlan -Path $root -Architecture $Architecture
            $tokenRegion = if ($Region) { $Region }
            elseif ($ImageUri -match '\A[0-9]{12}\.dkr\.ecr\.([a-z0-9-]+)\.') { $Matches[1] }
            else {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new("'$ImageUri' is not an Amazon ECR image URI. The no-Docker build pushes to Amazon ECR only.", 'ImageUri'),
                        'NoDockerTargetNotEcr', [System.Management.Automation.ErrorCategory]::InvalidArgument, $ImageUri))
            }
            if (-not $PSCmdlet.ShouldProcess($root, "Build and push image '$ImageUri' without Docker")) {
                return
            }
            $credentialParameter = Get-LambdaPSCredentialParameter -BoundParameters $PSBoundParameters -SessionState $PSCmdlet.SessionState
            $token = Get-LambdaPSEcrAuthorizationToken -Region $tokenRegion -CredentialParameter $credentialParameter
            $digest = Invoke-LambdaPSOciImageBuild -Plan $plan -ImageUri $ImageUri -AuthorizationToken $token
            return [PSCustomObject]@{
                PSTypeName   = 'AWS.ToolExtras.LambdaPSRuntime.RuntimeImage'
                Tag          = $null
                ImageUri     = $ImageUri
                ImageDigest  = $digest
                Architecture = $Architecture
                Pushed       = $true
            }
        }
        $containerRuntime = $resolved.ContainerRuntime

        $dockerArgs = [System.Collections.Generic.List[string]]::new()
        $dockerArgs.Add('buildx')
        $dockerArgs.Add('build')
        $dockerArgs.Add('--provenance=false')
        # No SBOM attestation either, whatever the builder or environment defaults to: an attestation
        # wraps the image in an index with an attestation manifest, so the push would not be the single
        # Docker v2 Schema 2 manifest this recipe produces.
        $dockerArgs.Add('--sbom=false')
        if ($push) {
            # Direct single-step push: type=image + oci-mediatypes=false pushes a Docker v2
            # Schema 2 manifest straight to the registry (the CMR-required format).
            $dockerArgs.Add('--output')
            $dockerArgs.Add('type=image,oci-mediatypes=false,push=true')
            # buildx records the pushed manifest's digest here, so the caller can refer to exactly the
            # image it pushed rather than a tag that can be moved.
            $metadataFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-buildx-$([System.IO.Path]::GetRandomFileName()).json")
            $dockerArgs.Add('--metadata-file')
            $dockerArgs.Add($metadataFile)
        }
        else {
            # Local build: --load (shorthand for --output type=docker) lands a Schema 2 image in
            # the local docker store. It is mutually exclusive with --output type=image.
            $dockerArgs.Add('--load')
        }
        $dockerArgs.Add('--platform')
        $dockerArgs.Add($platform)
        if ($push) {
            # On --push, tag ONLY with the registry-qualified ImageUri. Adding the bare local -t
            # $Tag as well makes buildx try to push it to docker.io/library/<Tag> too, which fails
            # with "denied: requested access to the resource is denied".
            $dockerArgs.Add('-t')
            $dockerArgs.Add($ImageUri)
        }
        else {
            # Local --load build: tag with the local name.
            $dockerArgs.Add('-t')
            $dockerArgs.Add($Tag)
        }
        $dockerArgs.Add($root)

        $action = if ($push) { "Build and push image '$ImageUri'" } else { "Build image '$Tag'" }
        if (-not $PSCmdlet.ShouldProcess($root, $action)) {
            return
        }

        $digest = $null
        try {
            Invoke-LambdaPSExternalTool -Executable $containerRuntime.Path -ArgumentList $dockerArgs.ToArray()
            if ($push -and (Test-Path -LiteralPath $metadataFile -PathType Leaf)) {
                # The image is pushed by now, so metadata buildx wrote in another shape leaves the digest
                # unknown rather than failing the build.
                try { $digest = [string](Get-Content -LiteralPath $metadataFile -Raw | ConvertFrom-Json -ErrorAction Stop).'containerimage.digest' }
                catch { Write-Verbose "Could not read the pushed image's digest from buildx's metadata: $($_.Exception.Message)" }
                if ([string]::IsNullOrWhiteSpace($digest)) { $digest = $null }
            }
        }
        catch {
            # A cross-architecture build fails inside the first RUN step with 'exec format error' when the
            # host has no emulator registered, and docker's exit code alone does not say so. The hint is added
            # only when docker's output says so; when the output is not available, it is worded conditionally,
            # since a cross-architecture build can also fail for unrelated reasons (a failed package install, say).
            $hostPlatform = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'linux/arm64' } else { 'linux/amd64' }
            if ($_.FullyQualifiedErrorId -like 'ExternalToolFailed*' -and $platform -ne $hostPlatform) {
                $output = $_.Exception.Data['Output']
                $emulation = 'on Linux, register QEMU with binfmt_misc (for example: docker run --privileged --rm ' +
                    'tonistiigi/binfmt --install all); Docker Desktop includes emulation.'
                $hint = if ($null -eq $output) {
                    "The build targets $platform on a $hostPlatform host. If docker reported 'exec format error', no " +
                    "emulator is registered for ${platform}: $emulation Otherwise, see the docker output above for the failing step."
                }
                elseif (@($output) -match 'exec format error') {
                    "The build targets $platform on a $hostPlatform host and docker reported 'exec format error', so no " +
                    "emulator is registered for ${platform}: $emulation"
                }
                if ($hint) {
                    throw [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new("$($_.Exception.Message) $hint", $_.Exception),
                        'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, 'docker')
                }
            }
            throw
        }
        finally {
            if ($push) { Remove-Item -LiteralPath $metadataFile -Force -ErrorAction SilentlyContinue }
        }

        [PSCustomObject]@{
            PSTypeName   = 'AWS.ToolExtras.LambdaPSRuntime.RuntimeImage'
            Tag          = if ($push) { $null } else { $Tag }
            ImageUri     = if ($push) { $ImageUri } else { $null }
            ImageDigest  = $digest
            Architecture = $Architecture
            Pushed       = [bool]$push
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
