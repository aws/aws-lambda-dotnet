function Set-LambdaPSZipUnixHost {
    <#
    .SYNOPSIS
        Records Unix as the host of every entry in a .zip.
    .DESCRIPTION
        Sets the host byte of "version made by" (offset 5 of each central-directory record) to Unix (3).
        Extractors such as Info-ZIP apply the Unix mode in an entry's external attributes only when its
        host is Unix, and .NET records the OS it runs on, with no API to change it. Write-LambdaPSZipArchive
        calls this on the closed archive, so a zip built on Windows keeps its 0755 entries executable.

        The records start where the end-of-central-directory record (or, for a Zip64 archive, the Zip64
        one it points at) says, and are walked by their own lengths. Nothing else in the file changes.
    .PARAMETER Path
        The closed .zip to update in place.
    .OUTPUTS
        None.
    .EXAMPLE
        Set-LambdaPSZipUnixHost -Path $partial

        Marks every entry of the archive at $partial as made on Unix.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '',
        Justification = 'Private helper: it rewrites one byte per entry of the closed partial archive its caller is about to move into place, under the caller''s confirmation.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path
    )

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    try {
        $reader = [System.IO.BinaryReader]::new($stream, [System.Text.Encoding]::UTF8, $true)
        # The end record is 22 bytes plus a comment of at most 65535 bytes, at the end of the file.
        $tailLength = [int][System.Math]::Min($stream.Length, 22 + 65535)
        $stream.Position = $stream.Length - $tailLength
        $tail = $reader.ReadBytes($tailLength)
        $end = -1
        for ($i = $tail.Length - 22; $i -ge 0; $i--) {
            if ([System.BitConverter]::ToUInt32($tail, $i) -eq 0x06054b50) { $end = $i; break }
        }
        if ($end -lt 0) { throw [System.IO.InvalidDataException]::new("'$Path' has no end-of-central-directory record.") }
        [long]$offset = [System.BitConverter]::ToUInt32($tail, $end + 16)
        if ($offset -eq [uint32]::MaxValue) {
            # Zip64: the 20-byte locator before the end record holds the Zip64 end record's offset,
            # and that record holds the central directory's offset at byte 48.
            $stream.Position = [System.BitConverter]::ToInt64($tail, $end - 20 + 8) + 48
            $offset = $reader.ReadInt64()
        }
        $stream.Position = $offset
        while ($reader.ReadUInt32() -eq 0x02014b50) {
            $record = $stream.Position - 4
            $stream.Position = $record + 5
            $stream.WriteByte(3)
            $stream.Position = $record + 28
            $nameLength = $reader.ReadUInt16()
            $extraLength = $reader.ReadUInt16()
            $commentLength = $reader.ReadUInt16()
            $stream.Position = $record + 46 + $nameLength + $extraLength + $commentLength
        }
    }
    finally {
        $stream.Dispose()
    }
}
