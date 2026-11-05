function New-LambdaPSRuntimeDeployment {
    <#
    .SYNOPSIS
        Scaffolds and builds a PowerShell runtime image, pushes it to ECR, publishes it as a
        customer-managed runtime version, and points a runtime alias at it, in one command.
    .DESCRIPTION
        Runs the full deploy flow end to end: preflight the Docker build toolchain (docker + buildx,
        via Test-LambdaPSContainerRuntime) and the runtime (see below), scaffold a runtime-image
        project (via New-LambdaPSRuntimeProject, forwarding -IncludeModule so the scaffolder stages a
        version-pinned set of modules into it), build the image locally, ensure the ECR repository
        and a Lambda pull grant scoped to this runtime (aws:SourceAccount and aws:SourceArn), push the
        image (via Build-LambdaPSRuntimeImage), register the customer-managed runtime and publish the
        version by the pushed image's digest, wait for the version to reach ACTIVE, and point an
        alias at it. A version that does not reach ACTIVE is a terminating error, so the cmdlet never
        reports success over a runtime that cannot be invoked.

        Docker is the only supported container CLI: the build depends on buildx flags that have no
        equivalent in podman, finch, or nerdctl. The Layer and self-contained .zip targets are the
        container-free path (New-LambdaPSRuntimeProject -Target Layer, Build-LambdaPSRuntimePackage).

        The AWS calls are made with AWS.Tools cmdlets. Read-only: Get-STSCallerIdentity,
        Get-ECRImageMetadata, Get-ECRRepository, Get-ECRRepositoryPolicy, Get-ECRLoginCommand,
        Get-LMCRuntime, Get-LMCRuntimeVersion and Get-LMCRuntimeAlias. State-changing:
        New-ECRRepository (when the repository does not exist, with immutable tags; an existing
        repository's tag mutability is left as it is), Set-ECRRepositoryPolicy (for the pull grant),
        New-LMCRuntime (when the runtime does not exist), New-LMCRuntimeVersion, and New-LMCRuntimeAlias or
        Update-LMCRuntimeAlias.

        -RepositoryName is optional and defaults to -RuntimeName. An ECR repository name is stricter
        than a runtime name: it refuses names where '.' touches another separator, '_' touches '-', or
        three or more '_' run together, for example 'my..runtime'. A runtime name that is not a valid
        ECR repository name fails with a request for an explicit -RepositoryName, rather than being
        silently altered.

        Module staging is delegated entirely to the scaffolder: this cmdlet forwards -IncludeModule to
        New-LambdaPSRuntimeProject, which stages the requested modules (with transitive dependencies),
        downloading any that are not installed locally unless -MissingModuleAction is Stop. Module
        versions are never resolved implicitly: every -IncludeModule spec must pin an exact
        RequiredVersion, so a deployment is reproducible.

        Before anything is created, a read-only runtime preflight looks the runtime up. Where
        customer-managed runtimes are not offered (or the caller lacks lambda:GetRuntime) the service
        refuses with AccessDeniedException, and the deploy stops with RuntimeServiceAccessDenied. If the
        runtime already has -RuntimeVersion, the deploy stops with RuntimeVersionExists: versions are
        immutable, and pushing first would move the <RuntimeVersion> tag off the image that version
        runs. (The version is published by digest, so it keeps running, but an ECR lifecycle rule
        that expires untagged images could then delete its image.) If the ECR repository already has
        an image tagged <RuntimeVersion> (left by a run that pushed but did not publish, or used by
        another runtime sharing -RepositoryName), the deploy stops with RuntimeImageTagExists, so no
        tag is pushed over; a repository that cannot be read stops it with RuntimeImageTagCheckFailed.
        A version that cannot be read for any reason other than not existing stops the deploy with
        RuntimeVersionCheckFailed, rather than being taken as new. For a new version it then reads
        -RuntimeAlias, to warn when the deploy moves it off a version that supports an architecture
        the new version does not. A missing alias is created by the deploy; any other failed read, such as a refused
        lambda:GetRuntimeAlias, stops the deploy with RuntimeAliasCheckFailed. The image is built
        before the ECR repository is ensured, so a failed build leaves nothing in AWS; the push reuses
        the local build's cache. The local build leaves an image tagged
        <RepositoryName>:<RuntimeVersion> in the local docker store, and with docker's default builder
        the push also stores it there under its ECR image URI.

        Each state-changing stage is guarded by ShouldProcess, so -WhatIf previews the plan (the
        project path, the composed ECR image URI, the version and the alias) without creating or
        changing anything, and -Confirm asks once per stage: the steps inside a confirmed stage do
        not ask again, and declining a stage skips the stages after it, which need its output. The
        read-only preflights run under -WhatIf too, so a deploy that would fail on them fails there
        as well.

        Progress lines (the Docker toolchain found, the target image URI, and each stage as it
        completes) are written to the Information stream with the tag LambdaPSDeploy, so they are
        shown only with -InformationAction Continue; -InformationVariable collects them.

        Besides parameter validation, the deploy can stop with these errors, by stage:
          inputs and preflight  InvalidRepositoryName, ProfileNotFound, RegionNotResolved,
                                UnknownPwshVersion, PwshVersionMismatch, DriveNotFound and
                                PathNotFileSystem (for -ProjectPath), TargetIsFile, TargetNotEmpty,
                                TargetNotReadable (a -ProjectPath that cannot be listed),
                                InvalidProjectMarker, DockerNotFound, DockerBuildxNotFound,
                                RuntimeServiceAccessDenied, RuntimeVersionCheckFailed,
                                RuntimeVersionExists, RuntimeImageTagExists, RuntimeImageTagCheckFailed,
                                RuntimeAliasCheckFailed, PwshArm64HashNotPinned
                                (-Architecture arm64 for a pwsh version with no pinned arm64 hash)
          scaffold and modules  TargetOwnedByAnotherUser, TemplateNotFound,
                                AWSPowerShellModuleNotSupported (AWSPowerShell or
                                AWSPowerShell.NetCore in -IncludeModule), ModuleNameInvalid (an
                                -IncludeModule name that is a wildcard or a path),
                                ModuleVersionRequired, ModuleVersionSpelling, ModuleVersionConflict,
                                DependencyNameMissing, DependencyNameIsPath, DependencyVersionUnpinned,
                                ModuleClosureNotSettled, ModuleNotFoundLocally,
                                ModuleRepositoryNotFound, ModuleDownloadFailed, ModuleFolderIncomplete
                                (a module version folder with no <Name>.psd1), ModuleGuidMismatch (the
                                module's name and version exist only with another GUID than the one
                                -IncludeModule names),
                                ModuleInstallerNotFound, AWSToolsInstallerImportFailed
          build and push        ExternalToolFailed, EcrDockerLoginFailed
          publish               RuntimeVersionNotActive, RuntimeVersionTimeout, RuntimeVersionWaitFailed
                                (the new version's state could not be read),
                                RuntimeImageArchitectureMismatch
        A call the service refuses at another point surfaces the AWS.Tools error.
    .PARAMETER RuntimeName
        The customer-managed runtime name to create or reuse: lowercase letters, digits, '_', '.' and
        '-', 2 to 128 characters, starting and ending with a letter or digit.
    .PARAMETER PowerShellVersion
        The PowerShell version the runtime image targets, in major.minor form (for example 7.4).
    .PARAMETER PwshVersion
        The full pwsh version to install (x.y.z, for example 7.4.20), forwarded to
        New-LambdaPSRuntimeProject. When omitted, the default release for -PowerShellVersion is used.
    .PARAMETER IncludeModule
        Modules to bake into the runtime, forwarded to New-LambdaPSRuntimeProject (with transitive
        dependencies). Accepts a list. Each specification must pin an exact RequiredVersion (for
        example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }); minimum-version and
        version-range specs are rejected. Versions are never resolved implicitly. AWSPowerShell (the
        Windows PowerShell module, which doesn't run in PowerShell 7) and AWSPowerShell.NetCore (every
        AWS service, over 600 MB) fail with AWSPowerShellModuleNotSupported; name the AWS.Tools.*
        modules for the services you use instead. Alias: -Module.
    .PARAMETER MissingModuleAction
        What to do with a module (or dependency) that is not available locally, forwarded to
        New-LambdaPSRuntimeProject: Save (default; download into a temporary folder that is deleted
        afterwards), Install (also install it for the current user), or Stop (fail with
        ModuleNotFoundLocally). See Save-LambdaPSModule.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules from, in order (for example
        an internal mirror). When omitted, every registered repository is searched in priority order,
        so a same-named package in a higher-priority feed would be used. AWS.Tools modules always come
        from the AWS.Tools.zip that AWS.Tools.Installer downloads; when the installer itself must be
        fetched, it comes from these repositories (PSGallery when this is omitted) and, on Windows, only
        when its Authenticode signature is valid. Every name, or pattern with `*` wildcards, must match
        a registered PSResourceGet repository, or the deploy stops with ModuleRepositoryNotFound before
        anything is downloaded.
    .PARAMETER Region
        The AWS region to deploy into. When omitted, the region AWS.Tools would use: the shell's
        default (Set-DefaultAWSRegion, including one a calling script set for itself), the
        -ProfileName profile's region, the default profile's region, AWS_REGION, the AWS_PROFILE
        profile's region, or the instance or container's.
        AWS_DEFAULT_REGION is not read. With none of those it fails with RegionNotResolved. Region
        names are lowercase, so a region given in another case (US-EAST-1) is used as us-east-1.
    .PARAMETER ProfileName
        The name of the AWS credential profile to use, as for any AWS.Tools cmdlet. When no credential
        parameter is given, credentials set with Set-AWSCredential are used, including ones a calling
        script set for itself; otherwise AWS.Tools resolves them the usual way: environment variables,
        the default profile, or the instance or container role. A profile that is not found (in
        -ProfileLocation, or the default credential stores) fails with ProfileNotFound before any AWS
        call.
    .PARAMETER ProfileLocation
        The location of the shared credentials file, as for any AWS.Tools cmdlet. Used with
        -ProfileName.
    .PARAMETER Credential
        An AWSCredentials object to use, as for any AWS.Tools cmdlet (for example from
        Get-AWSCredential, or (Use-STSRole ...).Credentials).
    .PARAMETER RepositoryName
        The ECR repository name to push the runtime image to. Optional; defaults to -RuntimeName. If
        the runtime name is not a valid ECR repository name, the deploy fails asking for an explicit
        -RepositoryName rather than altering the name.
    .PARAMETER RuntimeAlias
        The runtime alias to create or repoint. Defaults to 'default', matching the alias
        New-LambdaPSFunctionDeployment binds to. ASCII letters, digits, '_', '.' and '-', up to 128
        characters, not starting or ending with '.' or '-'. Alias names are case-sensitive.
    .PARAMETER RuntimeVersion
        The runtime version label / image tag to publish. Defaults to 'v1'. It must be valid as both
        a runtime version name and an image tag: ASCII letters, digits, '_', '.' and '-', up to 128
        characters, not starting or ending with '.' or '-'. Version names are case-sensitive: 'V1' and
        'v1' are different versions.
    .PARAMETER Architecture
        The target architecture: x86_64 (default, mapped to linux/amd64) or arm64 (linux/arm64). The
        image is built for that platform, and after publishing, the architecture the service reads
        from the image is checked against it: a mismatch (for example a builder that cannot emit
        linux/arm64) stops the deploy before the alias is moved. New-LambdaPSFunctionDeployment reads
        the same architecture from the alias, adopts it when -Architecture is not given, and refuses a
        mismatch -- Lambda also rejects a mismatched function when it is created, but checking first
        means the error names the runtime alias.
        When -RuntimeAlias already points at a version that supports an architecture the new version
        does not, the alias is still moved, with a warning given before anything is created or
        confirmed, and under -WhatIf: functions bound to it that were built for that architecture fail
        to invoke from their next cold start until they are redeployed.
    .PARAMETER ProjectPath
        Where to scaffold the runtime-image project (alias -Path). Defaults to ./<RuntimeName>-runtime
        under the current location. NOTE: this directory is REPLACED. If it is a project this cmdlet
        scaffolded before (it has a lambdaps-runtime.json marker with Target CMR), its contents are
        deleted before the project is written, so keep customizations elsewhere. Any other non-empty
        directory is refused with TargetNotEmpty and left untouched. Use -WhatIf to see what would be
        replaced.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.RuntimeDeployment

        RuntimeArn is the runtime's ARN, to bind functions to. PwshVersion is the pwsh release the
        image installs. ImageDigest is the digest of the pushed image, which the version is published
        by (empty, with a warning, when buildx reports none and the version is published by tag).
        ModulesStaged lists every module baked into the runtime image, as
        AWS.ToolExtras.LambdaPSRuntime.StagedModule objects (Name, and Version as a [version]),
        including the dependencies followed from -IncludeModule. RuntimeArn, ImageDigest and
        ModulesStaged are empty under -WhatIf, and for the stages skipped when one is declined under
        -Confirm; the object is still returned. RuntimeAlias is the alias requested: it was moved only
        when its stage ran, and declining that stage alone is warned about with the command that points it.
    .EXAMPLE
        New-LambdaPSRuntimeDeployment -RuntimeName my-powershell-runtime -PowerShellVersion 7.6 -Region us-east-1 -InformationAction Continue

        Runs the full flow with the ECR repository defaulted to the runtime name, alias 'default',
        version 'v1', and x86_64 architecture, showing a progress line as each stage completes.
    .EXAMPLE
        New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.6 -Region us-east-1 -RepositoryName rt -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -Architecture arm64 -WhatIf

        Previews an arm64 deployment to the 'rt' repository that would bake AWS.Tools.S3 5.0.274 in,
        without changing anything.
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.RuntimeDeployment')]
    # -Credential is an Amazon.Runtime.AWSCredentials, as on every AWS.Tools cmdlet, not a PSCredential
    # or a password. It is typed [object] so the module binds no SDK type, and AWS.Tools validates it.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUsePSCredentialType', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a PSCredential.')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingPlainTextForPassword', '',
        Justification = 'AWS.Tools -Credential takes an AWSCredentials object, not a password string.')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [ValidateNotNullOrWhiteSpace()]
        # Options = 'None': ValidatePattern ignores case by default, and runtime names are lowercase.
        [ValidatePattern('^[a-z0-9](?:[a-z0-9_.-]{0,126})?[a-z0-9]\z', Options = 'None', ErrorMessage = "'{0}' is not a valid runtime name (lowercase letters, digits, '_', '.' and '-', 2 to 128 characters, starting and ending with a letter or digit).")]
        [string]$RuntimeName,

        [Parameter(Mandatory)]
        [ValidatePattern('\A[0-9]+\.[0-9]+\z', ErrorMessage = "'{0}' is not a PowerShell version in major.minor form, for example 7.4.")]
        [string]$PowerShellVersion,

        [Parameter()]
        [ValidatePattern('\A[0-9]+\.[0-9]+\.[0-9]+\z', ErrorMessage = "'{0}' is not a pwsh release in x.y.z form, for example 7.4.20.")]
        [string]$PwshVersion,

        [Parameter()]
        [Alias('Module')]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$IncludeModule = @(),

        [Parameter()]
        [ValidateSet('Save', 'Install', 'Stop')]
        [string]$MissingModuleAction = 'Save',

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string[]]$ModuleRepository,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Region,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileName,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProfileLocation,

        [Parameter()]
        [ValidateNotNull()]
        [object]$Credential,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RepositoryName,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        # Options = 'None': ignoring case, U+212A KELVIN SIGN would match 'k', which the service refuses.
        [ValidatePattern('^[A-Za-z0-9_](?:[A-Za-z0-9_.-]{0,126}[A-Za-z0-9_])?\z', Options = 'None', ErrorMessage = "'{0}' is not a valid runtime alias name (letters, digits, '_', '.' and '-', up to 128 characters, not starting or ending with '.' or '-').")]
        [string]$RuntimeAlias = 'default',

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        # Options = 'None': ignoring case, U+212A KELVIN SIGN would match 'k', which the service refuses.
        [ValidatePattern('^[A-Za-z0-9_](?:[A-Za-z0-9_.-]{0,126}[A-Za-z0-9_])?\z', Options = 'None', ErrorMessage = "'{0}' is not a valid runtime version name and image tag (letters, digits, '_', '.' and '-', up to 128 characters, not starting or ending with '.' or '-').")]
        [string]$RuntimeVersion = 'v1',

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture = 'x86_64',

        [Parameter()]
        [Alias('Path')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$ProjectPath
    )

    # AWS is reached only through AWS.Tools cmdlets: the state-changing calls are wrapped in small
    # private seams (Initialize-LambdaPSEcrRepository, Publish-LambdaPSRuntimeVersion,
    # Set-LambdaPSRuntimeAlias) and the read-only preflight calls the cmdlets directly, so the whole
    # flow is unit testable offline. No AWS SDK type is bound directly.
    try {
        # Credential parameters the caller gave, passed to every seam that calls AWS.
        $credentialParameter = Get-LambdaPSCredentialParameter -BoundParameters $PSBoundParameters -SessionState $PSCmdlet.SessionState
        if (-not $PSBoundParameters.ContainsKey('Region')) {
            $Region = Resolve-LambdaPSRegion -CredentialParameter $credentialParameter -SessionState $PSCmdlet.SessionState
        }
        # AWS region names are lowercase. The ECR pull grant is matched against the runtime ARN Lambda
        # sends, which carries the lowercase name, so a region given in another case is lowercased here,
        # before it is used in any ARN, image URI or call.
        $Region = $Region.ToLowerInvariant()
        # Resolve pwsh first: an unmapped -PowerShellVersion fails here, before any toolchain or AWS check.
        $resolvedPwshVersion = Resolve-LambdaPSPwshVersion -PowerShellVersion $PowerShellVersion -PwshVersion $PwshVersion
        # An arm64 image installs pwsh from a tarball checked against a pinned SHA256. Without one the
        # build can only fail, after Stage 1 has replaced -ProjectPath, so refuse here, under -WhatIf too.
        if ($Architecture -eq 'arm64' -and [string]::IsNullOrWhiteSpace((Get-LambdaPSPwshArchiveHash -PwshVersion $resolvedPwshVersion -Architecture arm64))) {
            # Only versions of the same line: -PowerShellVersion refuses any other with PwshVersionMismatch.
            $pinnedArm64 = @($script:Config.pwsh.ArchiveSha256.Keys | Where-Object {
                    $_.StartsWith("$PowerShellVersion.") -and
                    -not [string]::IsNullOrWhiteSpace((Get-LambdaPSPwshArchiveHash -PwshVersion $_ -Architecture arm64)) } |
                Sort-Object { [version]$_ })
            $pinnedList = if ($pinnedArm64.Count -gt 0) { " ($($pinnedArm64 -join ', '))" } else { '' }
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        ("pwsh $resolvedPwshVersion has no pinned SHA256 for its arm64 tarball, so an arm64 runtime " +
                         "image cannot be built for it. Use -Architecture x86_64, or a -PwshVersion with a pinned " +
                         "arm64 hash$pinnedList. Nothing was created.")),
                    'PwshArm64HashNotPinned', [System.Management.Automation.ErrorCategory]::InvalidArgument, $resolvedPwshVersion))
        }
        if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
            $ProjectPath = [System.IO.Path]::Combine((Get-Location).Path, "$RuntimeName-runtime")
        }
        # A full path, so the result still names the project after a Set-Location.
        $ProjectPath = Resolve-LambdaPSPath -Path $ProjectPath

        # Resolve the ECR repository: an explicit -RepositoryName wins; otherwise default to the runtime name.
        # A defaulted name that is not a valid ECR repository name is a hard error asking for an explicit
        # -RepositoryName, so the name is never silently altered.
        $repositoryDefaulted = -not $PSBoundParameters.ContainsKey('RepositoryName')
        $effectiveRepository = if ($repositoryDefaulted) { $RuntimeName } else { $RepositoryName }
        if (-not (Test-LambdaPSEcrRepositoryName -Name $effectiveRepository)) {
            $message = if ($repositoryDefaulted) {
                "Runtime name '$RuntimeName' is not a valid ECR repository name, so it cannot be used as the default repository. Pass an explicit -RepositoryName (lowercase letters and digits, 2-256 characters, in '/'-separated parts where letters or digits are joined by one '.', one '_', two '_', or any number of '-')."
            }
            else {
                "Repository '$RepositoryName' is not a valid ECR repository name (lowercase letters and digits, 2-256 characters, in '/'-separated parts where letters or digits are joined by one '.', one '_', two '_', or any number of '-')."
            }
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new($message, 'RepositoryName'),
                    'InvalidRepositoryName', [System.Management.Automation.ErrorCategory]::InvalidArgument, $effectiveRepository))
        }

        # Stage 1 replaces -ProjectPath, deleting what is in it. Only a runtime-image project this module
        # scaffolded (a lambdaps-runtime.json marker with Target CMR) is replaced; a file, or any other
        # non-empty directory (including a function or layer project), is refused here, before anything
        # is read from or created in AWS.
        if (Test-Path -LiteralPath $ProjectPath -PathType Leaf) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.IOException]::new("-ProjectPath '$ProjectPath' is a file, not a directory. Nothing was created."),
                    'TargetIsFile', [System.Management.Automation.ErrorCategory]::InvalidArgument, $ProjectPath))
        }
        if (Test-Path -LiteralPath $ProjectPath -PathType Container) {
            # A folder that cannot be listed is not an empty one; refuse it here, before any AWS call,
            # as the scaffolder (Assert-LambdaPSScaffoldPath) would.
            try { $existingItems = @(Get-ChildItem -LiteralPath $ProjectPath -Force -ErrorAction Stop) }
            catch {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.UnauthorizedAccessException]::new(
                            ("Target '$ProjectPath' cannot be listed, so it cannot be checked for files the scaffold " +
                             "would write over: $($_.Exception.Message) Choose a directory you can read."), $_.Exception),
                        'TargetNotReadable', [System.Management.Automation.ErrorCategory]::PermissionDenied, $ProjectPath))
            }
            $existingMarker = Get-LambdaPSProjectMarker -Path $ProjectPath
            if ($null -ne $existingMarker -and [string]::IsNullOrWhiteSpace([string]$existingMarker.Target)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.IOException]::new(
                            ("-ProjectPath '$ProjectPath' is not a runtime project scaffolded by this module (its " +
                             'lambdaps-runtime.json names no target), so it was not replaced. Pass an empty or new ' +
                             'directory, or remove its contents first. Nothing was created.')),
                        'TargetNotEmpty', [System.Management.Automation.ErrorCategory]::ResourceExists, $ProjectPath))
            }
            if ($null -ne $existingMarker -and $existingMarker.Target -ne 'CMR') {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.IOException]::new(
                            ("-ProjectPath '$ProjectPath' holds a '$($existingMarker.Target)' project, not a runtime " +
                             'image project, so it was not replaced. Pass an empty or new directory. Nothing was created.')),
                        'TargetNotEmpty', [System.Management.Automation.ErrorCategory]::ResourceExists, $ProjectPath))
            }
            if ($null -eq $existingMarker -and $existingItems.Count -gt 0) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.IOException]::new(
                            ("-ProjectPath '$ProjectPath' is not empty and is not a runtime project scaffolded by " +
                             'this module (it has no lambdaps-runtime.json), so it was not replaced. Pass an empty ' +
                             'or new directory, or remove its contents first. Nothing was created.')),
                        'TargetNotEmpty', [System.Management.Automation.ErrorCategory]::ResourceExists, $ProjectPath))
            }
        }

        # Preflight (read-only; always runs, even under -WhatIf).
        Write-Verbose 'Preflight: check the Docker build toolchain and the runtime.'
        $runtimeProbe = Test-LambdaPSContainerRuntime
        Write-Information "Preflight: using '$($runtimeProbe.Name)' at '$($runtimeProbe.Path)' ($($runtimeProbe.BuildxVersion))." -Tags 'LambdaPSDeploy'

        # Account id + image URI (read-only; safe to resolve for the -WhatIf preview).
        $stsConfig = Get-LambdaPSClientConfig -Service STS
        $accountId = (Get-STSCallerIdentity -Region $Region -ClientConfig $stsConfig @credentialParameter -ErrorAction Stop).Account
        $partition = Get-LambdaPSPartition -Region $Region
        $imageUri = "$accountId.dkr.ecr.$Region.$($partition.DnsSuffix)/$effectiveRepository`:$RuntimeVersion"
        # The runtime's ARN, known before the runtime exists: the ECR pull grant is scoped to it.
        $runtimeArnForGrant = "arn:$($partition.Name):lambda:${Region}:${accountId}:runtime/$RuntimeName"
        Write-Information "Target image URI: $imageUri" -Tags 'LambdaPSDeploy'

        # Runtime preflight (read-only; always runs, even under -WhatIf). Without it, each of these
        # surfaces only at publish or later, AFTER the scaffold, the ECR repository and the image push.
        # - Region: where CMR is not offered, the service answers GetRuntime with AccessDeniedException
        #   ("Unable to determine service/operation name to be authorized"), the same code as a real IAM
        #   denial. Message text is not a stable contract, so both causes are named. No region list is
        #   hard-coded: the set of regions that offer CMR changes over time.
        # - Version: versions are immutable, and creating an existing one is refused only after the push
        #   has moved its image tag. GetRuntimeVersion answers a missing version with
        #   ResourceNotFoundException, so one read answers the question.
        # - Alias: moving the alias to a version of another architecture breaks the functions bound to it,
        #   so the alias is read and the move is warned about before anything is created.
        $lambdaCoreConfig = Get-LambdaPSClientConfig -Service LambdaCore
        $runtimeExists = $true
        # The alias as read for a new version; it stays null when the runtime or the alias does not exist.
        $currentAlias = $null
        try {
            $null = Get-LMCRuntime -RuntimeName $RuntimeName -Region $Region -ClientConfig $lambdaCoreConfig @credentialParameter -ErrorAction Stop
        }
        catch {
            if (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException') {
                $runtimeExists = $false
            }
            elseif (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'AccessDeniedException') {
                $PSCmdlet.ThrowTerminatingError((New-LambdaPSRuntimeAccessDeniedError -RuntimeName $RuntimeName -Region $Region -Exception $_.Exception))
            }
            else {
                throw
            }
        }
        if ($runtimeExists) {
            $existingVersion = $null
            # Fail closed: a version that could not be read is not proof the version is new, and
            # carrying on would push over an existing version's image tag. Only not-found means new.
            try {
                $existingVersion = Get-LMCRuntimeVersion -RuntimeName $RuntimeName -Name $RuntimeVersion -Region $Region -ClientConfig $lambdaCoreConfig @credentialParameter -ErrorAction Stop
            }
            catch {
                if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) {
                    # The service's text is a sentence of its own: ended with a full stop unless it has one
                    # (a closing quote or bracket may follow it), and left out when it is empty.
                    $serviceText = $_.Exception.Message.Trim()
                    if ($serviceText -and $serviceText -notmatch '[.!?]["'')\]]*\z') { $serviceText += '.' }
                    $serviceDetail = if ($serviceText) { ": $serviceText" } else { '.' }
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.InvalidOperationException]::new(
                                ("Could not read version '$RuntimeVersion' of runtime '$RuntimeName' in region '$Region' " +
                                 "to check that it is new$serviceDetail Nothing was built or pushed."), $_.Exception),
                            'RuntimeVersionCheckFailed', [System.Management.Automation.ErrorCategory]::ReadError, $RuntimeName))
                }
            }
            if ($existingVersion) {
                $state = [string]$existingVersion.State
                # A run that published the version but failed at the alias stage cannot be finished by
                # running again, so for an ACTIVE version name the alias command that completes it --
                # unless its image is another architecture, which is why a run stops before the alias
                # (RuntimeImageArchitectureMismatch). A version reporting none is treated as publish does.
                [string[]]$existingArchitecture = Get-LambdaPSReportedArchitecture -Compatibility $existingVersion.Compatibility
                $aliasAdvice = if ($state -eq 'ACTIVE' -and $existingArchitecture.Count -gt 0 -and $existingArchitecture -notcontains $Architecture) {
                    " Its image is [$($existingArchitecture -join ', ')], not '$Architecture'."
                }
                elseif ($state -eq 'ACTIVE') {
                    # Running the named Update-LMCRuntimeAlias points the alias at this existing version, so
                    # say which of the alias's architectures that version lacks. An alias already on the
                    # version needs no command. The read is best effort: an alias that is missing or cannot
                    # be read, or a version that reports no architecture, gets no note, and the refusal
                    # stands either way.
                    $moveNote = ''
                    $aliasOnVersion = $false
                    try {
                        $currentAlias = Get-LMCRuntimeAlias -RuntimeName $RuntimeName -Name $RuntimeAlias -Region $Region -ClientConfig $lambdaCoreConfig @credentialParameter -ErrorAction Stop
                        # Version names are case-sensitive.
                        $aliasOnVersion = [string]$currentAlias.RuntimeVersion -ceq $RuntimeVersion
                        [string[]]$aliasArchitecture = Get-LambdaPSReportedArchitecture -Compatibility $currentAlias.Compatibility
                        [string[]]$droppedArchitecture = @()
                        if ($existingArchitecture.Count -gt 0) {
                            $droppedArchitecture = @($aliasArchitecture | Where-Object { $existingArchitecture -notcontains $_ })
                        }
                        if ($droppedArchitecture.Count -gt 0) {
                            $moveNote = (" Running Update-LMCRuntimeAlias moves the alias off a [$($aliasArchitecture -join ', ')] version: functions " +
                                "bound to it that were built for [$($droppedArchitecture -join ', ')] fail to invoke from " +
                                "their next cold start until they are redeployed with -Architecture $Architecture.")
                        }
                    }
                    catch { $moveNote = ''; $aliasOnVersion = $false }
                    if ($aliasOnVersion) {
                        " Alias '$RuntimeAlias' already points at it."
                    }
                    else {
                        (" If an earlier run published it but did not move the alias, run Update-LMCRuntimeAlias " +
                         "-RuntimeName $RuntimeName -Name $RuntimeAlias -RuntimeVersion $RuntimeVersion -Region $Region" +
                         "$(Get-LambdaPSCredentialArgumentText -CredentialParameter $credentialParameter) " +
                         "(New-LMCRuntimeAlias if the alias does not exist yet).$moveNote")
                    }
                }
                elseif ($state -eq 'CREATING') {
                    # An earlier run may have stopped while the version was still being created (for example
                    # RuntimeVersionTimeout); running again is refused, so give the commands that finish it.
                    $commandArguments = "-Region $Region$(Get-LambdaPSCredentialArgumentText -CredentialParameter $credentialParameter)"
                    (" It is still being created; check with Get-LMCRuntimeVersion -RuntimeName $RuntimeName -Name $RuntimeVersion $commandArguments. " +
                     "Once it is ACTIVE and its Compatibility.Architectures is '$Architecture', point the alias at it with " +
                     "Update-LMCRuntimeAlias -RuntimeName $RuntimeName -Name $RuntimeAlias -RuntimeVersion $RuntimeVersion $commandArguments " +
                     '(New-LMCRuntimeAlias if the alias does not exist yet).')
                }
                else { '' }
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new(
                            ("Runtime '$RuntimeName' already has version '$RuntimeVersion' ($state). Runtime versions are " +
                             'immutable, so pass a new -RuntimeVersion. Nothing was built or pushed, so the image that ' +
                             "version '$RuntimeVersion' runs is untouched.$aliasAdvice")),
                        'RuntimeVersionExists', [System.Management.Automation.ErrorCategory]::ResourceExists, $RuntimeVersion))
            }

            # Moving the alias to a version of another architecture breaks the functions bound to it
            # that were built for the old one. Say so here, before anything is created or confirmed, and
            # under -WhatIf too. This runs only for a new version: an existing one is refused above and
            # moves nothing. A missing alias is fine: the deploy creates it.
            $currentAlias = $null
            try {
                $currentAlias = Get-LMCRuntimeAlias -RuntimeName $RuntimeName -Name $RuntimeAlias -Region $Region -ClientConfig $lambdaCoreConfig @credentialParameter -ErrorAction Stop
            }
            catch {
                # Only not-found means the deploy creates the alias; any other failure stops it here.
                if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ResourceNotFoundException')) {
                    $serviceText = $_.Exception.Message.Trim()
                    if ($serviceText -and $serviceText -notmatch '[.!?]["'')\]]*\z') { $serviceText += '.' }
                    $serviceDetail = if ($serviceText) { ": $serviceText" } else { '.' }
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.InvalidOperationException]::new(
                                ("Could not read alias '$RuntimeAlias' of runtime '$RuntimeName' in region '$Region' to check " +
                                 "its architecture$serviceDetail Nothing was built or pushed."), $_.Exception),
                            'RuntimeAliasCheckFailed', [System.Management.Automation.ErrorCategory]::ReadError, "$RuntimeName/$RuntimeAlias"))
                }
            }
            if ($null -ne $currentAlias) {
                [string[]]$aliasArchitecture = Get-LambdaPSReportedArchitecture -Compatibility $currentAlias.Compatibility
                # Any architecture the alias supports that the new version does not is dropped,
                # including one of a version that supports both.
                [string[]]$droppedArchitecture = @($aliasArchitecture | Where-Object { $_ -ne $Architecture })
                if ($droppedArchitecture.Count -gt 0) {
                    $move = if ($WhatIfPreference) { 'would move' } else { 'moves' }
                    Write-Warning ("Alias '$RuntimeAlias' of runtime '$RuntimeName' points at a " +
                        "[$($aliasArchitecture -join ', ')] version; this deploy $move it to '$RuntimeVersion', which is " +
                        "'$Architecture'. Functions bound to the alias that were built for " +
                        "[$($droppedArchitecture -join ', ')] fail to invoke from their next cold start until they " +
                        "are redeployed with -Architecture $Architecture.")
                }
            }
        }

        # Image tag: an immutable repository refuses a push over a tag only after the build, and a
        # mutable one moves the tag off the image a version may run. A missing repository has no tags.
        $existingImage = $null
        try {
            $existingImage = Get-ECRImageMetadata -RepositoryName $effectiveRepository -ImageId @{ ImageTag = $RuntimeVersion } -Region $Region -ClientConfig (Get-LambdaPSClientConfig -Service ECR) @credentialParameter -ErrorAction Stop
        }
        catch {
            if (-not (Test-LambdaPSServiceError -ErrorRecord $_ -ErrorCode 'ImageNotFoundException', 'RepositoryNotFoundException')) {
                $serviceText = $_.Exception.Message.Trim()
                if ($serviceText -and $serviceText -notmatch '[.!?]["'')\]]*\z') { $serviceText += '.' }
                $serviceDetail = if ($serviceText) { ": $serviceText" } else { '.' }
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new(
                            ("Could not read ECR repository '$effectiveRepository' in region '$Region' to check that it has no " +
                             "image tagged '$RuntimeVersion'$serviceDetail Nothing was built or pushed."), $_.Exception),
                        'RuntimeImageTagCheckFailed', [System.Management.Automation.ErrorCategory]::ReadError, $effectiveRepository))
            }
        }
        if ($existingImage) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        ("ECR repository '$effectiveRepository' in region '$Region' already has an image tagged '$RuntimeVersion'. " +
                         'The deploy never pushes over a tag, so pass a new -RuntimeVersion. Nothing was built or pushed. ' +
                         "If no runtime version runs that image, free the tag with Remove-ECRImageBatch -RepositoryName " +
                         "$effectiveRepository -ImageId @{ ImageTag = '$RuntimeVersion' } -Region $Region" +
                         "$(Get-LambdaPSCredentialArgumentText -CredentialParameter $credentialParameter).")),
                    'RuntimeImageTagExists', [System.Management.Automation.ErrorCategory]::ResourceExists, $RuntimeVersion))
        }

        # ModulesStaged is read from the scaffolded project's modules/ folder after Stage 1 -- what the
        # image will actually contain, including transitive dependencies -- not from -IncludeModule. It
        # stays empty under -WhatIf, because nothing was scaffolded.
        $modulesStaged = @()
        $runtimeArn = $null
        $imageDigest = $null
        $publishUri = $imageUri

        # Stage 1: Scaffold and stage modules. Module staging is delegated to the scaffolder:
        # -IncludeModule is forwarded to New-LambdaPSRuntimeProject, which copies them from the local
        # machine. This is the ONLY module handling in this cmdlet.
        # -Force is passed so a re-deploy is repeatable, which means an existing directory at
        # $ProjectPath is REPLACED (its contents deleted). The ShouldProcess message says so, so -WhatIf
        # and -Confirm disclose it rather than silently discarding a customized project.
        Write-Verbose "Stage 1: Scaffold runtime project at '$ProjectPath'."
        $scaffoldAction = if ((Test-Path -LiteralPath $ProjectPath -PathType Container) -and @(Get-ChildItem -LiteralPath $ProjectPath -Force).Count -gt 0) {
            "Replace existing directory with a scaffolded PowerShell $PowerShellVersion runtime project (existing contents are deleted)"
        }
        else {
            "Scaffold PowerShell $PowerShellVersion runtime project"
        }
        # The stage confirms once for everything under it, so say when that includes installing
        # missing modules into the current user's module path, which outlives the deploy.
        if ($MissingModuleAction -eq 'Install' -and @($IncludeModule).Count -gt 0) {
            $scaffoldAction += ', installing missing modules for the current user'
        }
        $scaffoldParams = @{
            Path                = $ProjectPath
            PowerShellVersion   = $PowerShellVersion
            PwshVersion         = $resolvedPwshVersion
            IncludeModule       = $IncludeModule
            MissingModuleAction = $MissingModuleAction
            Force               = $true
        }
        if ($ModuleRepository) { $scaffoldParams.ModuleRepository = $ModuleRepository }
        # Each stage needs the one before it, so once a stage is declined under -Confirm the later ones
        # are skipped, with a warning, rather than run against output this run never produced (a stale
        # project, an image tag left by an earlier run, a version that was not published).
        $declinedStage = $null
        if ($PSCmdlet.ShouldProcess($ProjectPath, $scaffoldAction)) {
            $null = New-LambdaPSRuntimeProject @scaffoldParams -Confirm:$false
            $modulesStaged = Get-LambdaPSStagedModule -Path ([System.IO.Path]::Combine($ProjectPath, 'modules'))
            Write-Information "Scaffolded runtime project at '$ProjectPath'." -Tags 'LambdaPSDeploy'
        }
        elseif ($WhatIfPreference) {
            # Preview the scaffold too: it runs every local check a real run makes (pwsh version,
            # module pins, dependencies, availability) and fails the preview the same way.
            $null = New-LambdaPSRuntimeProject @scaffoldParams -WhatIf
        }
        else { $declinedStage = 'scaffold' }

        # Stage 2: Build the image locally, then ensure the ECR repo, then push. Building first means a
        # failed build (for example a package that cannot install for the target platform) stops before
        # anything exists in AWS; creating the repository first would leave an empty one behind.
        # The push build reuses BuildKit's cache from the local build, so it only exports and pushes.
        Write-Verbose "Stage 2: Build image, ensure ECR repository, push image."
        if ($declinedStage) {
            Write-Warning "Skipped building and pushing the image, because the $declinedStage stage was declined."
        }
        elseif ($PSCmdlet.ShouldProcess($imageUri, "Build image, ensure ECR repository '$effectiveRepository' and its Lambda pull grant, and push image")) {
            $null = Build-LambdaPSRuntimeImage -Path $ProjectPath -Architecture $Architecture -Tag "$effectiveRepository`:$RuntimeVersion" -Confirm:$false
            $null = Initialize-LambdaPSEcrRepository -RepositoryName $effectiveRepository -AccountId $accountId -RuntimeArn $runtimeArnForGrant -Region $Region -CredentialParameter $credentialParameter -Confirm:$false
            $pushed = Build-LambdaPSRuntimeImage -Path $ProjectPath -Architecture $Architecture -ImageUri $imageUri -Confirm:$false
            # Publish exactly the image just pushed: by digest, so a tag moved in between cannot change
            # what the version runs. The tag is the fallback when buildx reported no digest.
            if ($null -ne $pushed -and $pushed.ImageDigest) {
                $imageDigest = $pushed.ImageDigest
                $publishUri = "$($imageUri.Substring(0, $imageUri.LastIndexOf(':')))@$imageDigest"
            }
            else {
                Write-Warning ("buildx did not report the pushed image's digest, so version '$RuntimeVersion' is " +
                    "published by the tag '$imageUri'. The service records the digest the tag points at when it publishes.")
            }
            Write-Information "Built and pushed image '$imageUri'." -Tags 'LambdaPSDeploy'
        }
        elseif (-not $WhatIfPreference) { $declinedStage = 'build and push' }

        # Stage 3: Publish the runtime version.
        Write-Verbose "Stage 3: Publish version '$RuntimeVersion' of runtime '$RuntimeName'."
        if ($declinedStage) {
            Write-Warning "Skipped publishing the runtime version, because the $declinedStage stage was declined."
        }
        elseif ($PSCmdlet.ShouldProcess($RuntimeName, "$(if (-not $runtimeExists) { "Create runtime '$RuntimeName' and publish" } else { 'Publish' }) Lambda runtime version '$RuntimeVersion' from image '$publishUri'")) {
            $publishParams = @{
                RuntimeName    = $RuntimeName
                ImageUri       = $publishUri
                RuntimeVersion = $RuntimeVersion
                RuntimeAlias   = $RuntimeAlias
                Architecture   = $Architecture
                Region         = $Region
            }
            $published = Publish-LambdaPSRuntimeVersion @publishParams -CredentialParameter $credentialParameter -Confirm:$false
            # The publish returns the VERSION's ARN (.../runtime/<name>/version/<v>); functions bind to the runtime's.
            $runtimeArn = $published.Arn -replace '/version/[^/]+$', ''
            Write-Information "Published runtime '$RuntimeName' version '$RuntimeVersion'." -Tags 'LambdaPSDeploy'
        }
        elseif (-not $WhatIfPreference) { $declinedStage = 'publish' }

        # Stage 4: Point the alias at the published version.
        Write-Verbose "Stage 4: Point alias '$RuntimeAlias' at '$RuntimeVersion'."
        if ($declinedStage) {
            Write-Warning "Skipped pointing alias '$RuntimeAlias' at '$RuntimeVersion', because the $declinedStage stage was declined."
        }
        elseif ($PSCmdlet.ShouldProcess("$RuntimeName/$RuntimeAlias", "Point alias at $RuntimeVersion")) {
            $aliasParams = @{
                RuntimeName    = $RuntimeName
                RuntimeAlias   = $RuntimeAlias
                RuntimeVersion = $RuntimeVersion
                Region         = $Region
            }
            $null = Set-LambdaPSRuntimeAlias @aliasParams -CredentialParameter $credentialParameter -Confirm:$false
            Write-Information "Pointed alias '$RuntimeAlias' at '$RuntimeVersion'." -Tags 'LambdaPSDeploy'
        }
        elseif (-not $WhatIfPreference) {
            # The version is published, but functions bound to the alias stay where they are. The preflight
            # read says whether the alias exists, so name the one command that moves or creates it.
            $aliasCommand = if ($null -ne $currentAlias) { 'Update-LMCRuntimeAlias' } else { 'New-LMCRuntimeAlias' }
            Write-Warning ("Alias '$RuntimeAlias' was not pointed at '$RuntimeVersion', because that stage was declined. " +
                "Point it later with $aliasCommand -RuntimeName $RuntimeName -Name $RuntimeAlias " +
                "-RuntimeVersion $RuntimeVersion -Region $Region$(Get-LambdaPSCredentialArgumentText -CredentialParameter $credentialParameter).")
        }

        [PSCustomObject]@{
            PSTypeName        = 'AWS.ToolExtras.LambdaPSRuntime.RuntimeDeployment'
            RuntimeName       = $RuntimeName
            RuntimeArn        = $runtimeArn
            PowerShellVersion = $PowerShellVersion
            PwshVersion       = $resolvedPwshVersion
            Region            = $Region
            RepositoryName    = $effectiveRepository
            ImageUri          = $imageUri
            ImageDigest       = $imageDigest
            RuntimeVersion    = $RuntimeVersion
            RuntimeAlias      = $RuntimeAlias
            Architecture      = $Architecture
            ModulesStaged     = $modulesStaged
            ProjectPath       = $ProjectPath
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
