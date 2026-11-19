function Get-LambdaPSProjectMarker {
    <#
    .SYNOPSIS
        Reads a scaffolded project's lambdaps-runtime.json marker.
    .DESCRIPTION
        The scaffolders write lambdaps-runtime.json at a project's root, recording its deployment
        target, PowerShell version, pwsh version and module version. This is the one reader of that file, shared by
        Build-LambdaPSRuntimePackage, Compress-LambdaPSFunctionPackage, Test-LambdaPSRuntimeProject,
        New-LambdaPSFunctionDeployment and New-LambdaPSRuntimeDeployment.

        Returns nothing when -Path has no marker. Otherwise returns an object with Path (the marker
        file), Target, PowerShellVersion and PwshVersion (any may be $null when the marker omits it). A marker
        that is not valid JSON, whose Target is not one of CMR, Layer, SelfContainedZip or Container,
        or whose PowerShellVersion is not major.minor or PwshVersion not x.y.z, is a terminating
        InvalidProjectMarker error that names the file, rather than a
        parse error or a parameter-validation error further on. Target is matched without regard to
        case and returned in that canonical spelling, so 'layer' is returned as 'Layer'.
    .PARAMETER Path
        The project directory.
    .OUTPUTS
        System.Management.Automation.PSCustomObject
    .EXAMPLE
        (Get-LambdaPSProjectMarker -Path ./rt).Target

        Returns 'CMR' for a project scaffolded with New-LambdaPSRuntimeProject.
    #>
    [CmdletBinding()]
    [OutputType([System.Management.Automation.PSCustomObject])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    $markerPath = [System.IO.Path]::Combine((Resolve-LambdaPSPath -Path $Path), 'lambdaps-runtime.json')
    # Ignore: a folder that cannot be read writes no stray error here; its caller reports it.
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf -ErrorAction Ignore)) {
        return
    }

    try {
        $marker = [System.IO.File]::ReadAllText($markerPath) | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.InvalidDataException]::new(
                "The project marker '$markerPath' is not valid JSON: $($_.Exception.Message)", $_.Exception),
            'InvalidProjectMarker', [System.Management.Automation.ErrorCategory]::InvalidData, $markerPath)
    }

    $target = if ($null -ne $marker -and $marker.PSObject.Properties.Name -contains 'Target') { [string]$marker.Target } else { $null }
    if ([string]::IsNullOrWhiteSpace($target)) {
        $target = $null
    }
    elseif (@('CMR', 'Layer', 'SelfContainedZip', 'Container') -notcontains $target) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.InvalidDataException]::new(
                ("The project marker '$markerPath' names target '$target', which is not one of CMR, Layer, " +
                 'SelfContainedZip or Container.')),
            'InvalidProjectMarker', [System.Management.Automation.ErrorCategory]::InvalidData, $markerPath)
    }
    else {
        # The check above ignores case. Callers print and return the target, so it is the canonical spelling.
        $target = @('CMR', 'Layer', 'SelfContainedZip', 'Container') | Where-Object { $_ -eq $target }
    }

    $powerShellVersion = if ($null -ne $marker -and $marker.PSObject.Properties.Name -contains 'PowerShellVersion') { [string]$marker.PowerShellVersion } else { $null }

    $pwshVersion = if ($null -ne $marker -and $marker.PSObject.Properties.Name -contains 'PwshVersion') { [string]$marker.PwshVersion } else { $null }

    # Checked here, so an edited marker is reported against its own file rather than as a parameter
    # error for a parameter the caller never passed.
    $versionFields = [ordered]@{
        PowerShellVersion = @{ Value = $powerShellVersion; Pattern = '\A[0-9]+\.[0-9]+\z'; Form = 'a major.minor version (for example 7.4)' }
        PwshVersion       = @{ Value = $pwshVersion; Pattern = '\A[0-9]+\.[0-9]+\.[0-9]+\z'; Form = 'a pwsh release in x.y.z form (for example 7.4.20)' }
    }
    foreach ($field in $versionFields.GetEnumerator()) {
        if (-not [string]::IsNullOrWhiteSpace($field.Value.Value) -and $field.Value.Value -notmatch $field.Value.Pattern) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.InvalidDataException]::new(
                    "The project marker '$markerPath' has $($field.Key) '$($field.Value.Value)', which is not $($field.Value.Form). Fix it, or scaffold the project again."),
                'InvalidProjectMarker', [System.Management.Automation.ErrorCategory]::InvalidData, $markerPath)
        }
    }
    if ($powerShellVersion -and $pwshVersion -and -not $pwshVersion.StartsWith("$powerShellVersion.", [System.StringComparison]::Ordinal)) {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.InvalidDataException]::new(
                ("The project marker '$markerPath' has PwshVersion '$pwshVersion', which is not a " +
                 "PowerShell $powerShellVersion release. Fix it, or scaffold the project again.")),
            'InvalidProjectMarker', [System.Management.Automation.ErrorCategory]::InvalidData, $markerPath)
    }

    [PSCustomObject]@{
        Path              = $markerPath
        Target            = $target
        PowerShellVersion = if ([string]::IsNullOrWhiteSpace($powerShellVersion)) { $null } else { $powerShellVersion }
        PwshVersion       = if ([string]::IsNullOrWhiteSpace($pwshVersion)) { $null } else { $pwshVersion }
    }
}
