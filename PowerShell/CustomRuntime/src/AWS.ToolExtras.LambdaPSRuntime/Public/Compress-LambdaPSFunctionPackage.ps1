function Compress-LambdaPSFunctionPackage {
    <#
    .SYNOPSIS
        Packages PowerShell Lambda function code into a deployable .zip, staging any requested and
        handler-declared modules.
    .DESCRIPTION
        The low-level function packager. -Path is your code: a New-LambdaPSFunctionProject
        -Target RuntimeBound project folder, a prepared folder, or a single .ps1 script. The code's files
        (the handler and any modules/ tree) are placed at the ROOT of the archive so the Lambda runtime
        finds them directly. A folder with a lambdaps-runtime.json marker (a CMR, Layer,
        SelfContainedZip or Container project) is refused with NotAFunctionProject: build it with
        Build-LambdaPSRuntimePackage or Build-LambdaPSRuntimeImage instead.

        Modules are staged under modules/ from two sources, combined: the -IncludeModule list, and the
        '#Requires -Modules' declarations read from the handler script (see -HandlerScript). Both go
        through Copy-LambdaPSModuleSet, which stages each (with transitive dependencies) and enforces
        the exact-version-pin rule. One of PowerShell's own modules (Microsoft.PowerShell.Utility, for
        example) is part of every pwsh, so it needs no pin and is not staged. A module that is not available locally is handled according to
        -MissingModuleAction: by default it is downloaded into a temporary folder that is deleted
        afterwards. Staging happens in a temporary tree, so your source directory is never modified.

        The high-level New-LambdaPSFunctionDeployment calls this cmdlet, so both the one-command and
        the manual (scaffold, package, create) paths stage modules the same way.

        For a project New-LambdaPSFunctionProject scaffolded (its top-level manifest.json has the
        scaffolder's HandlerString and ScriptFile), the scaffolding-only 'manifest.json' and
        'README.md' at the top level are dropped from the archive, after manifest.json is read to
        locate the handler script. Any other folder or single .ps1 is packaged as-is, including a
        manifest.json or README.md of your own. A top-level manifest.json that is not valid JSON is a
        terminating InvalidProjectManifest error, since it may be a scaffolded project's.

        A folder is packaged with its hidden files, so keep secrets such as .env files out of it. Always
        left out, silently: .git directories and links into them, -OutFile itself when it is inside the
        folder, the <OutFile>.*.partial files an interrupted run leaves beside it, and links to any of
        these or to another excluded path. Left out with a warning: symbolic links
        that point outside the folder, links to something that does not exist, links whose target
        cannot be resolved (for example two links that point at each other), and folder links back to
        a folder they were reached through (the folder itself, a folder above the link, or two folders
        that link to each other). A .zip at the top of the folder ships, with a warning, since it is
        usually an earlier package. Files that are executable on a Linux or macOS host keep their
        execute bit; everything else is mode 0644.

        Entries are written with System.IO.Compression.ZipArchive, with forward-slash relative paths in
        ordinal order and the same timestamp on each, so the same files with the same PowerShell version
        on the same OS give a byte-identical package. This cmdlet makes no AWS API calls; create the function with
        Publish-LMFunction, or package and deploy in one step with New-LambdaPSFunctionDeployment.

        Errors it can raise, by error ID:
          - The source: ProjectNotFound, SourceNotPowerShell, NotAFunctionProject, InvalidProjectMarker,
            InvalidProjectManifest, HandlerScriptNotFound, InvalidHandlerPath, BackslashInFileName (off Windows,
            a file with '\' in its name).
          - Paths: DriveNotFound, PathNotFileSystem, OutFileExists, OutFileIsDirectory, OutFileIsSource (-OutFile
            is the single-file -Path).
          - Modules (-IncludeModule and the handler's #Requires): ModuleNameInvalid (-IncludeModule or a
            #Requires naming a wildcard or a path), ModuleVersionRequired (a #Requires that does not pin
            an exact RequiredVersion), AWSPowerShellModuleNotSupported (-IncludeModule or a #Requires
            that names the AWSPowerShell or AWSPowerShell.NetCore module), ModuleVersionSpelling (versions that differ only in a trailing .0),
            ModuleVersionConflict, ModuleClosureNotSettled, DependencyNameMissing, DependencyNameIsPath,
            DependencyVersionUnpinned, ModuleNotFoundLocally (-MissingModuleAction Stop),
            ModuleRepositoryNotFound, ModuleDownloadFailed, ModuleFolderIncomplete (a module version
            folder with no <Name>.psd1), ModuleInstallerNotFound,
            AWSToolsInstallerImportFailed.
    .PARAMETER Path
        The function code to package: a folder, or a single .ps1 script. Alias: -ProjectPath.
    .PARAMETER OutFile
        The destination .zip path. Its parent directory is created if missing. Alias: -PackagePath,
        the name of the result field that reports it.
    .PARAMETER IncludeModule
        Modules to stage, combined with any the handler declares via
        '#Requires -Modules'. Each must pin an exact RequiredVersion (enforced by Copy-LambdaPSModuleSet).
        Aliased as -Module.
        A module's dependency declared without a name fails with DependencyNameMissing, and one whose
        version differs from an available version only in a trailing .0 with ModuleVersionSpelling.
        AWSPowerShell (the Windows PowerShell module, which doesn't run in PowerShell 7) and
        AWSPowerShell.NetCore (over 600 MB, more than Lambda's 250 MB package limit) fail with
        AWSPowerShellModuleNotSupported: name the AWS.Tools.* modules for the services you use instead.
    .PARAMETER MissingModuleAction
        What to do with a module (or dependency) that is not available locally:
          Save     (default) download it into a temporary folder, stage it, and delete the folder. The
                   local PowerShell environment is not changed.
          Install  download it the same way, and also install it for the current user so it stays
                   available for local testing.
          Stop     fail with ModuleNotFoundLocally.
        Versions are matched exactly, so a requirement that differs from an available version only in a
        trailing .0 fails with ModuleVersionSpelling.
        AWS.Tools modules are downloaded with AWS.Tools.Installer 2.0.5 or a later 2.x (the
        SHA512-checked AWS.Tools.zip from CloudFront); anything else with PSResourceGet from your
        registered repositories.
        PowerShellGet is never used, and repository trust is not bypassed. See Save-LambdaPSModule.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules from, in order (for example
        an internal mirror). When omitted, every registered repository is searched in priority order,
        so a same-named package in a higher-priority feed would be used. AWS.Tools modules always come
        from the AWS.Tools.zip that AWS.Tools.Installer downloads; when the installer itself must be
        fetched, it comes from these repositories (PSGallery when this is omitted) and, on Windows, only
        when its Authenticode signature is valid. Every name, or pattern with `*` wildcards, must match
        a registered PSResourceGet repository, or the cmdlet stops with ModuleRepositoryNotFound before
        anything is downloaded.
    .PARAMETER HandlerScript
        The handler script whose '#Requires -Modules' to read (for example 'handler.ps1'). When
        omitted, it is resolved from a manifest.json ScriptFile if present, otherwise from the sole
        root .ps1 file; if none can be identified, no requirements are read, with a warning when a root
        script declares '#Requires -Modules'. It is resolved against the
        source folder, and a handler script that is not in -Path (or, for a single-file -Path, is not that
        file) fails with HandlerScriptNotFound, as does one whose real path, with every link followed, is
        outside -Path. The file name is matched case-sensitively, as on Lambda. Off Windows, a '\' in it
        fails with InvalidHandlerPath: separate folders with '/'.
    .PARAMETER Force
        Overwrite the destination .zip if it already exists.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.FunctionPackage

        An object with PackagePath (the full path of the .zip written, as on FunctionDeployment) and
        ModulesStaged: an AWS.ToolExtras.LambdaPSRuntime.StagedModule (Name, and Version as a
        [version]) for every module in the package's modules/ tree -- the -IncludeModule list, the
        '#Requires -Modules' harvest, their transitive dependencies, and any module already present in
        the source's own modules/ folder. It is read from the staged tree, so it is what the package
        actually contains. Nothing under -WhatIf.
    .EXAMPLE
        Compress-LambdaPSFunctionPackage -Path ./hello -OutFile ./hello.zip

        Packages the ./hello project into ./hello.zip with the handler and modules at the root.
    .EXAMPLE
        (Compress-LambdaPSFunctionPackage -Path ./handler.ps1 -OutFile ./handler.zip).ModulesStaged

        Lists every module that ended up in the package, including dependencies.
    .EXAMPLE
        Compress-LambdaPSFunctionPackage -Path ./handler.ps1 -OutFile ./handler-s3.zip -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }

        Zips a single handler script plus the requested module (and any its #Requires declares).
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.FunctionPackage')]
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
        [Alias('Module')]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$IncludeModule = @(),

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
        if (-not (Test-Path -LiteralPath $Path)) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.FileNotFoundException]::new("Function code path '$Path' was not found."),
                    'ProjectNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Path))
        }

        # -Force: a folder or file whose name starts with "." is hidden on Linux and macOS.
        $source = Get-Item -LiteralPath $Path -Force
        $isFile = -not $source.PSIsContainer
        if ($isFile -and $source.Extension -ne '.ps1') {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new("A single-file source must be a .ps1 script; got '$($source.Name)'.", 'Path'),
                    'SourceNotPowerShell', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path))
        }

        # A runtime or self-contained project would be zipped as handler code, without pwsh and with a
        # non-executable bootstrap. New-LambdaPSFunctionDeployment refuses it the same way.
        $marker = if ($isFile) { $null } else { Get-LambdaPSProjectMarker -Path $source.FullName }
        if ($null -ne $marker) {
            $projectTarget = $marker.Target
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new(
                        ("'$Path' is a $(if ($projectTarget) { "$projectTarget " })project (it has a lambdaps-runtime.json marker), not " +
                         'function code. Build a Layer or SelfContainedZip project with Build-LambdaPSRuntimePackage, and a CMR or ' +
                         'Container project with Build-LambdaPSRuntimeImage.'), 'Path'),
                    'NotAFunctionProject', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path))
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
        # -Force replaces an existing file, so a -OutFile that is the source itself (by its real path, every link
        # followed) would replace the code with its own package.
        if ($isFile) {
            $comparison = if ($IsLinux) { [System.StringComparison]::Ordinal } else { [System.StringComparison]::OrdinalIgnoreCase }
            if ([string]::Equals((Get-LambdaPSRealPath -Path $destination), (Get-LambdaPSRealPath -Path $source.FullName), $comparison)) {
                $PSCmdlet.ThrowTerminatingError(
                    [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new("-OutFile '$destination' is the function code '$Path' itself, so writing the package would replace it. Write the .zip somewhere else.", 'OutFile'),
                        'OutFileIsSource', [System.Management.Automation.ErrorCategory]::InvalidArgument, $destination))
            }
        }

        # Resolve the handler script and its '#Requires -Modules' from the source, before ShouldProcess,
        # so -WhatIf runs the same module checks a real run does. The stage mirrors the source, so the
        # handler resolves the same either way; Resolve-LambdaPSHandlerScript is shared with
        # New-LambdaPSFunctionDeployment so the packager and the deployer agree on the entry point.
        # A named handler that is not in the source would package without its declared modules.
        # Resolve-LambdaPSHandlerPath, which New-LambdaPSFunctionDeployment also uses, finds it by its real
        # path and matches its file name case-sensitively, as on Lambda's file system.
        $handlerMissing = $null
        $handlerPath = $null
        $handlerName = if (-not $isFile) { Resolve-LambdaPSHandlerScript -Path $source.FullName -HandlerScript $HandlerScript }
        elseif ([string]::IsNullOrWhiteSpace($HandlerScript)) { $source.Name }
        else { $HandlerScript }
        if (-not [string]::IsNullOrWhiteSpace($handlerName)) {
            # Refused here, before ShouldProcess, so -WhatIf reports it too; the stage is checked again after the copy.
            # A single file with no -HandlerScript is its own handler, so its name is not matched again.
            $handlerPath = Resolve-LambdaPSHandlerPath -Path $source.FullName -HandlerScript $(if ($isFile) { $HandlerScript } else { $handlerName })
            if (-not $handlerPath) { $handlerMissing = $handlerName }
            elseif (-not $isFile) {
                # The handler's place in the source as named, which is where the copy stages it; $handlerPath
                # is its real path, which differs when the source is reached through a link.
                $handlerRelative = [System.IO.Path]::GetRelativePath($source.FullName, [System.IO.Path]::GetFullPath(
                    [System.IO.Path]::Combine($source.FullName, $handlerName)))
            }
        }
        if ($handlerMissing) {
            $PSCmdlet.ThrowTerminatingError(
                [System.Management.Automation.ErrorRecord]::new(
                    [System.IO.FileNotFoundException]::new(
                        "The handler script '$handlerMissing' is not in the function source '$Path', or its real path (with every link followed) is outside it, so its '#Requires -Modules' cannot be read. Fix -HandlerScript (or the manifest.json ScriptFile), or add the script."),
                    'HandlerScriptNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $handlerMissing))
        }
        # Several root scripts, and no manifest.json ScriptFile or -HandlerScript to say which is the handler: no
        # script's '#Requires -Modules' is read. A Module:: handler names no script, so this is a warning, not an error.
        if (-not $isFile -and [string]::IsNullOrWhiteSpace($handlerName)) {
            $declaring = @(Get-ChildItem -LiteralPath $source.FullName -Filter '*.ps1' -File | Where-Object {
                    try { @(Get-LambdaPSRequiredModule -Path $_.FullName).Count -gt 0 } catch { $false }
                } | ForEach-Object Name | Sort-Object)
            if ($declaring.Count -gt 0) {
                Write-Warning ("The handler script in '$Path' could not be identified (more than one root .ps1 and no manifest.json " +
                    "ScriptFile), so the '#Requires -Modules' in $($declaring -join ', ') are not read and those modules are not " +
                    'packaged. Name the handler script: -HandlerScript, or the script in -Handler when deploying.')
            }
        }
        $required = @()
        if ($handlerPath) {
            # PowerShell's own modules are part of every pwsh, so they are not staged (Select-LambdaPSUnmetModule).
            $shippedModules = if (-not $isFile) { [System.IO.Path]::Combine($source.FullName, 'modules') }
            $required = @(Select-LambdaPSUnmetModule -Module @(Get-LambdaPSRequiredModule -Path $handlerPath) -ModulesPath $shippedModules)
            # Checked here rather than with -IncludeModule below, so the error names the script that
            # declared the module: the caller never typed it.
            # Named as the caller named it: $handlerPath is the real path, which a link can give another name.
            $declaredIn = $handlerName
            foreach ($spec in $required) {
                $pinError = Get-LambdaPSModulePinError -Specification $spec -DeclaredIn $declaredIn
                if ($null -ne $pinError) { $PSCmdlet.ThrowTerminatingError($pinError) }
            }
        }
        $combined = @($IncludeModule) + @($required)

        # Modules that are not available locally are downloaded here, never into the stage (which is
        # zipped whole) and never into a module path unless -MissingModuleAction is Install.
        $downloadRoot = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-modules-$([System.IO.Path]::GetRandomFileName())")

        # Confirming the package confirms everything under it, so say when that includes installing
        # missing modules into the current user's module path.
        $packageAction = if ($MissingModuleAction -eq 'Install' -and $combined.Count -gt 0) {
            'Create function package, installing missing modules for the current user'
        }
        else { 'Create function package' }
        if (-not $PSCmdlet.ShouldProcess($destination, $packageAction)) {
            # Under -WhatIf, check the module set the way a real run would (exact pins, dependencies,
            # availability) and preview the downloads and copies, without writing anything.
            if ($WhatIfPreference -and $combined.Count -gt 0) {
                $pending = @()
                if ($MissingModuleAction -ne 'Stop') {
                    $pending = @((Save-LambdaPSMissingModule -Module $combined -Path $downloadRoot -MissingModuleAction $MissingModuleAction -ModuleRepository $ModuleRepository).Pending)
                }
                $null = Copy-LambdaPSModuleSet -Module $combined -Path ([System.IO.Path]::Combine($destination, 'modules')) -SearchPath $downloadRoot -Pending $pending
            }
            return
        }
        # Confirmed once: the staging copies, module downloads and temp cleanup below are part of
        # creating the package, so they must not each ask again under -Confirm.
        $ConfirmPreference = 'None'

        # Stage into a temp tree so modules/ can be added without mutating the caller's source.
        $stage = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-fn-$([System.IO.Path]::GetRandomFileName())")
        # Only this user can read the staged files in the shared temp folder.
        if ($IsWindows) { $null = New-Item -ItemType Directory -Path $stage -Force }
        else { $null = [System.IO.Directory]::CreateDirectory($stage, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute') }
        try {
            if ($isFile) {
                Copy-Item -LiteralPath $source.FullName -Destination ([System.IO.Path]::Combine($stage, $source.Name)) -Force
            }
            else {
                # The partial .zip an interrupted run left beside -OutFile is never part of the code.
                $partials = @(Get-ChildItem -LiteralPath ([System.IO.Path]::GetDirectoryName($destination)) -Filter "$([System.IO.Path]::GetFileName($destination)).*.partial" -File -Force -ErrorAction SilentlyContinue | ForEach-Object FullName)
                Copy-LambdaPSFunctionSource -Path $source.FullName -Destination $stage -ExcludePath (@($destination) + $partials)
            }

            # The handler is checked again in what was staged: one that is a link leaving the source is
            # skipped by the copy, and the package would ship without its entry point.
            if ($handlerPath -and -not $isFile) {
                $stagedHandler = [System.IO.Path]::Combine($stage, $handlerRelative)
                if (-not (Test-Path -LiteralPath $stagedHandler -PathType Leaf)) {
                    $PSCmdlet.ThrowTerminatingError(
                        [System.Management.Automation.ErrorRecord]::new(
                            [System.IO.FileNotFoundException]::new(
                                "The handler script '$handlerRelative' in '$Path' was left out by the copy (a link that leaves the folder or loops back into it, a link into .git, or a link to an excluded path; see any warning above): it, or a folder above it, is such a link. Replace the link with the script."),
                            'HandlerScriptNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $handlerPath))
                }
            }

            if ($combined.Count -gt 0) {
                if ($MissingModuleAction -ne 'Stop') {
                    $null = Save-LambdaPSMissingModule -Module $combined -Path $downloadRoot -MissingModuleAction $MissingModuleAction -ModuleRepository $ModuleRepository
                }
                $null = Copy-LambdaPSModuleSet -Module $combined -Path ([System.IO.Path]::Combine($stage, 'modules')) -SearchPath $downloadRoot
            }

            # Record what the package will contain, read from the staged tree (not from the input lists,
            # which omit transitive dependencies and modules already in the source's modules/ folder).
            $modulesStaged = Get-LambdaPSStagedModule -Path ([System.IO.Path]::Combine($stage, 'modules'))

            # Scaffolding-only metadata is dropped from the payload, but ONLY for a project the scaffolder
            # wrote (its manifest.json has the scaffolder's shape). Any other folder or single file is
            # packaged as-is, so a user's own manifest.json or README.md is never silently omitted. Only
            # top-level files are dropped; nested files of the same name are payload. The zip replaces
            # -OutFile only once complete.
            $isScaffolded = -not $isFile -and $null -ne (Get-LambdaPSFunctionManifest -Path $source.FullName)
            $exclude = if ($isScaffolded) { @('manifest.json', 'README.md') } else { @() }
            Write-LambdaPSZipArchive -Path $stage -DestinationPath $destination -ExcludeTopLevel $exclude
        }
        finally {
            Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $downloadRoot) {
                Remove-Item -LiteralPath $downloadRoot -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        [PSCustomObject]@{
            PSTypeName    = 'AWS.ToolExtras.LambdaPSRuntime.FunctionPackage'
            PackagePath   = $destination
            ModulesStaged = $modulesStaged
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
