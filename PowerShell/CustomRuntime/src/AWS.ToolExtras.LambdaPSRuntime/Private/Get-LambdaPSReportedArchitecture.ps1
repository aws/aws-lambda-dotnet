function Get-LambdaPSReportedArchitecture {
    <#
    .SYNOPSIS
        Returns the architectures in a runtime version's or alias's Compatibility block.
    .DESCRIPTION
        Get-LMCRuntimeVersion and Get-LMCRuntimeAlias both report a Compatibility block whose
        Architectures Lambda reads from the image manifest. This flattens it to a string array
        ('x86_64' and/or 'arm64'), dropping blank entries. The array is empty when the block or its
        Architectures are missing.
    .PARAMETER Compatibility
        The Compatibility property of a runtime version or alias response. May be $null.
    .OUTPUTS
        System.String[]
    .EXAMPLE
        Get-LambdaPSReportedArchitecture -Compatibility $runtimeVersion.Compatibility

        Returns @('arm64') for a version built from an arm64 image.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    # PSUseOutputTypeCorrectly: the return is comma-wrapped so an empty or one-item result still
    # reaches the caller as one string[], which is what OutputType declares.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single string[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter()]
        [AllowNull()]
        [object]$Compatibility
    )

    $architectures = @()
    if ($null -ne $Compatibility -and $null -ne $Compatibility.Architectures) {
        $architectures = @($Compatibility.Architectures | ForEach-Object { [string]$_ } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    return , ([string[]]$architectures)
}
