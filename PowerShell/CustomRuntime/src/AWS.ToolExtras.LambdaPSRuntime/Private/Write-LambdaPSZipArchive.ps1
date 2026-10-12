function Write-LambdaPSZipArchive {
    <#
    .SYNOPSIS
        Zips a staged directory tree into a Lambda deployment .zip.
    .DESCRIPTION
        The zip writer shared by Compress-LambdaPSFunctionPackage and Build-LambdaPSRuntimePackage.

          - Entries are the files under -Path, named by forward-slash relative path, written in
            ordinal order of those names, each with the same timestamp (1980-01-01). So the same files
            give a byte-identical archive whichever OS built it, whatever order the file system lists
            them in and whatever their modification times.
          - Each entry carries a Unix mode in its external attributes: 0755 for the -ExecutableEntry
            names (Lambda runs 'bootstrap' and the bundled 'powershell/pwsh' directly) and, on a Unix
            host, for any file with an execute bit set; 0644 for every other file.
          - Every entry's "version made by" names Unix as its host, whichever OS built the zip.
            Extractors such as Info-ZIP apply the Unix mode only for a Unix host, and .NET records
            the OS it runs on (Windows, on a Windows build host).
          - Top-level files named in -ExcludeTopLevel are left out (case-insensitive); nested files of
            the same name are kept.
          - The archive is written to a temporary file beside -DestinationPath and moved into place
            only when it is complete, so an existing .zip is replaced only by a complete one and a
            failure never leaves a partial .zip behind.

        The caller has already confirmed the write; this helper does not ask again.
    .PARAMETER Path
        The staged directory to zip.
    .PARAMETER DestinationPath
        The .zip to write. Its parent directory is created if missing; an existing file is replaced.
    .PARAMETER ExcludeTopLevel
        Top-level file names to leave out of the archive.
    .PARAMETER ExecutableEntry
        Entry names (forward-slash relative paths) to mark executable.
    .EXAMPLE
        Write-LambdaPSZipArchive -Path $stage -DestinationPath ./layer.zip -ExecutableEntry 'bootstrap', 'powershell/pwsh'

        Zips $stage into ./layer.zip with bootstrap and pwsh executable.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$DestinationPath,

        [Parameter()]
        [AllowNull()]
        [AllowEmptyCollection()]
        [string[]]$ExcludeTopLevel = @(),

        [Parameter()]
        [AllowNull()]
        [AllowEmptyCollection()]
        [string[]]$ExecutableEntry = @()
    )

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $destination = Resolve-LambdaPSPath -Path $DestinationPath
    $parent = [System.IO.Path]::GetDirectoryName($destination)
    if (-not [string]::IsNullOrEmpty($parent) -and -not (Test-Path -LiteralPath $parent)) {
        $null = New-Item -ItemType Directory -Path $parent -Force -Confirm:$false -WhatIf:$false
    }

    $excluded = [System.Collections.Generic.HashSet[string]]::new([string[]]@($ExcludeTopLevel | Where-Object { $_ }), [System.StringComparer]::OrdinalIgnoreCase)
    $executable = [System.Collections.Generic.HashSet[string]]::new([string[]]@($ExecutableEntry | Where-Object { $_ }), [System.StringComparer]::Ordinal)
    # Unix mode 0100755 / 0100644 in the high 16 bits of the zip external attributes.
    $execAttr = [Convert]::ToInt32('100755', 8) -shl 16
    $fileAttr = [Convert]::ToInt32('100644', 8) -shl 16

    # A file that is executable on a Unix host (a native helper, a shell script the handler runs) keeps
    # its execute bit. Windows records no mode, so there only -ExecutableEntry marks a file executable.
    $anyExecute = [System.IO.UnixFileMode]::UserExecute -bor [System.IO.UnixFileMode]::GroupExecute -bor [System.IO.UnixFileMode]::OtherExecute

    $sourceFull = Resolve-LambdaPSPath -Path $Path
    $partial = "$destination.$([System.IO.Path]::GetRandomFileName()).partial"
    try {
        $archive = [System.IO.Compression.ZipFile]::Open($partial, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            # Entries are written in ordinal order of their names, each with the same timestamp, so the same
            # files give the same bytes whatever the file system's listing order and the files' modification
            # times.
            $files = [System.Collections.Generic.SortedDictionary[string, System.IO.FileInfo]]::new([System.StringComparer]::Ordinal)
            foreach ($file in Get-ChildItem -LiteralPath $sourceFull -Recurse -File -Force) {
                $entryName = [System.IO.Path]::GetRelativePath($sourceFull, $file.FullName) -replace '\\', '/'
                if (-not $entryName.Contains('/') -and $excluded.Contains($entryName)) {
                    continue
                }
                $files[$entryName] = $file
            }
            $entryTime = [System.DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)
            foreach ($pair in $files.GetEnumerator()) {
                $entryName = $pair.Key
                $file = $pair.Value
                # The time and mode are set before the entry is written: they cannot change afterwards.
                $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $entryTime
                $isExecutable = $executable.Contains($entryName) -or
                    (-not $IsWindows -and ([System.IO.File]::GetUnixFileMode($file.FullName) -band $anyExecute) -ne 0)
                $entry.ExternalAttributes = if ($isExecutable) { $execAttr } else { $fileAttr }
                $source = [System.IO.File]::OpenRead($file.FullName)
                try {
                    $target = $entry.Open()
                    try { $source.CopyTo($target) } finally { $target.Dispose() }
                }
                finally { $source.Dispose() }
            }
        }
        finally {
            $archive.Dispose()
        }
        Set-LambdaPSZipUnixHost -Path $partial
        Move-Item -LiteralPath $partial -Destination $destination -Force -Confirm:$false -WhatIf:$false
    }
    finally {
        if (Test-Path -LiteralPath $partial) {
            Remove-Item -LiteralPath $partial -Force -ErrorAction SilentlyContinue -Confirm:$false -WhatIf:$false
        }
    }
}
