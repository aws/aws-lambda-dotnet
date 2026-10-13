function Get-LambdaPSModuleDependency {
    <#
    .SYNOPSIS
        Lists a module's RequiredModules, saying whether each version is exact or a minimum.
    .DESCRIPTION
        PSModuleInfo.RequiredModules reports a RequiredVersion entry and a ModuleVersion entry the
        same way (both only as Version), but they mean different things: RequiredVersion is that
        version exactly, ModuleVersion is that version or later. The dependency walk needs to know
        which, so a pinned 1.2.0 can satisfy a dependency's ModuleVersion = '1.1.0'. This reads the
        RequiredModules entries from the module's .psd1 manifest.

        Each entry is returned as Name, Version, Kind ('Exact' or 'Minimum') and MaximumVersion (or
        $null). An entry with no version (a bare name, a hashtable with only ModuleName, or one with
        only a MaximumVersion) has an empty Version and Kind 'Minimum'; the walk rejects it. When the manifest cannot be read, the
        PSModuleInfo entries are returned as 'Exact', which is the strictest reading.
    .PARAMETER Module
        The PSModuleInfo of the module (from Get-Module -ListAvailable).
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        Get-LambdaPSModuleDependency -Module (Get-Module -ListAvailable AWS.Tools.S3 | Select-Object -First 1)

        Lists AWS.Tools.S3's dependency on AWS.Tools.Common as an Exact entry.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object]$Module
    )

    $entries = $null
    $manifestPath = [string]$Module.Path
    if ($manifestPath -and $manifestPath.EndsWith('.psd1', [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        try {
            $entries = @((Import-PowerShellDataFile -LiteralPath $manifestPath -ErrorAction Stop).RequiredModules)
        }
        catch {
            Write-Verbose "Could not read RequiredModules from '$manifestPath'; treating every dependency version as exact. $($_.Exception.Message)"
            $entries = $null
        }
    }

    if ($null -eq $entries) {
        foreach ($required in @($Module.RequiredModules)) {
            if ($null -eq $required) { continue }
            [PSCustomObject]@{ Name = $required.Name; Version = [string]$required.Version; Kind = 'Exact'; MaximumVersion = $null }
        }
        return
    }

    foreach ($entry in $entries) {
        if ($null -eq $entry) { continue }
        if ($entry -is [string]) {
            [PSCustomObject]@{ Name = $entry; Version = ''; Kind = 'Minimum'; MaximumVersion = $null }
            continue
        }
        $requiredVersion = [string]$entry['RequiredVersion']
        if (-not [string]::IsNullOrWhiteSpace($requiredVersion)) {
            [PSCustomObject]@{ Name = [string]$entry['ModuleName']; Version = $requiredVersion; Kind = 'Exact'; MaximumVersion = $null }
            continue
        }
        $maximumVersion = [string]$entry['MaximumVersion']
        [PSCustomObject]@{
            Name           = [string]$entry['ModuleName']
            Version        = [string]$entry['ModuleVersion']
            Kind           = 'Minimum'
            MaximumVersion = if ([string]::IsNullOrWhiteSpace($maximumVersion)) { $null } else { $maximumVersion }
        }
    }
}
