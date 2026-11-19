function Invoke-LambdaPSOciContextPrecompile {
    <#
    .SYNOPSIS
        Precompiles a no-Docker image's PowerShellLambdaContext.dll, returning $false when the runtime must compile it at cold start.
    .DESCRIPTION
        On Linux, compiles with the bundled pwsh when the host has the image's architecture, otherwise with the
        same pwsh release for the host's architecture (Get-LambdaPSHostPwsh). Elsewhere, or when that pwsh can't be
        fetched or started, it compiles with the running pwsh if it is of the same line (Get-LambdaPSSameLinePwsh).
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
    $pwsh = if ($IsLinux -and $hostArchitecture -eq $Architecture) { $BundledPwshPath }
    elseif ($IsLinux) { Get-LambdaPSHostPwsh -PwshVersion $PwshVersion -Architecture $hostArchitecture -Destination ([System.IO.Path]::Combine($StagePath, 'host-powershell')) }
    if ($pwsh -and (Invoke-LambdaPSContextPrecompile -PwshPath $pwsh -SourcePath $SourcePath -OutputPath $OutputPath)) { return $true }
    $sameLine = Get-LambdaPSSameLinePwsh -PwshVersion $PwshVersion -Destination ([System.IO.Path]::Combine($StagePath, 'same-line-probe'))
    [bool]($sameLine -and (Invoke-LambdaPSContextPrecompile -PwshPath $sameLine -SourcePath $SourcePath -OutputPath $OutputPath))
}
