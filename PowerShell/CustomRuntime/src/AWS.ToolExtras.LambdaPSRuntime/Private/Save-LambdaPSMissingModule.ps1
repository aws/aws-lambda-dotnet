function Save-LambdaPSMissingModule {
    <#
    .SYNOPSIS
        Downloads the modules in a version-pinned set that are not available locally, with their
        dependencies, into a folder -- and optionally installs them for the current user.
    .DESCRIPTION
        The shared implementation of -MissingModuleAction Save and Install, and of Save-LambdaPSModule.
        It walks the set with Get-LambdaPSModuleClosure, resolving each module from the PSModulePath
        and then from -Path. Every missing module is downloaded into -Path at its exact version:
          AWS.Tools.*    with AWS.Tools.Installer 2.0.5 or a later 2.x (Save-LambdaPSAWSToolsModule),
                         grouped into one call per version, because every call downloads the whole
                         AWS.Tools.zip
          anything else  with PSResourceGet (Save-LambdaPSModuleFromRepository); AWS.Tools.Installer
                         needed as a dependency comes, like the bootstrapped one, from
                         -ModuleRepository or PSGallery, signature-checked on Windows
        The walk then runs again, because a downloaded module's dependencies are only known once its
        manifest is on disk, until nothing is missing. A module that only ModuleVersion entries ask for
        is downloaded once nothing else is missing, because a module still to be downloaded may need it
        at a higher version. A module that is still missing after it was downloaded is a terminating
        ModuleDownloadFailed error.

        -MissingModuleAction Save leaves the local PowerShell environment untouched.
        -MissingModuleAction Install additionally copies each downloaded module (and a bootstrapped
        AWS.Tools.Installer) into the CurrentUser module folder when that version is not already
        there; an existing version folder is never overwritten.

        When anything is downloaded, two warnings are written, once each: that the modules should be
        tested before production use, and -- for modules that came from PSGallery -- that PSGallery is
        not recommended as a direct production dependency.

        Downloads and installs honor ShouldProcess, one question per stage: -Confirm asks once before
        the first download (naming the modules and "any dependencies they need", which later passes
        download under the same answer) and, with Install, once before installing. Under -WhatIf each
        download is listed and nothing is downloaded; the modules that would be are returned as Pending
        (their own dependencies cannot be known) so Copy-LambdaPSModuleSet can preview their copies
        instead of failing. A download declined under -Confirm downloads nothing more, and the modules
        still missing are returned as Declined, not Pending: they are not on disk, and a caller that
        stages the set must not treat them as previewed.

        Returns one object: Downloaded (AWS.ToolExtras.LambdaPSRuntime.DownloadedModule objects for the
        modules this run downloaded that are part of the set), Pending ('Name Version' keys, under
        -WhatIf only), Declined ('Name Version' keys of the modules a declined download left missing)
        and Resolved (the final walk's resolved modules, as Get-LambdaPSModuleClosure returns them, so
        a caller need not walk the set again).
    .PARAMETER Module
        The module specifications to resolve. Each must pin an exact RequiredVersion.
    .PARAMETER Path
        The folder to download into, laid out as <Name>/<Version>/. Created on the first download.
    .PARAMETER ModuleRepository
        The PSResourceGet repositories to download non-AWS.Tools modules (and AWS.Tools.Installer)
        from. When omitted, every registered repository is searched in priority order for modules, and
        AWS.Tools.Installer comes from PSGallery only.
    .PARAMETER MissingModuleAction
        Save (download into -Path only) or Install (also install into the CurrentUser module folder).
    .PARAMETER SkipInstalled
        Download modules other than AWS.Tools.* even when they are installed, so they come from
        -ModuleRepository (Save-LambdaPSModule passes this whenever -ModuleRepository is given).
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        Save-LambdaPSMissingModule -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -Path $temp

        Downloads AWS.Tools.S3 (and AWS.Tools.Common) 5.0.274 into $temp unless they are installed.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$Module,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [ValidateSet('Save', 'Install')]
        [string]$MissingModuleAction = 'Save',

        [Parameter()]
        [AllowNull()]
        [string[]]$ModuleRepository,

        [Parameter()]
        [switch]$SkipInstalled
    )

    $root = Resolve-LambdaPSPath -Path $Path
    # A bootstrapped AWS.Tools.Installer goes to its own temporary folder, deleted when this returns,
    # so it is not left behind in Save-LambdaPSModule's output folder.
    $installerPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "lambdaps-installer-$([System.IO.Path]::GetRandomFileName())")
    $downloadedKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $attempted = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $origin = @{}
    $pending = [System.Collections.Generic.List[string]]::new()
    $declined = [System.Collections.Generic.List[string]]::new()
    $installedInstaller = [System.Collections.Generic.List[string]]::new()
    # The download stage's answer under -Confirm: asked once, before the first download.
    $downloadConfirmed = $null
    $closure = $null

    try {
        while ($true) {
            # The walk's warnings (a built-in named in -Module) are the same on every pass: show them once.
            $walkWarnings = if ($null -eq $closure) { @{} } else { @{ WarningAction = 'SilentlyContinue' } }
            $closure = Get-LambdaPSModuleClosure -Module $Module -SearchPath @($root) -Pending $pending.ToArray() -SkipInstalled:$SkipInstalled @walkWarnings
            if ($null -ne $closure.Error) {
                # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
                # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
                # caller's -ErrorVariable naming this internal helper.
                throw $closure.Error
            }
            if ($closure.Missing.Count -eq 0) {
                break
            }

            # A module only minimums ask for waits until nothing else is missing: a module downloaded
            # in this pass may pin it higher, and fetching the minimum first would be a wasted download
            # (or a failed one, when the minimum is not in the repository).
            $fetch = @($closure.Missing | Where-Object { -not $_.MinimumOnly })
            if ($fetch.Count -eq 0) {
                $fetch = @($closure.Missing)
            }
            foreach ($item in @($closure.Missing | Where-Object { $fetch -notcontains $_ })) {
                Write-Verbose "Waiting to download '$($item.Name)' $($item.Version): only minimum versions ask for it, and a module downloaded first may need it higher."
            }

            foreach ($item in $fetch) {
                if ($attempted.Contains($item.Key)) {
                    $message = "Module '$($item.Name)' $($item.Version) was downloaded, but " +
                        "'$([System.IO.Path]::Combine($root, $item.Name, $item.Version, "$($item.Name).psd1"))' was not found."
                    throw [System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new($message),
                        'ModuleDownloadFailed', [System.Management.Automation.ErrorCategory]::ResourceUnavailable, $item.Key)
                }
                $null = $attempted.Add($item.Key)
            }

            if (-not $WhatIfPreference -and $null -eq $downloadConfirmed) {
                $listed = @($fetch | ForEach-Object { $_.Key })
                $downloadConfirmed = $PSCmdlet.ShouldProcess(
                    "'$root': $($listed.Count) module(s): $($listed -join ', '), and any dependencies they need", 'Download modules')
            }
            # Declined: nothing is downloaded. Every module still missing is reported as declined, including
            # one that only minimums ask for, which this answer would have downloaded later.
            if (-not $WhatIfPreference -and -not $downloadConfirmed) {
                foreach ($item in $closure.Missing) { $declined.Add($item.Key) }
                break
            }

            $isAwsTools = { param($m) $m.Name -like 'AWS.Tools.*' -and $m.Name -ne 'AWS.Tools.Installer' }
            $awsTools = @($fetch | Where-Object { & $isAwsTools $_ })
            $others = @($fetch | Where-Object { -not (& $isAwsTools $_) })

            foreach ($group in @($awsTools | Group-Object -Property Version)) {
                $names = @($group.Group | ForEach-Object { $_.Name })
                $version = $group.Name
                $go = if ($WhatIfPreference) { $PSCmdlet.ShouldProcess("$($names -join ', ') $version", "Download with AWS.Tools.Installer to '$root'") } else { $downloadConfirmed }
                if ($go) {
                    Write-Verbose "Downloading $($names -join ', ') $version (and AWS.Tools.Common) with AWS.Tools.Installer to '$root'."
                    # The installer always brings AWS.Tools.Common at the same version. A version folder
                    # already in -Path is kept as it is, so it is not reported as downloaded.
                    $already = @(@($names + 'AWS.Tools.Common') | Where-Object {
                            Test-Path -LiteralPath ([System.IO.Path]::Combine($root, $_, $version)) })
                    Save-LambdaPSAWSToolsModule -Name $names -Version $version -Path $root -InstallerPath $installerPath -ModuleRepository $ModuleRepository
                    foreach ($name in @($names + 'AWS.Tools.Common' | Where-Object { $already -notcontains $_ })) {
                        $key = "$name $version"
                        $null = $downloadedKeys.Add($key)
                        $origin[$key] = @{ Source = 'AWS.Tools.Installer'; Repository = $null }
                    }
                }
                else {
                    foreach ($item in $group.Group) { $pending.Add($item.Key) }
                }
            }

            foreach ($item in $others) {
                $go = if ($WhatIfPreference) { $PSCmdlet.ShouldProcess($item.Key, "Download with PSResourceGet to '$root'") } else { $downloadConfirmed }
                if ($go) {
                    Write-Verbose "Downloading '$($item.Name)' $($item.Version) with PSResourceGet to '$root'."
                    $from = @{ ModuleRepository = $ModuleRepository }
                    if ($item.Name -eq 'AWS.Tools.Installer') {
                        # A dependency on the Installer follows the bootstrap's rule, since its code runs here:
                        # -ModuleRepository or else PSGallery, and on Windows only with a valid signature.
                        $from = @{ ModuleRepository = $(if ($ModuleRepository) { $ModuleRepository } else { @('PSGallery') }); AuthenticodeCheck = [bool]$IsWindows; InstallerDefault = -not $ModuleRepository }
                    }
                    $saved = Save-LambdaPSModuleFromRepository -Name $item.Name -VersionRange "[$($item.Version)]" -Path $root @from
                    $null = $downloadedKeys.Add($item.Key)
                    $origin[$item.Key] = @{ Source = 'PSResourceGet'; Repository = $saved.Repository }
                }
                else {
                    $pending.Add($item.Key)
                }
            }
        }

        # Report only modules this run downloaded that the set actually uses, and that resolve from -Path:
        # a module the installer brought along that was already installed elsewhere is not reported. Test
        # the location rather than Source, because when -Path is on the PSModulePath a downloaded module
        # resolves as Local.
        $comparison = if ($IsWindows) { [System.StringComparison]::OrdinalIgnoreCase } else { [System.StringComparison]::Ordinal }
        $rootPrefix = $root.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $downloaded = @(
            foreach ($found in @($closure.Resolved | Where-Object {
                        $downloadedKeys.Contains($_.Key) -and
                        [System.IO.Path]::GetFullPath($_.ModuleBase).StartsWith($rootPrefix, $comparison) })) {
                New-LambdaPSDownloadedModule -Name $found.Name -Version $found.Version -Source $origin[$found.Key].Source `
                    -Repository $origin[$found.Key].Repository -Path $found.ModuleBase -Action Saved
            }
        )

        $installRoot = $null
        if ($MissingModuleAction -eq 'Install' -and $downloaded.Count -gt 0) {
            $installRoot = Get-LambdaPSCurrentUserModulePath
            # What would be installed: downloaded modules, and the AWS.Tools.Installer this run
            # bootstrapped (so the next run finds it), wherever that version is not already there. An
            # existing version folder is never overwritten.
            $toInstall = [System.Collections.Generic.List[object]]::new()
            foreach ($entry in $downloaded) {
                $destination = [System.IO.Path]::Combine($installRoot, $entry.Name, $entry.Version)
                if (-not (Test-Path -LiteralPath $destination)) {
                    $toInstall.Add([PSCustomObject]@{ Label = "$($entry.Name) $($entry.Version)"; Source = $entry.Path; Destination = $destination; Entry = $entry })
                }
            }
            $savedInstaller = [System.IO.Path]::Combine($installerPath, 'AWS.Tools.Installer')
            if (Test-Path -LiteralPath $savedInstaller -PathType Container) {
                foreach ($versionDir in @(Get-ChildItem -LiteralPath $savedInstaller -Directory)) {
                    $destination = [System.IO.Path]::Combine($installRoot, 'AWS.Tools.Installer', $versionDir.Name)
                    if (-not (Test-Path -LiteralPath $destination)) {
                        $toInstall.Add([PSCustomObject]@{ Label = "AWS.Tools.Installer $($versionDir.Name)"; Source = $versionDir.FullName; Destination = $destination; Entry = $null })
                    }
                }
            }

            # One question for the install stage. Report Installed only for a copy that happened: a
            # declined -Confirm leaves the modules saved only.
            if ($toInstall.Count -gt 0 -and
                $PSCmdlet.ShouldProcess("'$installRoot': $(@($toInstall | ForEach-Object { $_.Label }) -join ', ')", 'Install modules for the current user')) {
                foreach ($install in $toInstall) {
                    Copy-LambdaPSModuleFolder -Path $install.Source -Destination $install.Destination
                    if ($install.Entry) {
                        $install.Entry.Path = $install.Destination
                        $install.Entry.Action = 'Installed'
                    }
                    else {
                        $installedInstaller.Add($install.Label)
                    }
                }
            }
        }

        if ($downloaded.Count -gt 0 -or $pending.Count -gt 0) {
            $listed = @($downloaded | ForEach-Object { "$($_.Name) $($_.Version)" }) + @($pending)
            $verb = if ($downloaded.Count -gt 0) { 'Downloaded' } else { 'Would download' }
            $testWarning = "$verb modules that were not available locally: $($listed -join ', '). " +
                'Test your function with these exact module versions before production use.'
            # The bootstrapped Installer is installed too; name it, or the user never learns it was.
            $installed = @($downloaded | Where-Object { $_.Action -eq 'Installed' } | ForEach-Object { "$($_.Name) $($_.Version)" }) +
                @($installedInstaller)
            if ($installed.Count -gt 0) {
                $testWarning += " Installed for the current user in '$installRoot': $($installed -join ', ')."
            }
            Write-Warning $testWarning

            $fromGallery = @($downloaded | Where-Object { $_.Repository -eq 'PSGallery' })
            if ($fromGallery.Count -gt 0) {
                Write-Warning ("$(($fromGallery | ForEach-Object { "$($_.Name) $($_.Version)" }) -join ', ') " +
                    'came from the PowerShell Gallery (PSGallery). PSGallery has no availability SLA and is not ' +
                    'recommended as a direct dependency for production deployments. Mirror these modules to a ' +
                    'private repository, or install them locally so they do not have to be downloaded.')
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $installerPath) {
            Remove-Item -LiteralPath $installerPath -Recurse -Force -ErrorAction SilentlyContinue -WhatIf:$false -Confirm:$false
            if (Test-Path -LiteralPath $installerPath) {
                # On Windows the imported installer's assembly stays locked until the session ends.
                Write-Verbose "Could not remove the temporary AWS.Tools.Installer folder '$installerPath'; delete it after this session ends."
            }
        }
    }

    [PSCustomObject]@{
        Downloaded = $downloaded
        Pending    = $pending.ToArray()
        Declined   = $declined.ToArray()
        Resolved   = @($closure.Resolved)
    }
}
