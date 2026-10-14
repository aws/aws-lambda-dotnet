function Find-LambdaPSModule {
    <#
    .SYNOPSIS
        Resolves one exact module version from the local machine or from extra module folders.
    .DESCRIPTION
        The single lookup behind module staging and module download. It first asks
        Get-Module -ListAvailable -FullyQualifiedName (the PSModulePath), then looks for
        <SearchPath>/<Name>/<Version>/<Name>.psd1 under each -SearchPath folder -- the layout
        Save-PSResource and Install-AWSToolsModule -Path both write -- matching names
        case-insensitively. A module found under a search folder is read with
        Get-Module -ListAvailable -Name <manifest>, which returns the same PSModuleInfo shape
        (including RequiredModules) without requiring its dependencies to be present.

        The version is matched exactly, as PowerShell matches it: 1.0 and 1.0.0 are different versions,
        and a module whose manifest says one does not load for a pin, RequiredModules entry or
        minimum that needs the other. Under a search folder, both the <Version> folder name and the
        manifest's ModuleVersion must be the version asked for.

        With -Guid, only a module whose manifest has that GUID is found, as
        Get-Module -FullyQualifiedName matches a GUID: a module of the same name and version with
        another GUID is a different module.

        Returns an object with Name (the module's real casing, whatever the caller typed), Version (its
        own version, as its manifest states it), Guid, ModuleBase, Dependencies and Source ('Local' or
        'SearchPath'), or nothing when the module is in neither place. Makes no network call.

        Dependencies lists each RequiredModules entry as Name, Version, Kind and MaximumVersion. Kind
        is 'Exact' for a RequiredVersion entry and 'Minimum' for a ModuleVersion entry, which
        PSModuleInfo.RequiredModules does not record (both appear only as Version), so it is read from
        the module's manifest. An entry with no version has an empty Version.
    .PARAMETER Name
        The module name.
    .PARAMETER Version
        The exact module version.
    .PARAMETER Guid
        The module's GUID, when the requirement names one.
    .PARAMETER SearchPath
        Extra folders to search after the PSModulePath, laid out as <Name>/<Version>/.
    .PARAMETER SkipInstalled
        Do not look in the PSModulePath; search only -SearchPath.
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        Find-LambdaPSModule -Name 'AWS.Tools.S3' -Version '5.0.274' -SearchPath /tmp/downloads

        Returns the locally installed AWS.Tools.S3 5.0.274, or the copy under /tmp/downloads.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Name,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Version,

        [Parameter()]
        [AllowNull()]
        [Nullable[guid]]$Guid,

        [Parameter()]
        [AllowEmptyCollection()]
        [string[]]$SearchPath = @(),

        [Parameter()]
        [switch]$SkipInstalled
    )

    # Exactly the version asked for, as PowerShell compares it ([version] '1.0' is not '1.0.0').
    $asked = $null
    $isAsked = {
        param($Candidate)
        $parsedCandidate = $null
        if ($null -ne $asked -and [version]::TryParse([string]$Candidate, [ref]$parsedCandidate)) { return $parsedCandidate -eq $asked }
        [string]$Candidate -eq $Version
    }
    $null = [version]::TryParse($Version, [ref]$asked)

    $found = $null
    $foundVersion = $null
    if (-not $SkipInstalled) {
        $specification = @{ ModuleName = $Name; RequiredVersion = $Version }
        if ($Guid) { $specification.Guid = $Guid }
        $found = @(Get-Module -ListAvailable -FullyQualifiedName $specification) | Select-Object -First 1
    }
    $source = 'Local'

    if ($null -eq $found) {
        $source = 'SearchPath'
        foreach ($root in @($SearchPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            # Match the folder names case-insensitively: the download tools write the module's real
            # casing (PSFramework), which need not be what the caller typed (psframework), and the
            # file system is case-sensitive on Linux.
            if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
            $nameDir = @(Get-ChildItem -LiteralPath $root -Directory | Where-Object { $_.Name -eq $Name }) | Select-Object -First 1
            if ($null -eq $nameDir) { continue }
            $versionDir = @(Get-ChildItem -LiteralPath $nameDir.FullName -Directory | Where-Object { & $isAsked $_.Name }) | Select-Object -First 1
            if ($null -eq $versionDir) { continue }
            $manifest = @(Get-ChildItem -LiteralPath $versionDir.FullName -File -Filter '*.psd1' |
                    Where-Object { $_.Name -eq "$($nameDir.Name).psd1" }) | Select-Object -First 1
            if ($null -ne $manifest) {
                # The folder name alone does not decide it: PowerShell loads the version the manifest
                # states. Get-Module reports 0.0 for a manifest it cannot fully read (an invalid
                # RequiredModules entry, say), so the manifest's own ModuleVersion is read then.
                $candidate = @(Get-Module -ListAvailable -Name $manifest.FullName) | Select-Object -First 1
                if ($null -eq $candidate) { continue }
                if ($Guid -and $candidate.Guid -ne $Guid) { continue }
                $stated = $candidate.Version
                if ($stated -eq [version]'0.0') {
                    try { $stated = (Import-PowerShellDataFile -LiteralPath $manifest.FullName -ErrorAction Stop).ModuleVersion } catch { $stated = $null }
                }
                if (& $isAsked $stated) {
                    $found = $candidate
                    $foundVersion = [string]$stated
                    break
                }
            }
        }
    }

    if ($null -eq $found) {
        return
    }

    # Report the module's real name, not the caller's casing: it names the staged folder, and on Linux
    # (Lambda) a <name>/<version>/<Name>.psd1 casing mismatch makes the module unimportable by name.
    [PSCustomObject]@{
        Name         = $found.Name
        Version      = if ($foundVersion) { $foundVersion } elseif ($found.Version) { $found.Version.ToString() } else { $Version }
        Guid         = $found.Guid
        ModuleBase   = $found.ModuleBase
        Dependencies = @(Get-LambdaPSModuleDependency -Module $found)
        Source       = $source
    }
}
