function Get-LambdaPSTarCommand {
    <#
    .SYNOPSIS
        Returns the path of the tar program to run: Windows' own tar.exe on Windows, and the tar on
        PATH elsewhere.
    .DESCRIPTION
        On Windows, 'tar' on PATH is often Git for Windows' GNU tar, which reads a drive-letter path
        such as C:\temp\pwsh.tar.gz as host:path and fails with "Cannot execute remote shell". So on
        Windows this returns $env:SystemRoot\System32\tar.exe (bsdtar, shipped with Windows 10 1803
        and later) when that file exists. Otherwise it returns the path of the first tar program on
        PATH, so a session function or alias named tar does not run instead. When there is none it
        returns 'tar', which is not a program: callers check the result with
        Get-Command -CommandType Application before they run it.
    .PARAMETER OnWindows
        Whether the host is Windows. Defaults to $IsWindows; tests pass it to cover the other platform.
    .PARAMETER SystemRoot
        The Windows folder. Defaults to $env:SystemRoot.
    .OUTPUTS
        System.String
    .EXAMPLE
        & (Get-LambdaPSTarCommand) -xzf $archive -C $destination

        Extracts $archive with C:\Windows\System32\tar.exe on Windows, and with, for example,
        /usr/bin/tar elsewhere.
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
    $application = Get-Command -Name 'tar' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($application) { return $application.Source }
    return 'tar'
}
