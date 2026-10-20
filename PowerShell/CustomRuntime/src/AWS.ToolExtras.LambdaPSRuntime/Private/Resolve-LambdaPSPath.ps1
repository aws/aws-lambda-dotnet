function Resolve-LambdaPSPath {
    <#
    .SYNOPSIS
        Returns the absolute file-system path for a path relative to the current PowerShell location.
    .DESCRIPTION
        [System.IO.Path]::GetFullPath resolves a relative path against the process working directory,
        which Set-Location does not change. Test-Path, Get-ChildItem and Remove-Item resolve it against
        the PowerShell location. Every path the module hands to a .NET API goes through this helper so
        both kinds of call act on the same directory. The path does not need to exist. Reads nothing
        and writes nothing. A path on another provider (Env:, Variable:, Function:) is a terminating
        PathNotFileSystem error. A path whose drive does not exist, such as a mistyped 'TestDriv:x', is
        a terminating DriveNotFound error. On Linux and macOS a file name may contain a colon: write
        './a:b' for a file named 'a:b', since 'a:b' names the drive 'a'.
    .PARAMETER Path
        The path to resolve. Absolute paths are returned normalized.
    .OUTPUTS
        System.String
    .EXAMPLE
        Set-Location ./work; Resolve-LambdaPSPath -Path ./out.zip

        Returns '<current location>/work/out.zip', whatever directory pwsh was started in.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
    # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
    # caller's -ErrorVariable naming this internal helper.
    $provider = $null
    $drive = $null
    try {
        $providerPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive)
    }
    catch {
        $driveNotFound = $_.Exception -is [System.Management.Automation.DriveNotFoundException] -or
            $_.Exception.InnerException -is [System.Management.Automation.DriveNotFoundException]
        if (-not $driveNotFound) { throw }
        $message = "The drive in '$Path' does not exist."
        if (-not $IsWindows) { $message += " For a file name that contains a colon, write './$Path'." }
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.Management.Automation.DriveNotFoundException]::new($message, $_.Exception),
            'DriveNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $Path)
    }
    if ($provider.Name -ne 'FileSystem') {
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new("'$Path' is a path on the $($provider.Name) provider. Pass a file-system path."),
            'PathNotFileSystem', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path)
    }
    return [System.IO.Path]::GetFullPath($providerPath)
}
