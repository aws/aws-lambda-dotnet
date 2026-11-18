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

            docker buildx build --provenance=false --sbom=false [--pull] --platform <linux/amd64|linux/arm64>
                --load -t <Tag> <Path>                                                 (local)
                --output type=image,oci-mediatypes=false,push=true --metadata-file <temp>
                -t <ImageUri> <Path>                                                   (-ImageUri)

        With -Tag, the image is loaded into the local docker image store under that tag. With
        -ImageUri, it is pushed: tagged ONLY with -ImageUri (a bare local tag would make buildx also
        try to push it to docker.io), and the result reports the pushed manifest's ImageDigest (read
        from buildx's --metadata-file), so the image can be referred to as
        <registry>/<repository>@<digest>. With docker's default builder (the docker driver), the pushed
        image is also stored in the local docker image store under -ImageUri; with a docker-container
        builder, the reserved-path check below pulls it there. Remove it with
        'docker image rm <ImageUri>' when it is not needed.

        After the build, a runtime image (lambdaps-runtime.json Target CMR) is checked for files in
        /opt, /var/task and /tmp, which Lambda hides at run time, with 'docker run --rm --network none
        --user 0 --entrypoint /bin/sh'. A pushed image is pulled again and checked after the push. When
        the check cannot run (for example an image with no /bin/sh, ls, wc, head or tr), it warns and
        the build succeeds.

        This cmdlet shells out to the docker CLI only and makes no AWS call. It does not create the
        ECR repository, set its policy or log docker in to it: New-LambdaPSRuntimeDeployment does
        all three, or use New-ECRRepository, Set-ECRRepositoryPolicy and Get-ECRLoginCommand.

        -BuildMethod NoDocker (or Auto, when docker or buildx is missing) builds and pushes the image
        without Docker, with the AWS.OCI.Builder library that ships in the module. It builds the recipe
        of the Dockerfile the module scaffolds (see Get-LambdaPSOciImagePlan), so it refuses a project
        whose Dockerfile was edited, and it can only push (-ImageUri). It reads an ECR authorization
        token with Get-ECRAuthorizationToken, honouring -Region, -ProfileName, -ProfileLocation and
        -Credential, instead of using docker login. It adds Microsoft's ICU libraries next to pwsh, so
        cultures work as in the Docker image, which installs libicu.

        Errors it can raise, by error ID: ProjectNotFound, DockerfileNotFound, DriveNotFound,
        PathNotFileSystem, PwshArm64HashNotPinned, DockerNotFound, DockerBuildxNotFound and
        DockerDaemonNotRunning (the preflight, Test-LambdaPSContainerRuntime), ExternalToolFailed (docker exited with a non-zero code),
        and ReservedPathNotEmpty (the built runtime image has files in /opt, /var/task or /tmp); for
        a no-Docker build NoDockerLocalBuild, DockerfileModified, PwshX64HashNotPinned (an x86_64 build of a
        pwsh release with no pinned x64 hash), IcuPackageHashMismatch, OciBuilderNotFound, OciBuilderConflict
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
        Auto (default): Docker when docker and buildx are installed and the daemon answers, else the
        no-Docker builder (-ImageUri only). It also uses the no-Docker builder when docker's buildx builder
        lists no -Architecture platform, for example with no emulator registered, and says so on the
        Information stream (tag LambdaPSBuild) unless -InformationAction is given. Docker: always docker buildx. NoDocker: always the no-Docker builder.
    .PARAMETER Region
        No-Docker push only: the region of the ECR token. Defaults to the region in -ImageUri's host.
    .PARAMETER ProfileName
        No-Docker push only: the AWS credential profile to read the ECR token with.
    .PARAMETER ProfileLocation
        No-Docker push only: the credential file that holds -ProfileName.
    .PARAMETER Credential
        No-Docker push only: an Amazon.Runtime.AWSCredentials object to read the ECR token with.
    .PARAMETER Pull
        Passes --pull, so docker refreshes the base image from its registry rather than building from
        a copy it already has. Off by default, as with docker build. A no-Docker build always reads the
        base image from its registry, so it ignores -Pull.
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
        [object]$Credential,

        [switch]$Pull
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
        $resolved = Resolve-LambdaPSBuildMethod -BuildMethod $BuildMethod -Architecture $Architecture -LocalBuild:(-not $push)
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
            if ($BuildMethod -eq 'Auto') {
                # Shown by default, since the image differs from a Docker build; an -InformationAction the caller gives still applies.
                $shownInformation = @{}
                if (-not $PSBoundParameters.ContainsKey('InformationAction')) { $shownInformation.InformationAction = 'Continue' }
                Write-Information 'Building without Docker (AWS.OCI.Builder); -Verbose says why.' -Tags 'LambdaPSBuild' @shownInformation
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
        if ($Pull) { $dockerArgs.Add('--pull') }
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
                if ($push) { $emulation += ' Or build with -BuildMethod NoDocker, which needs no emulator.' }
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

        # RUN steps can't be judged from the Dockerfile's text, so list the built image instead. Only a
        # project marked as a runtime image is checked: a function image's code belongs in /var/task.
        $marker = try { Get-LambdaPSProjectMarker -Path $root } catch { $null }
        if ($marker -and $marker.Target -eq 'CMR') {
            $imageRef = if (-not $push) { $Tag } elseif ($digest) { "$ImageUri@$digest" } else { $ImageUri }
            $listScript = 'for t in ls wc head tr; do command -v "$t" >/dev/null || { echo "$t not found in the image"; exit 4; }; done; rc=0; for d in /opt /var/task /tmp; do n=$(ls -A "$d" 2>/dev/null | wc -l); ' +
                'if [ "$n" -gt 0 ]; then echo "LAMBDAPS_RESERVED $d ($n entries): $(ls -A "$d" | head -n 5 | tr "\n" " ")"; rc=3; fi; done; exit $rc'
            # As root, so an image's USER can't hide a folder it can't read. A push is checked from the
            # registry by the digest it returns, so neither a local copy nor a moved tag stands in for it.
            $pullPolicy = if ($push) { 'always' } else { 'never' }
            $listArgs = @('run', '--rm', '--pull', $pullPolicy, '--network', 'none', '--user', '0', '--platform', $platform, '--entrypoint', '/bin/sh', $imageRef, '-c', $listScript)
            try { Invoke-LambdaPSExternalTool -Executable $containerRuntime.Path -ArgumentList $listArgs }
            catch {
                $found = @($_.Exception.Data['Output'] | Where-Object { $_ -like 'LAMBDAPS_RESERVED *' } | ForEach-Object { $_.Substring(18).Trim() })
                if ($found.Count -eq 0) {
                    $reason = @($_.Exception.Data['Output'])[-1]
                    if (-not $reason) { $reason = $_.Exception.Message }
                    Write-Warning "Could not list /opt, /var/task and /tmp in image '$imageRef' to check that they are empty: $reason"
                }
                else {
                    $pushedNote = if ($push) { " The image is already pushed to '$ImageUri': don't publish it as a runtime version." } else { '' }
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.InvalidOperationException]::new(
                                ("Image '$imageRef' has files in $($found -join '; '). Lambda mounts the function's code over /var/task " +
                                 'and its layers over /opt, and starts each execution environment with an empty /tmp, so the runtime ' +
                                 "can't use them. Install the files elsewhere (for example under /var/runtime) or remove them in the " +
                                 "Dockerfile.$pushedNote")),
                            'ReservedPathNotEmpty', [System.Management.Automation.ErrorCategory]::InvalidData, $imageRef))
                }
            }
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
