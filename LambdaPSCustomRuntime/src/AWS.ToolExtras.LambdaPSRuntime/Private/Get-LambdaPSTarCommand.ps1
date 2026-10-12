function Get-LambdaPSTarCommand {
    <#
    .SYNOPSIS
        Returns the tar command to run: Windows' own tar.exe on Windows, and 'tar' elsewhere.
    .DESCRIPTION
        On Windows, 'tar' on PATH is often Git for Windows' GNU tar, which reads a drive-letter path
        such as C:\temp\pwsh.tar.gz as host:path and fails with "Cannot execute remote shell". So on
        Windows this returns $env:SystemRoot\System32\tar.exe (bsdtar, shipped with Windows 10 1803
        and later) when that file exists, and 'tar' otherwise.
    .PARAMETER OnWindows
        Whether the host is Windows. Defaults to $IsWindows; tests pass it to cover the other platform.
    .PARAMETER SystemRoot
        The Windows folder. Defaults to $env:SystemRoot.
    .OUTPUTS
        System.String
    .EXAMPLE
        & (Get-LambdaPSTarCommand) -xzf $archive -C $destination

        Extracts $archive with C:\Windows\System32\tar.exe on Windows, and with tar elsewhere.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [bool]$OnWindows = $IsWindows,

        [Parameter()]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$SystemRoot = $env:SystemRoot
    )

    if ($OnWindows -and -not [string]::IsNullOrWhiteSpace($SystemRoot)) {
        $systemTar = [System.IO.Path]::Combine($SystemRoot, 'System32', 'tar.exe')
        if (Test-Path -LiteralPath $systemTar -PathType Leaf) { return $systemTar }
    }
    return 'tar'
}
