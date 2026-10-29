function Get-LambdaPSFolderHash {
    <#
    .SYNOPSIS
        Returns one SHA256 for the names and contents of every file in a folder.
    .DESCRIPTION
        Hashes each file under -Path (hidden files included) with SHA256, and makes one line per file:
        its path relative to -Path, with '/' separators, a space, and its hash in upper-case hex. The
        lines are sorted ordinally and joined with '\n', and the result is the SHA256 of that UTF-8
        text, in upper-case hex. A file added, removed, renamed or changed gives a different hash, on
        every platform. Files named in -Exclude (relative paths, compared case-insensitively) are left
        out. A folder that does not exist is a terminating error. Reads only.
    .PARAMETER Path
        The folder to hash.
    .PARAMETER Exclude
        Relative paths of files to leave out, for example the PSGetModuleInfo.xml that PSResourceGet
        writes beside a saved module.
    .OUTPUTS
        System.String
    .EXAMPLE
        Get-LambdaPSFolderHash -Path /tmp/i/AWS.Tools.Installer/2.0.6 -Exclude 'PSGetModuleInfo.xml'

        Returns the hash Config/installer.psd1 pins for AWS.Tools.Installer 2.0.6.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [string[]]$Exclude = @()
    )

    $root = [System.IO.Path]::GetFullPath($Path)
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -File -Force -ErrorAction Stop)) {
        $relative = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        if (@($Exclude | Where-Object { $_.Replace('\', '/') -eq $relative }).Count -gt 0) { continue }
        $lines.Add("$relative $((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)")
    }
    $lines.Sort([System.StringComparer]::Ordinal)
    [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($lines -join "`n")))
}
