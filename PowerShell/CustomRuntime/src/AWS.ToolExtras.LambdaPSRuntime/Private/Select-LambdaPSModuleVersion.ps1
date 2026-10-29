function Select-LambdaPSModuleVersion {
    <#
    .SYNOPSIS
        Picks the one version of a module that meets every requirement on it, or nothing.
    .DESCRIPTION
        Used by Get-LambdaPSModuleClosure. Each requirement has Kind 'Exact' (a request's pin or a
        RequiredVersion dependency) or 'Minimum' (a ModuleVersion dependency, optionally capped by
        MaximumVersion).

        - More than one distinct exact version: no answer.
        - One exact version: that version, provided every minimum and maximum allows it.
        - Only minimums: the highest minimum, provided every maximum allows it.

        Returns the version string, or $null when no single version meets them all. It never chooses
        a version that no requirement names. A MaximumVersion may end in '*' ('1.*').
    .PARAMETER Requirement
        The requirements on one module (objects with Kind, Version and MaximumVersion).
    .OUTPUTS
        System.String
    .EXAMPLE
        Select-LambdaPSModuleVersion -Requirement @(
            [PSCustomObject]@{ Kind = 'Exact'; Version = '1.2.0'; MaximumVersion = $null }
            [PSCustomObject]@{ Kind = 'Minimum'; Version = '1.1.0'; MaximumVersion = $null })

        Returns '1.2.0'.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [object[]]$Requirement
    )

    # Plain loops rather than 'ForEach-Object <Member>': that form honours -WhatIf, which is inherited
    # here, and would skip reading the property.
    $exact = [System.Collections.Generic.List[string]]::new()
    $minimums = [System.Collections.Generic.List[object]]::new()
    foreach ($item in $Requirement) {
        if ($item.Kind -eq 'Exact') {
            if (-not $exact.Contains([string]$item.Version)) { $exact.Add([string]$item.Version) }
        }
        else {
            $minimums.Add($item)
        }
    }
    if ($exact.Count -gt 1) {
        return $null
    }

    if ($exact.Count -eq 1) {
        $candidate = [string]$exact[0]
    }
    else {
        $candidate = $minimums |
            Sort-Object { $parsed = $null; if ([version]::TryParse($_.Version, [ref]$parsed)) { $parsed } else { [version]'0.0' } } -Descending |
            Select-Object -First 1 -ExpandProperty Version
    }

    $candidateVersion = $null
    if (-not [version]::TryParse($candidate, [ref]$candidateVersion)) {
        # Not a numeric version: only an exact, identical requirement can be met.
        if ($minimums.Count -gt 0) { return $null }
        return $candidate
    }

    foreach ($minimum in $minimums) {
        $floor = $null
        if (-not [version]::TryParse($minimum.Version, [ref]$floor) -or $candidateVersion -lt $floor) {
            return $null
        }
        if ($minimum.MaximumVersion) {
            $ceiling = $null
            if (-not [version]::TryParse(($minimum.MaximumVersion -replace '\*', [string][int]::MaxValue), [ref]$ceiling) -or
                $candidateVersion -gt $ceiling) {
                return $null
            }
        }
    }

    return $candidate
}
