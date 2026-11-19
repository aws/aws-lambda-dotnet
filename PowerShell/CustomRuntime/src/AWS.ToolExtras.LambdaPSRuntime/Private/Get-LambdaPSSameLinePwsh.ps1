function Get-LambdaPSSameLinePwsh {
    <#
    .SYNOPSIS
        Returns the running PowerShell's pwsh when it is of the pwsh release's line (x.y) and can compile here, else $null.
    .DESCRIPTION
        PowerShellLambdaContext.dll references only System.Runtime, so a pwsh of the same line (same .NET)
        compiles a DLL that every release of the line loads, on any operating system. A trial compile, as in
        Get-LambdaPSHostPwsh, makes a later ContextCompileFailed the source's fault, not the host's.
    .PARAMETER PwshVersion
        The pwsh release (x.y.z) the package or image bundles.
    .PARAMETER Destination
        The folder for the trial compile.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSSameLinePwsh -PwshVersion 7.6.9 -Destination $stage/same-line-probe

        In PowerShell 7.6.6 on macOS, returns $PSHOME/pwsh.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$PwshVersion,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    $running = $PSVersionTable.PSVersion
    if ("$($running.Major).$($running.Minor)" -ne ([version]$PwshVersion).ToString(2)) { return $null }
    $pwsh = [System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }))
    if (-not (Test-Path -LiteralPath $pwsh -PathType Leaf)) { return $null }
    try {
        $null = [System.IO.Directory]::CreateDirectory($Destination)
        $probe = [System.IO.Path]::Combine($Destination, 'probe.cs')
        [System.IO.File]::WriteAllText($probe, 'public static class LambdaPSSameLinePwshProbe { }')
        if (Invoke-LambdaPSContextPrecompile -PwshPath $pwsh -SourcePath $probe -OutputPath "$probe.dll") { return $pwsh }
        Write-Verbose "PowerShellLambdaContext.dll is not precompiled with the running pwsh: it could not start."
    }
    catch {
        Write-Verbose "PowerShellLambdaContext.dll is not precompiled with the running pwsh: it could not compile here. $($_.Exception.Message)"
    }
    $null
}
