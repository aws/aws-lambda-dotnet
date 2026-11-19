function Invoke-LambdaPSOciContextPrecompile {
    <#
    .SYNOPSIS
        Precompiles a no-Docker image's PowerShellLambdaContext.dll, returning $false when the runtime must compile it at cold start.
    .DESCRIPTION
        Compiles with the bundled pwsh on a Linux host of the image's architecture, else with the running pwsh if it
        is of the same line (Get-LambdaPSSameLinePwsh), else on Linux with the same pwsh release for the host's
        architecture (Get-LambdaPSHostPwsh), which is a ~70 MB download.
    .PARAMETER BundledPwshPath
        The image's unpacked pwsh binary.
    .PARAMETER PwshVersion
        The pwsh release (x.y.z) the image bundles.
    .PARAMETER Architecture
        The image's architecture: x86_64 or arm64.
    .PARAMETER SourcePath
        The PowerShellLambdaContext.cs to compile.
    .PARAMETER OutputPath
        The DLL to write.
    .PARAMETER StagePath
        The build's staging folder, where a host build of pwsh is unpacked and trial compiles run.
    .OUTPUTS
        System.Boolean
    .EXAMPLE
        Invoke-LambdaPSOciContextPrecompile -BundledPwshPath $pwshRoot/pwsh -PwshVersion 7.6.9 -Architecture arm64 -SourcePath $cs -OutputPath $dll -StagePath $stage

        On an x86_64 Linux host, compiles the DLL with the x86_64 build of pwsh 7.6.9.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$BundledPwshPath,

        [Parameter(Mandatory)]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [ValidateSet('x86_64', 'arm64')]
        [string]$Architecture,

        [Parameter(Mandatory)]
        [string]$SourcePath,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter(Mandatory)]
        [string]$StagePath
    )

    $hostArchitecture = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x86_64' }
    $compile = @{ SourcePath = $SourcePath; OutputPath = $OutputPath }
    if ($IsLinux -and $hostArchitecture -eq $Architecture -and (Invoke-LambdaPSContextPrecompile @compile -PwshPath $BundledPwshPath)) { return $true }
    $sameLine = Get-LambdaPSSameLinePwsh -PwshVersion $PwshVersion -Destination ([System.IO.Path]::Combine($StagePath, 'same-line-probe'))
    if ($sameLine -and (Invoke-LambdaPSContextPrecompile @compile -PwshPath $sameLine)) { return $true }
    if (-not $IsLinux -or $hostArchitecture -eq $Architecture) { return $false }
    $hostPwsh = Get-LambdaPSHostPwsh -PwshVersion $PwshVersion -Architecture $hostArchitecture -Destination ([System.IO.Path]::Combine($StagePath, 'host-powershell'))
    [bool]($hostPwsh -and (Invoke-LambdaPSContextPrecompile @compile -PwshPath $hostPwsh))
}
