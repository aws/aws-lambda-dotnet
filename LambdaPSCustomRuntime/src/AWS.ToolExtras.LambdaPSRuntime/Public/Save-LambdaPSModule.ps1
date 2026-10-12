function Save-LambdaPSModule {
    <#
    .SYNOPSIS
        Saves the version-pinned modules (and dependencies) into a folder, downloading what is not
        available locally, without installing anything.
    .DESCRIPTION
        The same module acquisition the scaffold, package and deploy cmdlets run under their default
        -MissingModuleAction Save, exposed so you can fetch the exact versions a function will ship
        with and test against them first:

            Save-LambdaPSModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -Path ./mods
            $env:PSModulePath = (Resolve-Path ./mods).Path + [IO.Path]::PathSeparator + $env:PSModulePath

        Each module, and every transitive RequiredModules dependency, is first looked up in the
        PSModulePath and in -Path. A dependency declared with RequiredVersion is taken at that
        version; one declared with ModuleVersion (a minimum) is taken at the version the set already
        pins for that module, or else at the minimum. Only what is found in neither place is
        downloaded into -Path, laid out as <Name>/<Version>/:
          AWS.Tools.*    with AWS.Tools.Installer 2.x, which downloads the SHA512-checked AWS.Tools.zip
                         from CloudFront. When Installer 2.x is not installed it is itself saved (not
                         installed) with PSResourceGet, from -ModuleRepository or else PSGallery and,
                         on Windows, only when its Authenticode signature is valid, then used from there.
          anything else  with PSResourceGet (Save-PSResource) from -ModuleRepository, or else from
                         your registered repositories in priority order. Repository trust is not
                         bypassed: an untrusted repository prompts, and fails in a non-interactive
                         session. PowerShellGet is not used.

        Modules in the set that are already installed are copied into -Path as well (Action Copied;
        with -ModuleRepository, only AWS.Tools modules are), so -Path always holds the complete set
        at the exact versions and can be tested on its own.

        Nothing is installed and the PSModulePath is not changed. Every specification must pin an exact
        RequiredVersion; minimum-version and version-range specs are rejected, because this tooling
        never resolves 'latest'.

        Under -Confirm it asks once to download and once to copy the installed modules; under -WhatIf
        it lists each download and copy. Declining the download leaves the modules it would have
        fetched out of -Path, with a warning naming them.

        When anything is downloaded, a warning names the modules and says to test them before
        production use; modules that came from PSGallery get a second warning that PSGallery is not
        recommended as a direct production dependency.
        Errors it can stop with (FullyQualifiedErrorId starts with):
          ModuleVersionRequired       a -Module specification without an exact RequiredVersion
          DependencyNameIsPath, DependencyNameMissing, DependencyVersionUnpinned
                                      a RequiredModules entry that names a path, has no name, or
                                      has no usable version
          ModuleVersionConflict, ModuleVersionSpelling, ModuleClosureNotSettled
                                      the set needs one module at versions no single version meets,
                                      a module is present only under another spelling of its version,
                                      or the versions do not settle
          ModuleRepositoryNotFound    a -ModuleRepository name or pattern that matches no registered
                                      repository, or one PSResourceGet cannot use
          ModuleInstallerNotFound, AWSToolsInstallerImportFailed, ModuleDownloadFailed
                                      PSResourceGet is missing, AWS.Tools.Installer cannot be
                                      imported, or a download fails
          PathNotFileSystem, DriveNotFound
                                      -Path is not on the file system, or names a drive that does not
                                      exist
    .PARAMETER Module
        The modules to make available. Each must pin an exact RequiredVersion (for example
        @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }).
    .PARAMETER Path
        The folder to download into. Created when missing.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules from, in order (for example
        an internal mirror). When omitted, every registered repository is searched in priority order,
        so a same-named package in a higher-priority feed would be used. AWS.Tools modules always come
        from the AWS.Tools.zip that AWS.Tools.Installer downloads; when the installer itself must be
        fetched, it comes from these repositories (PSGallery when this is omitted) and, on Windows, only
        when its Authenticode signature is valid.

        Every name, or pattern with `*` wildcards, must match a registered PSResourceGet repository
        (Get-PSResourceRepository), or the cmdlet stops with ModuleRepositoryNotFound before anything
        is downloaded; '?', '[', a backtick and a pattern mixed with names are refused, as
        Save-PSResource cannot use them. With a wildcard, AWS.Tools.Installer, when it must be fetched, comes from any
        matching repository. When it is given, modules other than AWS.Tools are downloaded from these
        repositories even if they are installed, so -Path holds what the repositories serve rather
        than local copies, unless -Path already holds that version.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.DownloadedModule

        One object per module written to -Path: Name, Version, Source (AWS.Tools.Installer,
        PSResourceGet, or Local for an installed module that was copied), Repository (the PSResourceGet
        repository, for example PSGallery), Path and Action (Saved, or Copied). Version is a
        [System.Version]. A module already present in -Path is left as it is and not returned.
        Nothing under -WhatIf.
    .EXAMPLE
        Save-LambdaPSModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -Path ./mods

        Puts AWS.Tools.S3 and AWS.Tools.Common 5.0.274 in ./mods: downloaded, or copied if installed.
    .EXAMPLE
        Save-LambdaPSModule -Module @{ ModuleName = 'PSFramework'; RequiredVersion = '1.12.346' } -Path ./mods -WhatIf

        Shows what would be downloaded without downloading anything.
    #>
    [CmdletBinding(SupportsShouldProcess, PositionalBinding = $false)]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.DownloadedModule')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [ValidateNotNull()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$Module,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [ValidateNotNullOrWhiteSpace()]
        [string[]]$ModuleRepository
    )

    try {
        # Downloads are gated by Save-LambdaPSMissingModule's own ShouldProcess; -WhatIf and -Confirm
        # reach it because $WhatIfPreference / $ConfirmPreference are inherited.
        # Named repositories are checked first, and then used for every module that can come from one:
        # an installed copy is not taken instead.
        if ($ModuleRepository) {
            Assert-LambdaPSModuleRepository -Name $ModuleRepository
        }
        $result = Save-LambdaPSMissingModule -Module $Module -Path $Path -MissingModuleAction Save -ModuleRepository $ModuleRepository -SkipInstalled:([bool]$ModuleRepository)
        $result.Downloaded
        if ($result.Declined.Count -gt 0) {
            Write-Warning "Did not download $($result.Declined -join ', '), because the download was declined, so '$Path' does not hold the complete set."
        }

        # Copy the members of the set that were already installed, so -Path holds the WHOLE set and can
        # be tested on its own, rather than only the modules that had to be downloaded. The download's
        # final walk already resolved them.
        $root = Resolve-LambdaPSPath -Path $Path
        $toCopy = @(foreach ($found in @($result.Resolved | Where-Object { $_.Source -eq 'Local' })) {
                $destination = [System.IO.Path]::Combine($root, $found.Name, $found.Version)
                if (-not (Test-Path -LiteralPath $destination)) {
                    [PSCustomObject]@{ Found = $found; Destination = $destination }
                }
            })
        # One question for the copy stage under -Confirm; -WhatIf lists each copy.
        $copyAll = $false
        if (-not $WhatIfPreference -and $toCopy.Count -gt 0) {
            $copyAll = $PSCmdlet.ShouldProcess(
                "'$root': $(@($toCopy | ForEach-Object { "$($_.Found.Name) $($_.Found.Version)" }) -join ', ')", 'Copy installed modules')
        }
        foreach ($copy in $toCopy) {
            $found = $copy.Found
            if ($WhatIfPreference) {
                $null = $PSCmdlet.ShouldProcess($copy.Destination, "Copy installed module '$($found.Name)' $($found.Version) from '$($found.ModuleBase)'")
            }
            elseif ($copyAll) {
                Copy-LambdaPSModuleFolder -Path $found.ModuleBase -Destination $copy.Destination
                New-LambdaPSDownloadedModule -Name $found.Name -Version $found.Version -Source Local -Path $copy.Destination -Action Copied
            }
        }
    }
    catch {
        # Rethrow from this cmdlet: a helper's terminating error otherwise ends only the
        # statement that called it, and this cmdlet would carry on to its next step.
        $PSCmdlet.ThrowTerminatingError($_)
    }
}
