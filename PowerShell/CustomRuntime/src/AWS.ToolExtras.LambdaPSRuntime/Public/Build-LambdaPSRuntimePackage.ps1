function Build-LambdaPSRuntimePackage {
    <#
    .SYNOPSIS
        Builds a container-free PowerShell Lambda package (a Lambda layer or a self-contained
        function zip) with no Docker.
    .DESCRIPTION
        The non-container counterpart to Build-LambdaPSRuntimeImage. For the Layer and
        SelfContainedZip targets, the deployment artifact is a .zip that runs on the stock
        provided.al2023 base, so no image build (and no Docker) is needed. This cmdlet assembles that
        package entirely in PowerShell, so it runs on Windows, macOS and Linux:

          1. Obtains the pwsh Linux binaries via Get-LambdaPSPwshArchive (a version-pinned,
             SHA256-verified download of the official release tarball, or a caller-supplied
             -PwshArchivePath) and extracts them under powershell/.
          2. Stages the project folder: the bootstrap (an sh wrapper that sets
             DOTNET_SYSTEM_GLOBALIZATION_INVARIANT so pwsh runs on provided.al2023 without libicu),
             runtime.ps1, the modules directory and, for SelfContainedZip, the handler and every other
             file it uses, plus the modules its '#Requires -Modules' declare (with their dependencies),
             staged under modules/ the way Compress-LambdaPSFunctionPackage does. The folder is copied
             with its hidden files, so keep secrets such as .env files out of it, and by the same rules as
             Compress-LambdaPSFunctionPackage, whose help lists what is left out (.git, links that leave
             the folder or loop back into it, -OutFile and its .partial files, ...) and which skips warn; a zip file at the top
             level ships with a warning. The project's lambdaps-runtime.json and README.md at its top
             level are left out too. A required file (bootstrap, runtime.ps1,
             PowerShellLambdaContext.cs) that is a link leaving the project is not packaged, so the build
             stops with ProjectFileNotFound or ContextSourceNotFound.
          3. Precompiles the staged PowerShellLambdaContext.cs to a DLL with the BUNDLED pwsh, so the DLL
             targets the .NET that runs it on Lambda. That needs a Linux host of the target architecture
             where the bundled pwsh can start (not a temp folder mounted noexec); elsewhere no DLL is
             shipped, with a warning, and the runtime compiles the .cs at cold start (~1.7 s). A compile
             that runs and fails is ContextCompileFailed.
          4. Zips it with forward-slash relative entry paths in ordinal order, each with the same
             timestamp, so an unchanged project built with the same PowerShell version on the same OS
             gives a byte-identical package (except for a precompiled
             PowerShellLambdaContext.dll, whose compiled bytes can differ between builds), and the executable
             bit set on bootstrap and powershell/pwsh, and on any file that is executable on a Linux or
             macOS build host; everything else is mode 0644. The package bundles pwsh, so it is over
             Lambda's 50 MB direct-upload limit, and is written with a warning to upload it through S3;
             one whose files add up to more than Lambda's 250 MB unzipped limit is written with a
             warning that Lambda rejects it.

        The target is read from the project's lambdaps-runtime.json marker; -Target names it for a
        project with no marker.
        Only Layer and SelfContainedZip are container-free; for CMR or Container use
        Build-LambdaPSRuntimeImage instead. This cmdlet makes no AWS API calls.

        Errors it can raise, by error ID:
          - The project: ProjectNotFound, NotARuntimeProject (a function project: use
            Compress-LambdaPSFunctionPackage), ProjectFileNotFound, ContextSourceNotFound,
            InvalidProjectMarker, TargetNotResolved, TargetMismatch, UnsupportedTarget,
            PowerShellVersionRequired, HandlerNotResolved, HandlerScriptNotFound, InvalidHandlerPath,
            BackslashInFileName (off Windows, a file with '\' in its name).
          - Paths: DriveNotFound, PathNotFileSystem, OutFileExists, OutFileIsDirectory, OutFileIsSource (-OutFile
            is the -PwshArchivePath tarball).
          - pwsh: UnknownPwshVersion, PwshVersionMismatch, UnknownPwshArchiveHash, PwshArchiveNotFound,
            PwshArchiveHashMismatch, TarNotFound, PwshArchiveExtractFailed, PwshBinaryNotFound,
            ContextCompileFailed, PackageEntryIsLink (a link in the tarball).
          - The handler's modules (SelfContainedZip only): ModuleNameInvalid (a #Requires naming a wildcard
            or a path), ModuleVersionRequired (a #Requires that does not pin an exact RequiredVersion), AWSPowerShellModuleNotSupported (a #Requires on the
            AWSPowerShell or AWSPowerShell.NetCore module), ModuleVersionSpelling (versions that differ only in a trailing .0),
            ModuleVersionConflict, ModuleClosureNotSettled, DependencyNameMissing, DependencyNameIsPath,
            DependencyVersionUnpinned, ModuleNotFoundLocally (-MissingModuleAction Stop),
            ModuleRepositoryNotFound, ModuleDownloadFailed, ModuleFolderIncomplete (a module version
            folder with no <Name>.psd1), ModuleGuidMismatch (the module and version exist only with a
            different GUID than the requirement names), ModuleInstallerNotFound,
            AWSToolsInstallerImportFailed.
    .PARAMETER Path
        The scaffolded project directory (as produced by New-LambdaPSRuntimeProject -Target Layer or
        New-LambdaPSFunctionProject -Target SelfContainedZip). Alias: -ProjectPath.
    .PARAMETER OutFile
        The destination .zip path. Its parent directory is created if missing. Alias: -PackagePath,
        the name of the result field that reports it.
    .PARAMETER Target
        The target of a project with no lambdaps-runtime.json marker: 'Layer' or 'SelfContainedZip'.
        A project's bootstrap is written for the target it was scaffolded for, so with a Layer or
        SelfContainedZip marker -Target must name that target (TargetMismatch otherwise). A CMR or
        Container project is UnsupportedTarget whatever -Target says: build it with
        Build-LambdaPSRuntimeImage.
    .PARAMETER PowerShellVersion
        Override the PowerShell version (major.minor) read from the marker.
    .PARAMETER PwshVersion
        Pin the full pwsh version (x.y.z). When omitted, the version the project was scaffolded with
        (recorded in lambdaps-runtime.json) is used, or else the major.minor value is mapped to a
        default patch release. A version with no pinned SHA256 requires -PwshArchivePath.
    .PARAMETER Architecture
        The target architecture: x86_64 (default) or arm64.
    .PARAMETER PwshArchivePath
        A pre-downloaded, caller-vetted pwsh Linux tarball to bundle instead of downloading (for
        air-gapped or security-controlled builds). When this module pins a hash for the pwsh version
        and architecture, the file must match the official tarball, so a tarball of the wrong
        version or architecture is refused. For a version with no pinned hash it is used as-is:
        verify the file's provenance yourself. On a Linux host of the target architecture, the pwsh
        in it is also RUN on the build host, to precompile the context DLL. The file is hashed and then
        read again to extract it, so it must not be writable by other users.
    .PARAMETER HandlerScript
        For SelfContainedZip (ignored, with a warning, for a Layer), the handler script whose
        '#Requires -Modules' are staged under modules/ (for example 'handler.ps1'). When omitted, it is the sole root .ps1 other than runtime.ps1;
        with none, no requirements are read, and with more than one it fails with HandlerNotResolved. It is found as Compress-LambdaPSFunctionPackage
        finds its handler: resolved against the project folder, its file name matched case-sensitively as
        on Lambda, and its real path (every link followed) in the project. Otherwise it fails with
        HandlerScriptNotFound. Off Windows, a '\' in it fails with InvalidHandlerPath: separate folders
        with '/'.
    .PARAMETER MissingModuleAction
        For SelfContainedZip (ignored, with a warning, for a Layer): what to do with a module the
        handler requires (or a dependency) that is not available locally, as on
        Compress-LambdaPSFunctionPackage: Save (default) downloads it into a temporary folder that
        is deleted afterwards; Install also installs it for the current user; Stop fails with
        ModuleNotFoundLocally. Versions are matched exactly, so a requirement that differs from an
        available version only in a trailing .0 fails with ModuleVersionSpelling.
    .PARAMETER ModuleRepository
        For SelfContainedZip (ignored, with a warning, for a Layer): the PSResourceGet repositories to
        download non-AWS.Tools modules from, in order, as on Compress-LambdaPSFunctionPackage. Every
        name, or pattern with `*` wildcards, must match a registered PSResourceGet repository, or the
        cmdlet stops with ModuleRepositoryNotFound before anything is downloaded.
    .PARAMETER Force
        Overwrite the destination .zip if it already exists.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.RuntimePackage

        PackagePath (the .zip written), Target, PwshVersion (the pwsh bundled), Architecture and
        ModulesStaged: an AWS.ToolExtras.LambdaPSRuntime.StagedModule (Name, and Version as a [version])
        for every module in the package's modules/ tree, read from what was staged, as on
        Compress-LambdaPSFunctionPackage. Nothing under -WhatIf.
    .EXAMPLE
        Build-LambdaPSRuntimePackage -Path ./layer -OutFile ./layer.zip

        Builds the Lambda layer .zip for the scaffolded layer project, no Docker.
    .EXAMPLE
        Build-LambdaPSRuntimePackage -Path ./hello -OutFile ./hello.zip -Architecture arm64 -PwshArchivePath ./powershell-7.4.20-linux-arm64.tar.gz

        Builds an arm64 .zip of the self-contained function project ./hello from a pre-vetted pwsh tarball.
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.RuntimePackage')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Alias('ProjectPath')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [Alias('PackagePath')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$OutFile,

        [Parameter()]
        [ValidateSet('Layer', 'SelfContainedZip')]
        [string]$Target,

        [Parameter()]
        [ValidatePattern('\A[0-9]+\.[0-9]+\z', ErrorMessage = "'{0}' is not a PowerShell version in major.minor form, for example 7.4.")]
        [string]$PowerShellVersion,

        [Parameter()]
        [ValidatePattern('\A[0-9]+\.[0-9]+\.[0-9]+\z', ErrorMessage = "'{0}' is not a pwsh release in x.y.z form, for example 7.4.20.")]
        [string]$PwshVersion,

        [Parameter()]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture = 'x86_64',

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$PwshArchivePath,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string]$HandlerScript,

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
        # tar is run directly, for its own error IDs; a caller's
        # $PSNativeCommandUseErrorActionPreference would otherwise throw before $LASTEXITCODE is checked.
        $PSNativeCommandUseErrorActionPreference = $false
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.DirectoryNotFoundException]::new("Project directory '$Path' was not found."),
                    'ProjectNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Path))
        }
        $root = Resolve-LambdaPSPath -Path $Path

        # Resolve target and PowerShell version from the marker unless overridden.
        $marker = Get-LambdaPSProjectMarker -Path $root
        # A function project (the scaffolder's manifest.json, no marker) is handler code for a runtime deployed
        # separately: it is packaged with Compress-LambdaPSFunctionPackage, not built here.
        if ($null -eq $marker) {
            $functionManifest = try { Get-LambdaPSFunctionManifest -Path $root } catch { $null }
            if ($null -ne $functionManifest) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new(
                            ("'$Path' is a function project (it has a manifest.json from New-LambdaPSFunctionProject and no " +
                             'lambdaps-runtime.json marker), not a runtime project. Package it with Compress-LambdaPSFunctionPackage.'), 'Path'),
                        'NotARuntimeProject', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path))
            }
        }

        # The bootstrap is written for the scaffolded target (/opt for a layer, /var/task for a function
        # zip), so building it as the other target would give a package that does not start.
        # A CMR or Container project is refused as UnsupportedTarget below, which names the cmdlet to use.
        $markerTarget = if ($marker) { $marker.Target }
        if (-not [string]::IsNullOrWhiteSpace($Target) -and @('Layer', 'SelfContainedZip') -contains $markerTarget -and $markerTarget -ne $Target) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("-Target $Target does not match '$Path', which lambdaps-runtime.json records as a $($marker.Target) project. " +
                         "Its bootstrap is written for $($marker.Target), so the package would not start as $Target. Omit -Target, " +
                         "or scaffold a $Target project."), 'Target'),
                    'TargetMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Target))
        }
        # ValidateSet ignores case, so -Target layer binds; the result reports the canonical spelling.
        $effectiveTarget = if (-not [string]::IsNullOrWhiteSpace($markerTarget)) { $markerTarget }
        else { @('Layer', 'SelfContainedZip') | Where-Object { $_ -eq $Target } }
        if ([string]::IsNullOrWhiteSpace($effectiveTarget)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        "No lambdaps-runtime.json marker was found in '$Path' to say which target it is. Pass -Target Layer or -Target SelfContainedZip.",
                        'Target'),
                    'TargetNotResolved', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path))
        }
        if ($effectiveTarget -ne 'Layer' -and $effectiveTarget -ne 'SelfContainedZip') {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        "Build-LambdaPSRuntimePackage builds only container-free targets (Layer, SelfContainedZip). Target '$effectiveTarget' is not one of them; for CMR or Container use Build-LambdaPSRuntimeImage.",
                        'Target'),
                    'UnsupportedTarget', [System.Management.Automation.ErrorCategory]::InvalidArgument, $effectiveTarget))
        }
        # The handler's modules are staged only for SelfContainedZip; a layer has no handler.
        if ($effectiveTarget -eq 'Layer') {
            $unused = @('HandlerScript', 'MissingModuleAction', 'ModuleRepository') | Where-Object { $PSBoundParameters.ContainsKey($_) }
            if ($unused) {
                Write-Warning ("$((@($unused) | ForEach-Object { "-$_" }) -join ', ') $(if (@($unused).Count -eq 1) { 'does' } else { 'do' }) not apply to a Layer, " +
                    "which has no handler whose '#Requires -Modules' are staged; $(if (@($unused).Count -eq 1) { 'it is' } else { 'they are' }) ignored.")
            }
        }

        $effectiveVersion = $PowerShellVersion
        if ([string]::IsNullOrWhiteSpace($effectiveVersion) -and $marker) {
            $effectiveVersion = $marker.PowerShellVersion
        }
        if ([string]::IsNullOrWhiteSpace($effectiveVersion)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        $(if ($marker) { "The lambdaps-runtime.json marker in '$Path' records no PowerShell version; pass -PowerShellVersion (major.minor)." }
                            else { "'$Path' has no lambdaps-runtime.json marker to give the PowerShell version; pass -PowerShellVersion (major.minor)." }), 'PowerShellVersion'),
                    'PowerShellVersionRequired', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path))
        }

        # The pwsh version the project was scaffolded with, unless -PwshVersion overrides it or
        # -PowerShellVersion asks for a different line.
        $effectivePwshVersion = $PwshVersion
        if ([string]::IsNullOrWhiteSpace($effectivePwshVersion) -and $marker -and $marker.PwshVersion -and
            $marker.PowerShellVersion -eq $effectiveVersion) {
            $effectivePwshVersion = $marker.PwshVersion
        }
        $resolvedVersion = Resolve-LambdaPSPwshVersion -PowerShellVersion $effectiveVersion -PwshVersion $effectivePwshVersion

        $contextSource = [System.IO.Path]::Combine($root, 'PowerShellLambdaContext.cs')
        if (-not (Test-Path -LiteralPath $contextSource -PathType Leaf)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.FileNotFoundException]::new("PowerShellLambdaContext.cs was not found in '$Path'."),
                    'ContextSourceNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $contextSource))
        }

        # The other required project files, checked before the ~70 MB pwsh download rather than after it.
        foreach ($required in @('bootstrap', 'runtime.ps1')) {
            $requiredPath = [System.IO.Path]::Combine($root, $required)
            if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.FileNotFoundException]::new("Required project file '$required' was not found in '$Path'."),
                        'ProjectFileNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $requiredPath))
            }
        }

        # The copy decides links by their real location (Get-LambdaPSRealPath), so a path whose real location
        # is outside the project folder, or cannot be resolved, would not be packaged.
        $realRoot = Get-LambdaPSRealPath -Path $root
        $leavesSource = {
            param([string]$File)
            try { $real = Get-LambdaPSRealPath -Path $File } catch { return $true }
            $prefix = $realRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            -not $real.StartsWith($prefix, [System.StringComparison]::Ordinal)
        }
        # Refused here, before ShouldProcess, so -WhatIf reports it too; the stage is checked again after the copy.
        foreach ($required in @('bootstrap', 'runtime.ps1', 'PowerShellLambdaContext.cs')) {
            $requiredPath = [System.IO.Path]::Combine($root, $required)
            if (& $leavesSource $requiredPath) {
                $errorId = if ($required -eq 'PowerShellLambdaContext.cs') { 'ContextSourceNotFound' } else { 'ProjectFileNotFound' }
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.FileNotFoundException]::new(
                            "Required project file '$required' in '$Path' links outside the project folder, so it would not be packaged. Replace the link with the file."),
                        $errorId, [System.Management.Automation.ErrorCategory]::ObjectNotFound, $requiredPath))
            }
        }

        $destination = Resolve-LambdaPSPath -Path $OutFile
        if (Test-Path -LiteralPath $destination -PathType Container) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.IOException]::new("Output path '$destination' is a directory. Pass the path of the .zip file to write."),
                    'OutFileIsDirectory', [System.Management.Automation.ErrorCategory]::InvalidArgument, $destination))
        }
        if ((Test-Path -LiteralPath $destination) -and -not $Force) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.IOException]::new("Output file '$destination' already exists. Use -Force to overwrite."),
                    'OutFileExists', [System.Management.Automation.ErrorCategory]::ResourceExists, $destination))
        }
        # -Force replaces an existing file, so a -OutFile that is the -PwshArchivePath tarball (by its real path,
        # every link followed) would replace the tarball with the package.
        if (-not [string]::IsNullOrWhiteSpace($PwshArchivePath)) {
            $comparison = if ($IsLinux) { [System.StringComparison]::Ordinal } else { [System.StringComparison]::OrdinalIgnoreCase }
            if ([string]::Equals((Get-LambdaPSRealPath -Path $destination), (Get-LambdaPSRealPath -Path (Resolve-LambdaPSPath -Path $PwshArchivePath)), $comparison)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new("-OutFile '$destination' is the -PwshArchivePath tarball itself, so writing the package would replace it. Write the .zip somewhere else.", 'OutFile'),
                        'OutFileIsSource', [System.Management.Automation.ErrorCategory]::InvalidArgument, $destination))
            }
        }

        # tar extracts the pwsh tarball, so a host without it fails here rather than after the download.
        $tar = Get-LambdaPSTarCommand
        if (-not (Get-Command -Name $tar -CommandType Application -ErrorAction SilentlyContinue)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new(
                        'The tar command was not found on PATH. Build-LambdaPSRuntimePackage uses it to extract the pwsh tarball. Install tar (it ships with Linux, macOS and Windows 10 1803 or later), or build on a host that has it.'),
                    'TarNotFound', [System.Management.Automation.ErrorCategory]::NotInstalled, 'tar'))
        }

        # The tarball checks that need no download run before ShouldProcess, so -WhatIf reports them.
        $archiveParams = @{ PwshVersion = $resolvedVersion; Architecture = $Architecture }
        if (-not [string]::IsNullOrWhiteSpace($PwshArchivePath)) { $archiveParams['PwshArchivePath'] = $PwshArchivePath }
        Get-LambdaPSPwshArchive @archiveParams -ValidateOnly

        # A SelfContainedZip handler's '#Requires -Modules' are staged under modules/, as
        # Compress-LambdaPSFunctionPackage does, and checked before ShouldProcess so -WhatIf runs the same
        # checks. runtime.ps1 is the runtime's own script, so it is never taken for the handler.
        $handlerName = $null
        $requiredModules = @()
        if ($effectiveTarget -eq 'SelfContainedZip') {
            $handlerName = if (-not [string]::IsNullOrWhiteSpace($HandlerScript)) { $HandlerScript }
            else {
                $scripts = @(Get-ChildItem -LiteralPath $root -Filter '*.ps1' -File | Where-Object Name -ne 'runtime.ps1')
                if ($scripts.Count -eq 1) { $scripts[0].Name }
                elseif ($scripts.Count -gt 1) {
                    # Several root scripts are ambiguous: guessing would ship a package without the handler's modules.
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.ArgumentException]::new(
                                ("Could not determine the handler script in '$Path': it has more than one .ps1 at its root " +
                                 "besides runtime.ps1 ($(($scripts.Name | Sort-Object) -join ', ')). " +
                                 'Pass -HandlerScript (for example -HandlerScript handler.ps1).'), 'HandlerScript'),
                            'HandlerNotResolved', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path))
                }
            }
            if ($handlerName) {
                # Found as Compress-LambdaPSFunctionPackage finds it (Resolve-LambdaPSHandlerPath): by its real
                # path, which must be in the project, with its file name matched case-sensitively as on Lambda.
                $handlerFile = Resolve-LambdaPSHandlerPath -Path $root -HandlerScript $handlerName
                if (-not $handlerFile) {
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.IO.FileNotFoundException]::new(
                                "The handler script '$handlerName' is not in the project '$Path', or its real path (with every link followed) is outside it, so it would not be packaged. Fix -HandlerScript, or add the script."),
                            'HandlerScriptNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $handlerName))
                }
                # Its place in the project as named, which is where the copy stages it.
                $handlerRelative = [System.IO.Path]::GetRelativePath($root, [System.IO.Path]::GetFullPath(
                        [System.IO.Path]::Combine($root, $handlerName)))
                # PowerShell's own modules are part of every pwsh, so they are not staged (Select-LambdaPSUnmetModule).
                $requiredModules = @(Select-LambdaPSUnmetModule -Module @(Get-LambdaPSRequiredModule -Path $handlerFile) -ModulesPath ([System.IO.Path]::Combine($root, 'modules')))
                foreach ($spec in $requiredModules) {
                    $pinError = Get-LambdaPSModulePinError -Specification $spec -DeclaredIn $handlerName
                    if ($null -ne $pinError) { $PSCmdlet.ThrowTerminatingError($pinError) }
                }
            }
        }
        # With Stop nothing is downloaded, so a missing module is reported now, before ShouldProcess and the
        # pwsh download, rather than after them.
        if ($MissingModuleAction -eq 'Stop' -and $requiredModules.Count -gt 0) {
            $closure = Get-LambdaPSModuleClosure -Module $requiredModules -RequireAll
            if ($null -ne $closure.Error) { $PSCmdlet.ThrowTerminatingError($closure.Error) }
        }
        # Modules that are not available locally are downloaded here, never into the stage directly.
        $downloadRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-modules-$([System.IO.Path]::GetRandomFileName())")

        $packageAction = if ($MissingModuleAction -eq 'Install' -and $requiredModules.Count -gt 0) {
            "Build container-free $effectiveTarget package, installing missing modules for the current user"
        }
        else { "Build container-free $effectiveTarget package" }
        if (-not $PSCmdlet.ShouldProcess($destination, $packageAction)) {
            # Under -WhatIf, check the module set the way a real run would, without writing anything.
            if ($WhatIfPreference -and $requiredModules.Count -gt 0) {
                $pending = @()
                if ($MissingModuleAction -ne 'Stop') {
                    $pending = @((Save-LambdaPSMissingModule -Module $requiredModules -Path $downloadRoot -MissingModuleAction $MissingModuleAction -ModuleRepository $ModuleRepository).Pending)
                }
                $null = Copy-LambdaPSModuleSet -Module $requiredModules -Path ([System.IO.Path]::Combine($destination, 'modules')) -SearchPath $downloadRoot -Pending $pending
            }
            return
        }
        # Confirmed once: the download, staging and temp cleanup below are part of building the
        # package, so they must not each ask again under -Confirm.
        $ConfirmPreference = 'None'

        # Stage the package tree in a temp directory. The download is inside the try, so a failure after
        # it still removes the tarball.
        $archive = $null
        $stage = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-pkg-$([System.IO.Path]::GetRandomFileName())")
        try {
            # Obtain the verified pwsh tarball (download+verify, or caller-supplied).
            $archive = Get-LambdaPSPwshArchive @archiveParams
            # Only this user can read the staged files in the shared temp folder.
            if ($IsWindows) { $null = New-Item -ItemType Directory -Path $stage -Force }
            else { $null = [System.IO.Directory]::CreateDirectory($stage, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute') }
            # 1. Extract pwsh under powershell/.
            $pwshDir = [System.IO.Path]::Combine($stage, 'powershell')
            $null = New-Item -ItemType Directory -Path $pwshDir -Force
            & $tar -xzf $archive -C $pwshDir
            if ($LASTEXITCODE -ne 0) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new("Failed to extract the pwsh tarball '$archive' (tar exit $LASTEXITCODE)."),
                        'PwshArchiveExtractFailed', [System.Management.Automation.ErrorCategory]::InvalidData, $archive))
            }
            $pwshBinary = [System.IO.Path]::Combine($pwshDir, 'pwsh')
            if (-not (Test-Path -LiteralPath $pwshBinary -PathType Leaf)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.FileNotFoundException]::new("The extracted tarball '$archive' did not contain a 'pwsh' binary."),
                        'PwshBinaryNotFound', [System.Management.Automation.ErrorCategory]::InvalidData, $archive))
            }

            # 2. Stage the project folder with the same rules as Compress-LambdaPSFunctionPackage, so a
            #    handler's own files (lib/, config.json, ...) ship and .git or outside links do not. The
            #    marker and README are scaffolding, and powershell/ and the DLL are this build's outputs.
            $exclude = @('lambdaps-runtime.json', 'README.md', 'PowerShellLambdaContext.dll', 'powershell') |
                ForEach-Object { [System.IO.Path]::Combine($root, $_) }
            # The partial .zip an interrupted run left beside -OutFile is never part of the project.
            $partials = @(Get-ChildItem -LiteralPath ([System.IO.Path]::GetDirectoryName($destination)) -Filter "$([System.IO.Path]::GetFileName($destination)).*.partial" -File -Force -ErrorAction SilentlyContinue | ForEach-Object FullName)
            Copy-LambdaPSFunctionSource -Path $root -Destination $stage -ExcludePath (@($exclude) + $destination + $partials)
            # The required files are checked again in what was staged: one that is a link leaving the project
            # is skipped by the copy, and the package would not start without it.
            foreach ($required in @('bootstrap', 'runtime.ps1', 'PowerShellLambdaContext.cs')) {
                if (-not (Test-Path -LiteralPath ([System.IO.Path]::Combine($stage, $required)) -PathType Leaf)) {
                    $errorId = if ($required -eq 'PowerShellLambdaContext.cs') { 'ContextSourceNotFound' } else { 'ProjectFileNotFound' }
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.IO.FileNotFoundException]::new(
                                "Required project file '$required' in '$Path' was left out by the copy (a link that leaves the folder or loops back into it, a link into .git, or a link to an excluded path; see any warning above). Replace the link with the file."),
                            $errorId, [System.Management.Automation.ErrorCategory]::ObjectNotFound, [System.IO.Path]::Combine($root, $required)))
                }
            }
            if ($handlerName -and -not (Test-Path -LiteralPath ([System.IO.Path]::Combine($stage, $handlerRelative)) -PathType Leaf)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.IO.FileNotFoundException]::new(
                            "The handler script '$handlerName' in '$Path' was left out by the copy (a link that leaves the folder or loops back into it, a link into .git, or a link to an excluded path; see any warning above): it, or a folder above it, is such a link. Replace the link with the script."),
                        'HandlerScriptNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, [System.IO.Path]::Combine($root, $handlerName)))
            }
            if ($requiredModules.Count -gt 0) {
                if ($MissingModuleAction -ne 'Stop') {
                    $null = Save-LambdaPSMissingModule -Module $requiredModules -Path $downloadRoot -MissingModuleAction $MissingModuleAction -ModuleRepository $ModuleRepository
                }
                $null = Copy-LambdaPSModuleSet -Module $requiredModules -Path ([System.IO.Path]::Combine($stage, 'modules')) -SearchPath $downloadRoot
            }
            # What the package will contain, read from the staged tree.
            $modulesStaged = Get-LambdaPSStagedModule -Path ([System.IO.Path]::Combine($stage, 'modules'))
            # The kernel executes the bootstrap directly, so CRLF line endings (a project edited on
            # Windows) make the shebang '/bin/sh\r' and the runtime fails to start with "bad
            # interpreter". Normalize to LF as it goes into the package.
            $stagedBootstrap = [System.IO.Path]::Combine($stage, 'bootstrap')
            $bootstrapText = [System.IO.File]::ReadAllText($stagedBootstrap).Replace("`r`n", "`n")
            [System.IO.File]::WriteAllText($stagedBootstrap, $bootstrapText, [System.Text.UTF8Encoding]::new($false))

            # 3. Precompile the LambdaContext assembly with the BUNDLED pwsh, not the host's (see
            #    Invoke-LambdaPSContextPrecompile). It only runs on a Linux host of the same architecture,
            #    and only if it can start there; otherwise no DLL is shipped and the runtime compiles the
            #    staged PowerShellLambdaContext.cs on each cold start instead.
            $hostArch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }
            $precompiled = $IsLinux -and $hostArch -eq $Architecture -and (Invoke-LambdaPSContextPrecompile -PwshPath $pwshBinary `
                    -SourcePath ([System.IO.Path]::Combine($stage, 'PowerShellLambdaContext.cs')) -OutputPath ([System.IO.Path]::Combine($stage, 'PowerShellLambdaContext.dll')))
            if (-not $precompiled) {
                Write-Warning ("This host cannot run the bundled $Architecture Linux pwsh, so PowerShellLambdaContext.dll " +
                    'is not precompiled. The package still works: the runtime compiles PowerShellLambdaContext.cs on each ' +
                    'cold start, which adds about 1.7 s. Build on a Linux host of the target architecture, with a temp ' +
                    'folder that allows running programs, to avoid that.')
            }

            # 4. Zip the stage. Lambda runs bootstrap and powershell/pwsh directly, so both are executable.
            Write-LambdaPSZipArchive -Path $stage -DestinationPath $destination -ExecutableEntry 'bootstrap', 'powershell/pwsh'
        }
        finally {
            Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $downloadRoot) {
                Remove-Item -LiteralPath $downloadRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
            # A downloaded tarball is ~70 MB and is fetched again (and re-verified) on the next build, so
            # do not leave it in the temp folder. A caller-supplied -PwshArchivePath is theirs to keep.
            if ([string]::IsNullOrWhiteSpace($PwshArchivePath) -and $archive) {
                Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
            }
        }

        [PSCustomObject]@{
            PSTypeName    = 'AWS.ToolExtras.LambdaPSRuntime.RuntimePackage'
            PackagePath   = $destination
            Target        = $effectiveTarget
            PwshVersion   = $resolvedVersion
            Architecture  = $Architecture
            ModulesStaged = $modulesStaged
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
