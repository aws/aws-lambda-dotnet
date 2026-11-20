function New-LambdaPSRuntimeProject {
    <#
    .SYNOPSIS
        Scaffolds a PowerShell Lambda custom-runtime project (runtime image or layer).
    .DESCRIPTION
        Copies the module's template tree for the selected deployment target into the target
        directory. This is the low-level scaffold cmdlet. -Target defaults to CMR.

        The CMR target (default) emits a runtime-image project with:
          - a provided:al2023 Dockerfile that installs pwsh (from the Microsoft GPG-signed RPM on
            linux/amd64, or on linux/arm64 from the official release tarball verified against a
            pinned SHA256) and moves the interpreter to /var/runtime/powershell;
          - the bootstrap, and the runtime.ps1 event loop it starts;
          - PowerShellLambdaContext.cs, precompiled to a DLL during the image build;
          - the pwsh-runtime helper modules, including modules/Private/Set-PSModulePath.ps1, which
            sets a function-first PSModulePath spanning all module tiers.

        The Layer target emits a Lambda layer project whose contents mount at /opt: a bootstrap that
        references /opt, runtime.ps1, the pwsh-runtime helper modules, and
        PowerShellLambdaContext.cs. Build it into a layer .zip with
        Build-LambdaPSRuntimePackage, which adds pwsh; no Docker is needed.

        Every scaffolded project is the customer's to edit. A lambdaps-runtime.json marker recording
        the deployment target, PowerShell version, pwsh version and module version is written into
        the output so the validator and future tooling can identify the project shape. A template tree missing from the module (an
        incomplete install) is a TemplateNotFound error. A write that fails part-way, on a full disk for example, is a
        ProjectWriteFailed error; when -Path was new or empty, what the run wrote is removed first.

        For the CMR target, the copy substitutes the concrete pwsh version into the Dockerfile's
        __PWSH_VERSION__ token and the pinned SHA256 of that version's linux-arm64 tarball into its
        __PWSH_ARM64_SHA256__ token. If no arm64 hash is pinned for the version, that
        token is left in place with a warning: the project still builds for linux/amd64, but a
        linux/arm64 build stops at the Dockerfile's hash check.

        This cmdlet only writes local files; it makes no AWS API calls. New-LambdaPSRuntimeDeployment
        builds, pushes and publishes a CMR project in one step; the project's README lists the manual
        steps (ECR repository and Lambda pull grant, docker login, Build-LambdaPSRuntimeImage -ImageUri,
        New-LMCRuntime, New-LMCRuntimeVersion, New-LMCRuntimeAlias).
    .PARAMETER Path
        The directory to create the project in. Alias: -ProjectPath.
        An existing file, a link to a file, or a link to nothing, is a TargetIsFile error, a folder
        you cannot list TargetNotReadable, a drive that does not exist DriveNotFound, and a provider other than the
        file system (for example Env:) PathNotFileSystem. Create projects in a
        folder only you can write to: a new -Path's parent folder is not checked, so another user who
        can write to it could change the project.
        On Linux and macOS an existing -Path must be owned by you, or by root when no group or other
        user can write to it: another user's directory (one created first in a shared folder such as
        /tmp) is a terminating TargetOwnedByAnotherUser error, even with -Force, since that user could
        change the project while it is written or built. A directory of yours that every user can
        write to (o+w, without the sticky bit) is a TargetWritableByOthers error for the same reason:
        remove that permission with chmod o-w <path>. The checks are skipped when you run as root.
    .PARAMETER PowerShellVersion
        The PowerShell release the project targets: 7.6.9 installs that exact release, 7.6.* the
        module's pinned release of the 7.6 line, and 7.* that of the highest 7.x line it supports.
        A bare 7.6 is PowerShellVersionNotSpecific, and a line with no pinned release
        UnknownPwshVersion. For the CMR target the release is baked into the
        Dockerfile; for the Layer target it is recorded in lambdaps-runtime.json and
        Build-LambdaPSRuntimePackage bundles it, unless its own -PowerShellVersion changes it.
    .PARAMETER Target
        The deployment shape to scaffold: 'CMR' (customer-managed runtime image, the default) or 'Layer' (a
        Lambda layer whose contents mount at /opt).
    .PARAMETER IncludeModule
        Modules to stage into the project's modules directory (with transitive dependencies), via
        Copy-LambdaPSModuleSet. Accepts a list. Each specification must pin an exact RequiredVersion
        (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }); minimum-version and
        version-range specs are rejected. A module that is not installed locally is handled according
        to -MissingModuleAction. Defaults to none. Alias: -Module.
        AWS.Tools modules are staged without their *.xml help, so Get-Help shows no
        help text for them and Test-ModuleManifest reports their FileList entries as missing.
        AWSPowerShell (the Windows PowerShell module, which doesn't run in PowerShell 7) and
        AWSPowerShell.NetCore (over 600 MB, more than Lambda's 250 MB package limit) fail with
        AWSPowerShellModuleNotSupported: name the AWS.Tools.* modules for the services you use
        instead. A name with a wildcard (*, ? or [) or a path (rooted, or with / or \) is a
        ModuleNameInvalid error, and a specification without RequiredVersion a ModuleVersionRequired
        error. Resolving the dependencies stops with ModuleVersionConflict when a module is needed at
        versions no one version meets; ModuleVersionSpelling when the module found has the version
        written another way (PowerShell matches versions exactly, so 1.2 is not 1.2.0) or is above a
        dependency's MaximumVersion; DependencyNameMissing, DependencyNameIsPath or
        DependencyVersionUnpinned when a manifest's RequiredModules entry has no name, names a path,
        or has no valid version; and ModuleClosureNotSettled when the dependency versions do not
        settle.
    .PARAMETER MissingModuleAction
        What to do with a module (or dependency) that is not available locally:
          Save     (default) download it into a temporary folder, stage it, and delete the folder. The
                   local PowerShell environment is not changed.
          Install  download it the same way, and also install it for the current user so it stays
                   available for local testing.
          Stop     fail with ModuleNotFoundLocally.
        AWS.Tools modules are downloaded with AWS.Tools.Installer 2.0.5 or a later 2.x (the
        AWS.Tools.zip from CloudFront, checked against the SHA512 published beside it, which detects a
        corrupted download, not a tampered one); anything else with PSResourceGet from your
        registered repositories.
        PowerShellGet is never used, and repository trust is not bypassed. See Save-LambdaPSModule.
        A download stops with ModuleInstallerNotFound when PSResourceGet is not available,
        AWSToolsInstallerImportFailed when AWS.Tools.Installer cannot be imported, ModuleDownloadFailed
        when the download does not produce the module, ModuleFolderIncomplete when a module's version
        folder exists without its <Name>.psd1 (remove that folder), and ModuleGuidMismatch when the
        module's name and version exist only with a GUID other than the one the requirement names
        (the message gives both).
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules from, in order (for example
        an internal mirror). When omitted, every registered repository is searched in priority order,
        so a same-named package in a higher-priority feed would be used. AWS.Tools modules always come
        from the AWS.Tools.zip that AWS.Tools.Installer downloads; when the installer itself must be
        fetched, it comes from these repositories (PSGallery when this is omitted) at the version this
        module pins, and is used only when its files match the pinned SHA256 and, on Windows, its
        Authenticode signature is valid.
        A repository name, or a pattern with '*' wildcards, that matches no registered PSResourceGet
        repository (Get-PSResourceRepository) stops the download with ModuleRepositoryNotFound, as does
        a missing PSGallery when the installer must come from it.
    .PARAMETER Force
        REPLACE an existing non-empty target directory: its contents are deleted before the project
        is written, so the emitted tree is the whole tree. Without -Force a non-empty target is a
        terminating TargetNotEmpty error. Replacing rather than merging is deliberate -- writing over
        the top would leave files the new target does not emit (for example a CMR Dockerfile in a
        re-scaffolded Layer project), producing a project that looks valid and builds into a broken
        image. -WhatIf names the full path of the folder whose contents would be deleted. -Force does not lift the owner check on an
        existing -Path (TargetOwnedByAnotherUser; see -Path). Nor does it replace a non-empty -Path
        that is a symbolic link or a junction (TargetIsLink), since that would empty the folder it
        points to, or one that, with links resolved, is the current location, your home folder, the
        root of a drive, or a folder that contains one of them (TargetProtected).

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
        New-LambdaPSRuntimeProject -Path ./powershell-runtime -PowerShellVersion 7.6.*

        Scaffolds a CMR runtime-image project targeting PowerShell 7.6 (the default 7.6 pwsh release).
    .EXAMPLE
        New-LambdaPSRuntimeProject -Path ./layer -PowerShellVersion 7.6.* -Target Layer

        Scaffolds a Lambda layer project targeting PowerShell 7.6.
    .EXAMPLE
        New-LambdaPSRuntimeProject -Path ./rt -PowerShellVersion 7.6.0 -Force

        Replaces the contents of an existing directory and pins pwsh 7.6.0. That version has no pinned
        arm64 hash, so the project builds linux/amd64 only and a warning says so.
    .EXAMPLE
        New-LambdaPSRuntimeProject -Path ./rt-s3 -PowerShellVersion 7.6.* -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }

        Scaffolds the project and stages AWS.Tools.S3 5.0.274 (and its dependencies) into
        ./rt/modules, downloading any that are not installed locally.
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
        [SupportsWildcards()]
        [ValidatePattern('\A(0|[1-9][0-9]*)\.((0|[1-9][0-9]*)(\.(0|[1-9][0-9]*|\*))?|\*)\z', ErrorMessage = "'{0}' is not a PowerShell version such as 7.6.9, 7.6.* or 7.*.")]
        [string]$PowerShellVersion,

        [Parameter()]
        [ValidateSet('CMR', 'Layer')]
        [string]$Target = 'CMR',

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

        $requested = Split-LambdaPSPowerShellVersion -PowerShellVersion $PowerShellVersion
        $PowerShellVersion, $PwshVersion = $requested.Line, $requested.Release
        $resolvedVersion = Resolve-LambdaPSPwshVersion -PowerShellVersion $PowerShellVersion -PwshVersion $PwshVersion

        $templateRoot = Get-LambdaPSProjectTemplateRoot -Target $Target

        $scaffoldParams = @{
            Path                = $Path
            Existing            = $existing
            IncludeModule       = $IncludeModule
            MissingModuleAction = $MissingModuleAction
            ModuleRepository    = $ModuleRepository
            TemplateParameter   = @{
                Path              = $templateRoot
                PwshVersion       = $resolvedVersion
                Target            = $Target
                PowerShellVersion = $PowerShellVersion
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
