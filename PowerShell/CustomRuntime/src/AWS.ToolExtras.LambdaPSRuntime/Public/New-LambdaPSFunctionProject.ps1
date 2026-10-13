function New-LambdaPSFunctionProject {
    <#
    .SYNOPSIS
        Scaffolds a PowerShell Lambda function project: a .zip bound to a runtime, a self-contained .zip,
        or a container image.
    .DESCRIPTION
        Emits a function-package project tree. The shape depends on -Target:

          - RuntimeBound (default): a handler-only function .zip that binds to a separate runtime
            (a customer-managed runtime) at deploy time. It contains a handler.ps1 with the standard
            param($LambdaInput, $LambdaContext) signature, a function-local modules directory that
            takes highest PSModulePath precedence (function-first), and a binding manifest that
            records the handler name, runtime alias, and architecture. It is copied from the module's
            Templates/FunctionZip tree, with the runtime settings section from Templates/Shared added
            to its README.

          - SelfContainedZip: a single, self-contained function .zip on provided.al2023 whose
            contents mount at /var/task. The interpreter (pwsh), runtime module, LambdaContext
            (precompiled when the build host can run the bundled pwsh), and a starter <Handler>.ps1
            all ship in the one .zip; there is no separate runtime resource. This target copies the
            module's Templates/Shared and Templates/SelfContainedZip trees and writes a
            lambdaps-runtime.json marker so the validator can recognize the project shape. pwsh
            itself is added at build time by Build-LambdaPSRuntimePackage (no Docker).

          - Container: a container-per-function image (PackageType=Image) that bundles pwsh, the
            runtime bootstrap, and the helper modules under /var/runtime (the CMR layout), plus the
            function's files (the handler and everything beside it) baked into /var/task. There is no
            separate runtime resource. This target copies
            the module's Templates/Shared, Templates/RuntimeImage and Templates/ContainerFunction trees
            (the CMR runtime image's Dockerfile with the function steps added, and the concrete pwsh
            version substituted into its __PWSH_VERSION__ token) and writes a lambdaps-runtime.json
            marker (Target=Container) so the validator can recognize the project shape.

        This cmdlet only writes local files; it makes no AWS API calls. For the RuntimeBound target,
        deploy the project with New-LambdaPSFunctionDeployment, or package it with
        Compress-LambdaPSFunctionPackage and create the function with Publish-LMFunction pointed at
        the runtime alias. A template tree missing from the module (an incomplete install) is a
        TemplateNotFound error.
    .PARAMETER Path
        The directory to create the function project in. Alias: -ProjectPath.
        An existing file, or a link to nothing, is a TargetIsFile error, a folder you cannot list
        TargetNotReadable, a drive that does not exist DriveNotFound, and a provider other than the
        file system (for example Env:) PathNotFileSystem. Create projects in a
        folder only you can write to: a new -Path's parent folder is not checked, so another user who
        can write to it could change the project.
        On Linux and macOS an existing -Path must be owned by you, or by root when no group or other
        user can write to it: another user's directory (one created first in a shared folder such as
        /tmp) is a terminating TargetOwnedByAnotherUser error, even with -Force, since that user could
        change the project while it is written or built. The check is skipped when you run as root.
    .PARAMETER Handler
        The handler name. A bare name ('handler') or a script file name ('handler.ps1') is accepted;
        the emitted handler script is '<name>.ps1' for every target. For RuntimeBound it is also the
        recorded handler string; for Container the Dockerfile's CMD is written to match; for
        SelfContainedZip pass '<name>.ps1' as the function's handler at deploy time. 'runtime' is
        rejected for SelfContainedZip and Container, whose bundled runtime loop is already runtime.ps1,
        with HandlerNameReserved.
    .PARAMETER RuntimeAlias
        The runtime alias the function binds to at deploy time (RuntimeBound target). Defaults to
        'production', the same alias New-LambdaPSRuntimeDeployment creates and
        New-LambdaPSFunctionDeployment binds to. Ignored, with a warning, for the other targets. An
        alias name is up to 128 letters, digits, '_', '.' and '-', not starting or ending with '.' or
        '-', as Lambda requires.
    .PARAMETER Architecture
        The target architecture: x86_64 or arm64 (RuntimeBound target), recorded in manifest.json so
        New-LambdaPSFunctionDeployment uses it. When omitted, none is recorded and the deploy takes the
        runtime alias's architecture. Ignored, with a warning, for the other targets.
    .PARAMETER Target
        The function-package shape to scaffold: 'RuntimeBound' (the default handler-only .zip that
        binds to a separate runtime), 'SelfContainedZip' (a single .zip that bundles the runtime), or
        'Container' (a container-per-function image that bundles the runtime).
    .PARAMETER PowerShellVersion
        The PowerShell version the bundled package targets, in major.minor form (for example
        7.4). Required for -Target SelfContainedZip and -Target Container, which stop with
        PowerShellVersionRequired without it; ignored for RuntimeBound.
    .PARAMETER PwshVersion
        The full pwsh version to install (x.y.z, for example 7.4.20). When omitted, the
        major.minor value is mapped to a default patch release. It is baked into the Container
        Dockerfile at scaffold time, together with the module's pinned SHA256 of that version's
        linux-arm64 release tarball (the arm64 image installs the tarball, since there is no aarch64
        RPM). If no arm64 hash is pinned for the version, a warning is written and the project builds
        for linux/amd64 only. For SelfContainedZip the version is recorded in lambdaps-runtime.json
        and Build-LambdaPSRuntimePackage bundles it; its own -PwshVersion overrides it.
        A version that is not a -PowerShellVersion release is a PwshVersionMismatch error; without
        it, a major.minor with no default patch is UnknownPwshVersion.
    .PARAMETER IncludeModule
        Modules to stage into the project's modules directory (with transitive dependencies), via
        Copy-LambdaPSModuleSet. Accepts a list. Each specification must pin an exact RequiredVersion
        (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }); minimum-version and
        version-range specs are rejected. A module that is not installed locally is handled according
        to -MissingModuleAction. Defaults to none. Alias: -Module.
        A specification for AWSPowerShell or AWSPowerShell.NetCore is an AWSPowerShellModuleNotSupported
        error (use the AWS.Tools.* modules), and one without RequiredVersion a ModuleVersionRequired
        error. Resolving the dependencies stops with ModuleVersionConflict when a module is needed at versions no one
        version meets; ModuleVersionSpelling when the module found has the version written another way
        (PowerShell matches versions exactly, so 1.2 is not 1.2.0) or is above a dependency's
        MaximumVersion; DependencyNameMissing, DependencyNameIsPath or DependencyVersionUnpinned when a
        manifest's RequiredModules entry has no name, names a path, or has no valid version; and
        ModuleClosureNotSettled when the dependency versions do not settle.
    .PARAMETER MissingModuleAction
        What to do with a module (or dependency) that is not available locally:
          Save     (default) download it into a temporary folder, stage it, and delete the folder. The
                   local PowerShell environment is not changed.
          Install  download it the same way, and also install it for the current user so it stays
                   available for local testing.
          Stop     fail with ModuleNotFoundLocally.
        AWS.Tools modules are downloaded with AWS.Tools.Installer 2.0.5 or a later 2.x (the
        SHA512-checked AWS.Tools.zip from CloudFront); anything else with PSResourceGet from your
        registered repositories.
        PowerShellGet is never used, and repository trust is not bypassed. See Save-LambdaPSModule.
        A download stops with ModuleInstallerNotFound when PSResourceGet is not available,
        AWSToolsInstallerImportFailed when AWS.Tools.Installer cannot be imported, and
        ModuleDownloadFailed when the download does not produce the module.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules from, in order (for example
        an internal mirror). When omitted, every registered repository is searched in priority order,
        so a same-named package in a higher-priority feed would be used. AWS.Tools modules always come
        from the AWS.Tools.zip that AWS.Tools.Installer downloads; when the installer itself must be
        fetched, it comes from these repositories (PSGallery when this is omitted) and, on Windows, only
        when its Authenticode signature is valid.
        A repository name, or a pattern with '*' wildcards, that matches no registered PSResourceGet
        repository (Get-PSResourceRepository) stops the download with ModuleRepositoryNotFound, as does
        a missing PSGallery when the installer must come from it.
    .PARAMETER Force
        REPLACE an existing non-empty target directory: its contents are deleted before the project
        is written, so the emitted tree is the whole tree. Without -Force a non-empty target is a
        terminating TargetNotEmpty error. Replacing rather than merging is deliberate -- writing over
        the top would leave files the new target does not emit, producing a hybrid project that looks
        valid. Use -WhatIf to see what would be deleted. -Force does not lift the owner check on an
        existing -Path (TargetOwnedByAnotherUser; see -Path).

        Under -Confirm the module download (when a module must be downloaded), the install for the
        current user (under -MissingModuleAction Install), the delete, the file write and the module
        copy ask once each. Declining the download writes nothing, with a warning naming the modules;
        so does declining the delete. Declining the install still stages the downloaded modules into
        the project, without installing them for you. Declining the file write stages no modules
        either, with a warning when there were modules to stage. Either way no paths are returned.
        Declining the module copy writes the project without its modules, and returns the project
        files only.
    .OUTPUTS
        System.String[]

        The full paths written, emitted as one array. Empty under -WhatIf, and when the download, the
        delete or the file write is declined under -Confirm.
    .EXAMPLE
        New-LambdaPSFunctionProject -Path ./hello -Handler handler

        Scaffolds a handler-only (RuntimeBound) function project whose entry point is handler.ps1.
    .EXAMPLE
        New-LambdaPSFunctionProject -Path ./hello -Handler handler -RuntimeAlias prod -Architecture arm64

        Scaffolds an arm64 RuntimeBound function project bound to the 'prod' runtime alias.
    .EXAMPLE
        New-LambdaPSFunctionProject -Path ./hello -Handler handler -Target SelfContainedZip -PowerShellVersion 7.4

        Scaffolds a self-contained function .zip project bundling PowerShell 7.4 (the default 7.4 pwsh release, added at build time).
    .EXAMPLE
        New-LambdaPSFunctionProject -Path ./hello -Handler handler -Target Container -PowerShellVersion 7.4

        Scaffolds a container-per-function image project bundling PowerShell 7.4 (the default 7.4 pwsh release).
    .EXAMPLE
        New-LambdaPSFunctionProject -Path ./hello -Handler handler -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }

        Scaffolds the function project and stages AWS.Tools.S3 5.0.274 (and its dependencies) into
        ./hello/modules, downloading any that are not installed locally.
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
    [OutputType([string[]])]
    # NOTE on ShouldProcess: this cmdlet delegates the -Force removal and every WRITE to helpers that
    # gate on their own $PSCmdlet.ShouldProcess (Invoke-LambdaPSProjectScaffold,
    # Copy-LambdaPSProjectTemplate, Copy-LambdaPSModuleSet); -WhatIf reaches those because
    # $WhatIfPreference is inherited by child scopes. So '-WhatIf' previews the template
    # copy (one line for the whole tree) and each module copy, deletes nothing, and leaves nothing on disk.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSShouldProcess', '',
        Justification = 'Every write and the -Force removal are gated by the helpers this cmdlet calls; -WhatIf and -Confirm reach them through the inherited preference variables.')]
    # PSUseOutputTypeCorrectly: the return is 'return , ([string[]]$written)'. The unary comma wraps the
    # array so the pipeline emits it as ONE object whose runtime type is string[] -- which is exactly
    # what OutputType declares. The rule infers Object[] from the static expression shape.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Alias('ProjectPath')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidatePattern('\A[A-Za-z][A-Za-z0-9_-]*(\.ps1)?\z', Options = 'None')]
        [string]$Handler,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [ValidatePattern('\A[A-Za-z0-9_](?:[A-Za-z0-9_.-]{0,126}[A-Za-z0-9_])?\z', Options = 'None', ErrorMessage = "'{0}' is not a valid runtime alias name (letters, digits, '_', '.' and '-', up to 128 characters, not starting or ending with '.' or '-').")]
        # 'production' to match New-LambdaPSRuntimeDeployment and New-LambdaPSFunctionDeployment, so the
        # alias the scaffolded manifest.json and README name is the one the one-command path creates.
        [string]$RuntimeAlias = 'production',

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter()]
        [ValidateSet('RuntimeBound', 'SelfContainedZip', 'Container')]
        [string]$Target = 'RuntimeBound',

        [Parameter()]
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
        [switch]$Force
    )

    try {
        # Every check runs before anything is downloaded or written, so a failure leaves no
        # half-scaffolded directory behind.
        $existing = Assert-LambdaPSScaffoldPath -Path $Path -Force:$Force

        # A bare name or '<name>.ps1' becomes '<name>.ps1'. The project is a script handler, which the
        # runtime runs as '& <file> $LambdaInput $LambdaContext', so the handler string is that file name.
        $handlerScript = if ($Handler -like '*.ps1') { $Handler } else { "$Handler.ps1" }

        # Validate the template targets before any download or write.
        $isTemplateTarget = $Target -eq 'SelfContainedZip' -or $Target -eq 'Container'
        if ($isTemplateTarget) {
            # These two describe the binding to a separate runtime, which these targets do not have.
            $ignored = @('RuntimeAlias', 'Architecture') | Where-Object { $PSBoundParameters.ContainsKey($_) }
            if ($ignored) {
                $agreement = if (@($ignored).Count -eq 1) { 'applies only to -Target RuntimeBound, so it is' } else { 'apply only to -Target RuntimeBound, so they are' }
                $buildHint = if (@($ignored) -contains 'Architecture') { ' Choose the architecture when you build it.' } else { '' }
                Write-Warning ("-$($ignored -join ' and -') $agreement ignored for -Target ${Target}: the project " +
                    "carries its own runtime.$buildHint")
            }
            if ($handlerScript -eq 'runtime.ps1') {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new(
                            "-Handler '$Handler' would overwrite the bundled runtime loop (runtime.ps1) in a $Target project. Choose another handler name.",
                            'Handler'),
                        'HandlerNameReserved', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Handler))
            }

            if ([string]::IsNullOrWhiteSpace($PowerShellVersion)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new(
                            "The -PowerShellVersion parameter (major.minor, for example 7.4) is required for -Target $Target.",
                            'PowerShellVersion'),
                        'PowerShellVersionRequired', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Target))
            }

            $resolvedVersion = Resolve-LambdaPSPwshVersion -PowerShellVersion $PowerShellVersion -PwshVersion $PwshVersion
        }
        $templateRoot = Get-LambdaPSProjectTemplateRoot -Target $Target

        $scaffoldParams = @{
            Path                = $Path
            Existing            = $existing
            IncludeModule       = $IncludeModule
            MissingModuleAction = $MissingModuleAction
            ModuleRepository    = $ModuleRepository
        }
        if ($isTemplateTarget) {
            $scaffoldParams.TemplateParameter = @{
                Path              = $templateRoot
                PwshVersion       = $resolvedVersion
                Target            = $Target
                PowerShellVersion = $PowerShellVersion
                HandlerScript     = $handlerScript
            }
        }
        else {
            # The RuntimeBound project runs on the runtime it binds to, which brings its own pwsh.
            $ignored = @('PowerShellVersion', 'PwshVersion') | Where-Object { $PSBoundParameters.ContainsKey($_) }
            if ($ignored) {
                $agreement = if (@($ignored).Count -eq 1) { 'applies only to -Target SelfContainedZip and Container, so it is' } else { 'apply only to -Target SelfContainedZip and Container, so they are' }
                Write-Warning ("-$($ignored -join ' and -') $agreement ignored for -Target RuntimeBound: the function " +
                    'runs on the pwsh of the runtime it binds to.')
            }
            # Record the architecture only when it was chosen, so a deploy otherwise follows the runtime's.
            $chosenArchitecture = if ($PSBoundParameters.ContainsKey('Architecture')) { $Architecture } else { $null }
            $scaffoldParams.TemplateParameter = @{
                Path             = $templateRoot
                Target           = $Target
                HandlerScript    = $handlerScript
                Token            = @{
                    __RUNTIME_ALIAS__       = $RuntimeAlias
                    __ARCHITECTURE__        = $chosenArchitecture
                    __README_ARCHITECTURE__ = if ($chosenArchitecture) { $chosenArchitecture } else { '<architecture of the runtime alias>' }
                }
            }
        }
        # Typed assignment: the helper returns one comma-wrapped string[], which '@(...)' would nest.
        [string[]]$written = Invoke-LambdaPSProjectScaffold @scaffoldParams
        return , ([string[]]$written)
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
