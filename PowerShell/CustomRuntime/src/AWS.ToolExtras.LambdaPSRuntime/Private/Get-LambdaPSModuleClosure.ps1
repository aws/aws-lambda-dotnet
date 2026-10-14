function Get-LambdaPSModuleClosure {
    <#
    .SYNOPSIS
        Walks a version-pinned module set and its transitive dependencies without changing anything.
    .DESCRIPTION
        The single dependency walk shared by Copy-LambdaPSModuleSet (which stages the result) and
        Save-LambdaPSMissingModule (which downloads what is missing, then walks again). Each module
        is resolved with Find-LambdaPSModule and its RequiredModules are followed. A RequiredVersion
        entry is followed at that exact version. A ModuleVersion entry is a minimum: it is satisfied by
        the version the set otherwise needs that module at (a request's pin, or another dependency's),
        and when nothing else needs the module it is followed at the minimum itself. This tooling never
        picks a version beyond those, so a dependency with no version stops the walk, and requirements
        no single version meets are a ModuleVersionConflict.

        Returns one object:
          Resolved  every module found, in walk order (Find-LambdaPSModule output plus Key)
          Missing   modules found in neither the PSModulePath nor -SearchPath (Name, Version, Key,
                    MinimumOnly); their dependencies are unknown until they are on disk, so they are not
                    walked. MinimumOnly is true when only ModuleVersion entries ask for the module.
          Pending   the -Pending keys the walk reached; treated as present but not walked (used under
                    -WhatIf, where a download is only previewed)
          Error     a ready-to-throw ModuleNameInvalid, AWSPowerShellModuleNotSupported,
                    ModuleVersionRequired, ModuleGuidMismatch, DependencyNameIsPath, DependencyNameMissing,
                    DependencyVersionUnpinned, ModuleVersionConflict, ModuleVersionSpelling,
                    ModuleClosureNotSettled or (with -RequireAll) ModuleNotFoundLocally ErrorRecord, or
                    $null. It is returned rather than thrown so the error is attributed to the calling
                    cmdlet.

        A -Module name that is a wildcard (*, ? or [) or a path (rooted, containing a / or \, or made
        only of dots, such as . or ..) is returned as a ModuleNameInvalid Error: each module must be
        named exactly.
        A -Module specification that names AWSPowerShell or AWSPowerShell.NetCore (any case, with or
        without a version) is returned as an AWSPowerShellModuleNotSupported Error, giving each
        module's reason (Get-LambdaPSAWSPowerShellModuleReason): AWSPowerShell does not run in
        PowerShell 7, and AWSPowerShell.NetCore is over 600 MB installed. Every other -Module
        specification must pin an exact RequiredVersion (Get-LambdaPSModulePinError); the first that
        does not is returned as Error before anything is walked. Every caller that downloads, stages
        or checks a module set walks it here, so this is the one place these rules are enforced for
        -IncludeModule and -Module.

        PowerShell's own modules (Test-LambdaPSBuiltInModule: Microsoft.PowerShell.Utility,
        .Management, .Security, .Host, .Diagnostics, .Core, CimCmdlets, Microsoft.WSMan.Management,
        PSDiagnostics; some exist only on Windows) are never published separately, so they are not
        followed or staged, with or without a version. A dependency on one is skipped silently; one
        named directly in -Module is skipped with a warning. Modules that also ship with PowerShell
        but are updated on their own (PSReadLine, Microsoft.PowerShell.ThreadJob, PSResourceGet,
        PowerShellGet, PackageManagement, Microsoft.PowerShell.Archive) are followed like any other,
        because the version on this machine need not be the version on Lambda.

        Versions are matched exactly, as PowerShell matches them: '1.0' and '1.0.0' are different
        versions, for a RequiredVersion pin, a RequiredModules entry, a ModuleVersion minimum and the
        folder and key spelling. A pin and a dependency that spell one version differently are a
        ModuleVersionConflict. A module that is present only under the other spelling is a
        ModuleVersionSpelling error naming both spellings and the one to use, since PowerShell would
        not load it. A minimum is met by a higher spelling (1.0.0 for 1.0 or later), as PowerShell
        allows.

        A dependency version that is not a version number is a DependencyVersionUnpinned error, like
        a dependency with no version: it would otherwise reach the download as a version range.

        A version chosen for a minimum is kept between passes while a requirement reached in the
        current pass still asks for it; when the module that raised it drops out of the set, the
        choice is released. A module whose choice has gone down and then back up is swinging, and
        its choice is never lowered from then on, so each swing settles on its higher version
        whatever the other modules are doing (ModuleClosureNotSettled only after the pass limit).
        When a kept higher choice breaks a maximum, that is a ModuleVersionConflict.

        A RequiredModules entry that names a path (rooted, containing a / or \, or made only of dots)
        rather than a module is a DependencyNameIsPath error: it would resolve from wherever the path
        points, or from the current directory, and would not load on Lambda.

        A -Module specification or RequiredModules entry that names a GUID is met only by the module
        with that GUID, as Get-Module -FullyQualifiedName matches it. When the name and version are
        present only with another GUID, the walk stops with ModuleGuidMismatch. Requirements without a
        GUID match by name and version alone.

        A HashSet keyed on "Name Version" dedupes modules and guards against dependency cycles. Makes
        no network call and writes nothing.
    .PARAMETER Module
        The module specifications to walk. Each must pin an exact RequiredVersion.
    .PARAMETER SearchPath
        Extra folders to resolve modules from, after the PSModulePath.
    .PARAMETER Pending
        'Name Version' keys to treat as present without walking their dependencies.
    .PARAMETER RequireAll
        Return a ModuleNotFoundLocally Error when any module is Missing, for callers that need the
        whole set on disk now rather than a list of what to download.
    .PARAMETER SkipInstalled
        Resolve modules other than AWS.Tools.* from -SearchPath only, not from the PSModulePath, so
        they are downloaded from the named repositories rather than copied from this machine.
        AWS.Tools modules never come from a repository and are still resolved from both.
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        (Get-LambdaPSModuleClosure -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }).Missing

        Lists the modules in the set that are not available locally.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [Microsoft.PowerShell.Commands.ModuleSpecification[]]$Module,

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$SearchPath = @(),

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$Pending = @(),

        [Parameter()]
        [switch]$RequireAll,

        [Parameter()]
        [switch]$SkipInstalled
    )

    # A -Module name must name one module. Get-Module -FullyQualifiedName would also take a wildcard
    # (and use the first module it matches) or a path (which does not load on Lambda; '.' and '..' are
    # read as folders relative to the current location), and either would get past the checks below. A dependency that names a path is refused the same way
    # (DependencyNameIsPath).
    foreach ($spec in $Module) {
        if ([System.IO.Path]::IsPathRooted($spec.Name) -or $spec.Name.IndexOfAny([char[]]@('/', '\', '*', '?', '[')) -ge 0 -or
            $spec.Name.Trim('.').Length -eq 0) {
            $message = "Module '$($spec.Name)' is a wildcard or a path, not a module name. Name each module " +
                "exactly (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '<version>' })."
            $nameError = [System.Management.Automation.ErrorRecord]::new(
                [System.ArgumentException]::new($message, 'Module'),
                'ModuleNameInvalid', [System.Management.Automation.ErrorCategory]::InvalidArgument, $spec.Name)
            return [PSCustomObject]@{ Resolved = @(); Missing = @(); Pending = @(); Error = $nameError }
        }
    }

    # AWSPowerShell and AWSPowerShell.NetCore are refused as for a handler's #Requires
    # (Get-LambdaPSRequiredModule), with each module's reason from Get-LambdaPSAWSPowerShellModuleReason.
    # Checked before the pin, so a bare name gets this advice rather than a request for a version.
    $monolithic = Get-LambdaPSAWSPowerShellModuleReason -Name @($Module.Name)
    if ($null -ne $monolithic) {
        $subject = if ($monolithic.Names.Count -eq 1) { 'Module' } else { 'Modules' }
        $message = "$subject $(@($monolithic.Names | ForEach-Object { "'$_'" }) -join ' and ') (-IncludeModule or -Module) " +
            "cannot be packaged for Lambda: $($monolithic.Reason). Require the AWS.Tools.* modules for the services you use " +
            "instead (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '<version>' }), each pinned with a " +
            'RequiredVersion.'
        $monolithicError = [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new($message, 'Module'),
            'AWSPowerShellModuleNotSupported', [System.Management.Automation.ErrorCategory]::InvalidArgument, $monolithic.Names[0])
        return [PSCustomObject]@{ Resolved = @(); Missing = @(); Pending = @(); Error = $monolithicError }
    }

    foreach ($spec in $Module) {
        $pinError = Get-LambdaPSModulePinError -Specification $spec
        if ($null -ne $pinError) {
            return [PSCustomObject]@{ Resolved = @(); Missing = @(); Pending = @(); Error = $pinError }
        }
    }

    $pendingSet = [System.Collections.Generic.HashSet[string]]::new([string[]]@($Pending), [System.StringComparer]::OrdinalIgnoreCase)

    # A dependency declared with ModuleVersion is a minimum, not a pin. The walk first stages it at
    # that minimum. If the same module is also needed at another version, and one version satisfies
    # every requirement (the exact pin, or else the highest minimum, within any MaximumVersion), the
    # walk is repeated with minimums resolved to that version. Only requirements that no single version
    # meets are a ModuleVersionConflict. $chosen maps a module name to the version minimums resolve to.
    $chosen = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    # Versions are matched exactly, as PowerShell matches them: '1.0' and '1.0.0' are different
    # versions. The other spellings of a version (a trailing .0 added or removed) are only looked up to
    # explain why a module that is present does not count.
    $otherSpellings = {
        param([string]$Value)
        $parsed = $null
        if (-not [version]::TryParse($Value, [ref]$parsed)) { return }
        $parts = @($Value.Split('.'))
        if ($parts.Count -lt 4) { "$Value.0" }
        if ($parts.Count -gt 2 -and $parts[-1] -eq '0') { ($parts[0..($parts.Count - 2)]) -join '.' }
    }
    # The ModuleVersionSpelling error: $Item needs $Version, but only $Other (another spelling) is present.
    $spellingError = {
        param($Item, [string]$Version, $Other, [string]$Key, [bool]$AboveMaximum = $false)
        if ($AboveMaximum) {
            # The other spelling would meet the minimum; only the maximum refuses it.
            $message = "Module '$($Item.Name)' is needed at '$Version' to '$($Item.MaximumVersion)' ($($Item.RequiredBy)), but the " +
                "'$($Item.Name)' found at '$($Other.ModuleBase)' has ModuleVersion '$($Other.Version)', which is above that " +
                "maximum, so it would not load. Supply '$($Item.Name)' with a version in that range, or pin the module that " +
                "requires it to a version that allows '$($Other.Version)'."
            return [System.Management.Automation.ErrorRecord]::new(
                [System.ArgumentException]::new($message, 'Module'),
                'ModuleVersionSpelling', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Key)
        }
        $needs = if ($Item.RequiredBy -eq 'requested') { "is pinned at RequiredVersion '$Version'" }
        elseif ($Item.Kind -eq 'Minimum') { "is needed at '$Version' or later ($($Item.RequiredBy))" }
        else { "is needed at RequiredVersion '$Version' ($($Item.RequiredBy))" }
        $fix = if ($Item.RequiredBy -eq 'requested') { "Pin RequiredVersion '$($Other.Version)' instead." }
        else { "Supply '$($Item.Name)' with ModuleVersion '$Version', the spelling the requirement uses, or pin the module that requires it to a version that asks for '$($Other.Version)'." }
        $message = "Module '$($Item.Name)' $needs, but the '$($Item.Name)' found at '$($Other.ModuleBase)' has " +
            "ModuleVersion '$($Other.Version)'. PowerShell matches versions exactly, so '$Version' and " +
            "'$($Other.Version)' are different versions and it would not load. $fix"
        [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new($message, 'Module'),
            'ModuleVersionSpelling', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Key)
    }
    # Advice for a dependency entry the walk cannot use. A -Module pin of the dependency cannot change
    # the parent's manifest, so the advice is about which parent version to take: when the parent was
    # reached as a dependency itself, that choice belongs to the module that requires it.
    $entryAdvice = {
        param($Item, [string]$Version, [string]$Dependency, [string]$Wants)
        # Choosing another version comes first: a published module's manifest is rarely yours to fix.
        $choose = if ($Wants -and $Item.RequiredBy -eq 'requested') {
            "Pin a version of '$($Item.Name)' whose manifest $Wants"
        }
        elseif ($Wants) {
            $requirer = ($Item.RequiredBy -replace '^required by ', '')
            "Choose another version of $requirer, the module that requires '$($Item.Name)', one that needs a version of " +
            "'$($Item.Name)' whose manifest $Wants"
        }
        elseif ($Item.RequiredBy -eq 'requested') {
            "Pin a version of '$($Item.Name)' whose manifest gives '$Dependency' a version (RequiredVersion or ModuleVersion)"
        }
        else {
            $requirer = ($Item.RequiredBy -replace '^required by ', '')
            "Choose another version of $requirer, the module that requires '$($Item.Name)', one whose dependencies give " +
            "'$Dependency' a version (RequiredVersion or ModuleVersion)"
        }
        "$choose, or fix that RequiredModules entry in '$($Item.Name)' $Version's manifest; this cmdlet never picks a version for you."
    }
    # Whether the module found under another spelling is within the item's MaximumVersion.
    $withinMaximumOf = {
        param($Item, $Other)
        if (-not $Item.MaximumVersion) { return $true }
        $ceiling = $null
        [version]::TryParse(($Item.MaximumVersion -replace '\*', [string][int]::MaxValue), [ref]$ceiling) -and [version]$Other.Version -le $ceiling
    }
    # A requirement that names a GUID is met only by the module with that GUID, as
    # Get-Module -FullyQualifiedName matches it; another module of the same name and version would be
    # staged and then fail to load for the module that requires it.
    $guidError = {
        param($Item, [string]$Version, $Other)
        $message = "Module '$($Item.Name)' $Version is required with GUID $($Item.Guid) ($($Item.RequiredBy)), but the " +
            "module '$($Item.Name)' $Version found has GUID $Other, so it is a different module. Install or download " +
            'the module with that GUID, or correct the GUID in the requirement.'
        [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new($message, 'Module'),
            'ModuleGuidMismatch', [System.Management.Automation.ErrorCategory]::InvalidArgument, "$($Item.Name) $Version")
    }
    $passLimit = 10
    # Per module: a choice that went down ($wentDown) and later came back up is swinging ($swinging),
    # and from then on it is never lowered, whatever the other modules are doing.
    $wentDown = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $swinging = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    for ($pass = 1; ; $pass++) {
        $visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        # Keys a minimum resolved to another (higher) spelling, with the module found: a later exact
        # requirement on the same key must still match that spelling, whatever the walk order.
        $metByOtherSpelling = @{}
        $resolved = [System.Collections.Generic.List[object]]::new()
        $missing = [System.Collections.Generic.List[object]]::new()
        $pendingReached = [System.Collections.Generic.List[string]]::new()
        $walkError = $null
        # Every requirement on each module name (Kind, Version, MaximumVersion, RequiredBy) and every
        # version the walk reached for it.
        $requirementsByName = [ordered]@{}
        $versionsByName = [ordered]@{}
        # The GUID of the module each key resolved to, for a requirement that names a GUID.
        $guidByKey = @{}

        # Breadth-first over the requested modules and their transitive dependencies. A requested
        # module is an exact pin.
        $queue = [System.Collections.Generic.Queue[object]]::new()
        foreach ($spec in $Module) {
            if (Test-LambdaPSBuiltInModule -Name $spec.Name) {
                if ($pass -eq 1) {
                    Write-Warning "Skipping '$($spec.Name)': it is one of PowerShell's own modules, which every PowerShell already has."
                }
                continue
            }
            $queue.Enqueue([PSCustomObject]@{
                    Name = $spec.Name; Version = [string]$spec.RequiredVersion; Kind = 'Exact'; MaximumVersion = $null
                    RequiredBy = 'requested'; Guid = $spec.Guid
                })
        }

        while ($queue.Count -gt 0 -and $null -eq $walkError) {
            $item = $queue.Dequeue()
            $version = if ($item.Kind -eq 'Minimum' -and $chosen.ContainsKey($item.Name)) { $chosen[$item.Name] } else { $item.Version }
            $key = "$($item.Name) $version"
            if (-not $requirementsByName.Contains($item.Name)) {
                $requirementsByName[$item.Name] = [System.Collections.Generic.List[object]]::new()
                $versionsByName[$item.Name] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            }
            $requirementsByName[$item.Name].Add($item)
            $null = $versionsByName[$item.Name].Add($version)
            if (-not $visited.Add($key)) {
                # The module this key resolved to must also be the one a later requirement names by GUID.
                if ($item.Guid -and $guidByKey.ContainsKey($key) -and $guidByKey[$key] -ne $item.Guid) {
                    $walkError = & $guidError $item $version $guidByKey[$key]
                    break
                }
                if ($item.Kind -eq 'Exact' -and $metByOtherSpelling.ContainsKey($key)) {
                    $walkError = & $spellingError $item $version $metByOtherSpelling[$key] $key
                    break
                }
                # A minimum on a key another minimum met with a higher spelling must also allow it.
                if ($item.Kind -eq 'Minimum' -and $metByOtherSpelling.ContainsKey($key) -and
                    -not (& $withinMaximumOf $item $metByOtherSpelling[$key])) {
                    $walkError = & $spellingError $item $version $metByOtherSpelling[$key] $key $true
                    break
                }
                continue
            }

            if ($pendingSet.Contains($key)) {
                $pendingReached.Add($key)
                continue
            }

            $skipInstalled = $SkipInstalled -and -not ($item.Name -like 'AWS.Tools.*' -and $item.Name -ne 'AWS.Tools.Installer')
            $found = Find-LambdaPSModule -Name $item.Name -Version $version -Guid $item.Guid -SearchPath $SearchPath -SkipInstalled:$skipInstalled
            if ($null -eq $found -and $item.Guid) {
                # The name and version are there, but as another module (another GUID).
                $other = Find-LambdaPSModule -Name $item.Name -Version $version -SearchPath $SearchPath -SkipInstalled:$skipInstalled
                if ($null -ne $other) {
                    $walkError = & $guidError $item $version $other.Guid
                    break
                }
            }
            if ($null -ne $found) {
                $guidByKey[$key] = $found.Guid
            }
            if ($null -eq $found) {
                # Present only under another spelling of the version (1.0 for 1.0.0, or the reverse)?
                $other = $null
                foreach ($spelling in @(& $otherSpellings $version)) {
                    $other = Find-LambdaPSModule -Name $item.Name -Version $spelling -SearchPath $SearchPath -SkipInstalled:$skipInstalled
                    if ($null -ne $other) { break }
                }
                # Any minimum, including one resolved to the version another minimum names, is met by a
                # higher spelling (PowerShell loads 1.0.0 for 1.0 or later), but only within its maximum.
                $higherForMinimum = $null -ne $other -and $item.Kind -eq 'Minimum' -and [version]$other.Version -gt [version]$version
                $withinMaximum = $null -eq $other -or (& $withinMaximumOf $item $other)
                if ($higherForMinimum -and $withinMaximum) {
                    $found = $other
                    $metByOtherSpelling[$key] = $other
                }
                elseif ($null -ne $other) {
                    $walkError = & $spellingError $item $version $other $key ($higherForMinimum -and -not $withinMaximum)
                    break
                }
            }
            if ($null -eq $found) {
                Write-Verbose "'$($item.Name)' $version ($($item.RequiredBy)) is not available locally."
                $missing.Add([PSCustomObject]@{ Name = $item.Name; Version = $version; Key = $key })
                continue
            }

            $found | Add-Member -NotePropertyName Key -NotePropertyValue $key
            $resolved.Add($found)
            $where = if ($found.Source -eq 'Local') { 'the PSModulePath' } else { 'the download folder' }
            Write-Verbose "Resolved '$($found.Name)' $version ($($item.RequiredBy)) from $where at '$($found.ModuleBase)'."

            foreach ($dependency in @($found.Dependencies)) {
                if ($null -eq $dependency) {
                    continue
                }
                if ([string]::IsNullOrWhiteSpace($dependency.Name)) {
                    $message = "Module '$($item.Name)' $version has a RequiredModules entry with no module name, in its " +
                        "manifest at '$($found.ModuleBase)'. PowerShell would not load the module either. " +
                        (& $entryAdvice $item $version '' 'names every module it requires')
                    $walkError = [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new($message, 'Module'),
                        'DependencyNameMissing', [System.Management.Automation.ErrorCategory]::InvalidArgument, $key)
                    break
                }
                if (Test-LambdaPSBuiltInModule -Name $dependency.Name) {
                    Write-Verbose "Skipping '$($dependency.Name)', required by $key`: it is one of PowerShell's own modules."
                    continue
                }
                if ([System.IO.Path]::IsPathRooted($dependency.Name) -or $dependency.Name.IndexOfAny([char[]]@('/', '\')) -ge 0 -or
                    $dependency.Name.Trim('.').Length -eq 0) {
                    $message = "Dependency '$($dependency.Name)' of module '$($item.Name)' $version is a path, not a " +
                        'module name. Dependencies are resolved by name and version only; a path would take the ' +
                        "module from wherever it points, and it would not load on Lambda. Change the module's " +
                        'RequiredModules entry to the module name.'
                    $walkError = [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new($message, 'Module'),
                        'DependencyNameIsPath', [System.Management.Automation.ErrorCategory]::InvalidArgument, $dependency.Name)
                    break
                }
                $parsedDependency = $null
                if (-not [string]::IsNullOrWhiteSpace($dependency.Version) -and -not [version]::TryParse($dependency.Version, [ref]$parsedDependency)) {
                    $message = "Dependency '$($dependency.Name)' of module '$($item.Name)' $version has version " +
                        "'$($dependency.Version)', which is not a version number. " + (& $entryAdvice $item $version $dependency.Name)
                    $walkError = [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new($message, 'Module'),
                        'DependencyVersionUnpinned', [System.Management.Automation.ErrorCategory]::InvalidArgument, $dependency.Name)
                    break
                }
                if ([string]::IsNullOrWhiteSpace($dependency.Version)) {
                    # A MaximumVersion alone is a ceiling, not a version this walk could choose.
                    $what = if ($dependency.MaximumVersion) { "only a MaximumVersion ('$($dependency.MaximumVersion)'), which is not a pin" } else { 'no version' }
                    $message = "Dependency '$($dependency.Name)' of module '$($item.Name)' $version has $what. " +
                        "Pinning '$($dependency.Name)' in -Module does not change that entry. " + (& $entryAdvice $item $version $dependency.Name)
                    $walkError = [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new($message, 'Module'),
                        'DependencyVersionUnpinned', [System.Management.Automation.ErrorCategory]::InvalidArgument, $dependency.Name)
                    break
                }
                $queue.Enqueue([PSCustomObject]@{
                        Name = $dependency.Name; Version = $dependency.Version; Kind = $dependency.Kind
                        MaximumVersion = $dependency.MaximumVersion; RequiredBy = "required by $key"; Guid = $dependency.Guid
                    })
            }
        }
        if ($null -ne $walkError) {
            break
        }

        # One version per module: a function loads a single version of a module, so staging two side
        # by side would leave which one wins to module resolution at run time.
        $rewalk = $false
        foreach ($name in $requirementsByName.Keys) {
            $requirements = $requirementsByName[$name]
            $target = Select-LambdaPSModuleVersion -Requirement $requirements
            if ($null -eq $target) {
                $detail = ($requirements | ForEach-Object {
                        $range = if ($_.Kind -eq 'Exact') { $_.Version }
                        elseif ($_.MaximumVersion) { "$($_.Version) to $($_.MaximumVersion)" }
                        else { "$($_.Version) or later" }
                        "$range ($($_.RequiredBy))"
                    } | Select-Object -Unique) -join ', '
                $message = "Module '$name' is needed at more than one version: $detail. A function loads a " +
                    "single version of a module: change the version requested in -Module, or choose another " +
                    "version of the module that requires '$name', so that one version satisfies them all."
                $walkError = [System.Management.Automation.ErrorRecord]::new(
                    [System.ArgumentException]::new($message, 'Module'),
                    'ModuleVersionConflict', [System.Management.Automation.ErrorCategory]::InvalidArgument, $name)
                break
            }
            # Keep an earlier, higher choice while a requirement reached in this pass still asks for it
            # (a module that raised it and dropped out releases it), and always once this module's
            # choice has gone down and come back up.
            $previous = $null; $proposed = $null
            $stillAsked = $false
            if ($chosen.ContainsKey($name) -and [version]::TryParse($chosen[$name], [ref]$previous)) {
                foreach ($requirement in $requirements) {
                    $asks = $null
                    if ([version]::TryParse([string]$requirement.Version, [ref]$asks) -and $asks -ge $previous) { $stillAsked = $true }
                }
            }
            if (($stillAsked -or $swinging.Contains($name)) -and $null -ne $previous -and
                [version]::TryParse($target, [ref]$proposed) -and $previous -gt $proposed) {
                $kept = Select-LambdaPSModuleVersion -Requirement (@($requirements) + [PSCustomObject]@{ Kind = 'Exact'; Version = $chosen[$name]; MaximumVersion = $null })
                if ($null -eq $kept) {
                    $message = "Module '$name' was resolved to $($chosen[$name]), which a later requirement does not allow, and a " +
                        'lower version would undo the requirement that raised it. Pin it with a RequiredVersion.'
                    $walkError = [System.Management.Automation.ErrorRecord]::new(
                        [System.ArgumentException]::new($message, 'Module'),
                        'ModuleVersionConflict', [System.Management.Automation.ErrorCategory]::InvalidArgument, $name)
                    break
                }
                $target = $kept
            }
            $walked = $versionsByName[$name]
            if ($walked.Count -ne 1 -or -not $walked.Contains($target)) {
                Write-Verbose "Using '$name' $target, the one version that meets every requirement on it ($(@($walked) -join ', ') reached so far)."
                $before = $null; $after = $null
                if ($chosen.ContainsKey($name) -and [version]::TryParse($chosen[$name], [ref]$before) -and [version]::TryParse($target, [ref]$after)) {
                    if ($after -lt $before) { $null = $wentDown.Add($name) }
                    elseif ($after -gt $before -and $wentDown.Contains($name) -and $swinging.Add($name)) {
                        Write-Verbose "'$name' is swinging between versions; keeping its higher version from here."
                    }
                }
                $chosen[$name] = $target
                $rewalk = $true
            }
        }
        if ($null -ne $walkError -or -not $rewalk) {
            break
        }
        # The pass limit stays as a backstop.
        if ($pass -ge $passLimit) {
            $walkError = [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new(
                    "The module set's dependency versions did not settle after $passLimit passes. Pin each module with a RequiredVersion."),
                'ModuleClosureNotSettled', [System.Management.Automation.ErrorCategory]::InvalidResult, $Module)
            break
        }
    }

    if ($null -eq $walkError -and $RequireAll -and $missing.Count -gt 0) {
        $walkError = New-LambdaPSModuleNotFoundError -Missing $missing.ToArray()
    }

    # A module that only minimums ask for may be needed higher once a missing module's manifest is on
    # disk, so a downloader fetches it last.
    foreach ($entry in $missing) {
        $minimumOnly = $true
        foreach ($requirement in $requirementsByName[$entry.Name]) {
            if ($requirement.Kind -eq 'Exact') { $minimumOnly = $false }
        }
        $entry | Add-Member -NotePropertyName MinimumOnly -NotePropertyValue $minimumOnly
    }

    [PSCustomObject]@{
        Resolved = $resolved.ToArray()
        Missing  = $missing.ToArray()
        Pending  = $pendingReached.ToArray()
        Error    = $walkError
    }
}
