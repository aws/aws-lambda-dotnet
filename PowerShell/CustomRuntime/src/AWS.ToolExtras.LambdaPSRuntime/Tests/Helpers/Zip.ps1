# Reads the .zip files the packaging cmdlets write.

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ZipEntryNames {
    param($ZipPath)
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try { , @($zip.Entries.FullName) } finally { $zip.Dispose() }
}

function Get-ZipEntryMode {
    param($ZipPath, $EntryName)
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq $EntryName }
        if ($null -eq $entry) { return $null }
        # High 16 bits hold the Unix st_mode; mask off the sign-extension from the arithmetic shift.
        return (($entry.ExternalAttributes -shr 16) -band 0xFFFF)
    } finally { $zip.Dispose() }
}

function Get-ZipEntry {
    # Name and permission bits (Mode, for example 0x1ED for 0755) of every entry.
    param($ZipPath)
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try { @($zip.Entries | ForEach-Object { [PSCustomObject]@{ Name = $_.FullName; Mode = ($_.ExternalAttributes -shr 16) -band 0x1FF } }) }
    finally { $zip.Dispose() }
}

function Get-ZipEntryHost {
    # The host byte of "version made by" in every central-directory record, read from the file:
    # 3 is Unix, 0 is MS-DOS/Windows. System.IO.Compression does not expose it.
    param([string]$ZipPath)
    $bytes = [System.IO.File]::ReadAllBytes($ZipPath)
    $end = -1
    for ($i = $bytes.Length - 22; $i -ge 0; $i--) { if ([System.BitConverter]::ToUInt32($bytes, $i) -eq 0x06054b50) { $end = $i; break } }
    $count = [System.BitConverter]::ToUInt16($bytes, $end + 10)
    $offset = [System.BitConverter]::ToUInt32($bytes, $end + 16)
    for ($n = 0; $n -lt $count; $n++) {
        [System.BitConverter]::ToUInt32($bytes, $offset) | Should -Be 0x02014b50
        $bytes[$offset + 5]
        $offset += 46 + [System.BitConverter]::ToUInt16($bytes, $offset + 28) + [System.BitConverter]::ToUInt16($bytes, $offset + 30) +
            [System.BitConverter]::ToUInt16($bytes, $offset + 32)
    }
}
