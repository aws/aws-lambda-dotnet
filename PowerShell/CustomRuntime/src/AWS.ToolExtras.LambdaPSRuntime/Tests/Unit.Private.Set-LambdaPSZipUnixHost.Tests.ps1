BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Zip.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Set-LambdaPSZipUnixHost' -Tag 'Unit' {
    # .NET on Windows records host 0 (MS-DOS/Windows), and Info-ZIP then ignores the Unix mode. Zeroing
    # the bytes here stands in for a Windows build on any host.
    It 'sets every entry''s host to Unix in a zip built as if on Windows, and keeps it readable' {
        $zip = [System.IO.Path]::Combine($TestDrive, 'windows-host.zip')
        $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($name in 'bootstrap', 'powershell/pwsh', 'handler.ps1') {
                $entry = $archive.CreateEntry($name)
                $writer = [System.IO.StreamWriter]::new($entry.Open())
                try { $writer.Write("content of $name") } finally { $writer.Dispose() }
                $mode = if ($name -eq 'handler.ps1') { '100644' } else { '100755' }
                $entry.ExternalAttributes = [Convert]::ToInt32($mode, 8) -shl 16
            }
        }
        finally { $archive.Dispose() }
        $bytes = [System.IO.File]::ReadAllBytes($zip)
        for ($i = 0; $i -le $bytes.Length - 4; $i++) { if ([System.BitConverter]::ToUInt32($bytes, $i) -eq 0x02014b50) { $bytes[$i + 5] = 0 } }
        [System.IO.File]::WriteAllBytes($zip, $bytes)
        @(Get-ZipEntryHost -ZipPath $zip | Where-Object { $_ -ne 0 }) | Should -BeNullOrEmpty

        InModuleScope $script:moduleName -Parameters @{ Zip = $zip } {
            param($Zip)
            Set-LambdaPSZipUnixHost -Path $Zip
        }

        $hosts = @(Get-ZipEntryHost -ZipPath $zip)
        $hosts.Count | Should -Be 3
        @($hosts | Where-Object { $_ -ne 3 }) | Should -BeNullOrEmpty
        Get-ZipEntryMode -ZipPath $zip -EntryName 'powershell/pwsh' | Should -Be ([Convert]::ToInt32('100755', 8))
        $read = [System.IO.Compression.ZipFile]::OpenRead($zip)
        try {
            $reader = [System.IO.StreamReader]::new(($read.Entries | Where-Object FullName -eq 'bootstrap').Open())
            try { $reader.ReadToEnd() | Should -Be 'content of bootstrap' } finally { $reader.Dispose() }
        }
        finally { $read.Dispose() }
    }

    # An archive over 4 GB records its central directory's offset only in the Zip64 end record. A small
    # archive given a Zip64 end record and locator, with 0xFFFFFFFF in the plain end record, stands in for it.
    It 'finds the central directory through the Zip64 end record' {
        $zip = [System.IO.Path]::Combine($TestDrive, 'zip64.zip')
        $archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($name in 'bootstrap', 'handler.ps1') {
                $writer = [System.IO.StreamWriter]::new($archive.CreateEntry($name).Open())
                try { $writer.Write("content of $name") } finally { $writer.Dispose() }
            }
        }
        finally { $archive.Dispose() }
        $bytes = [System.IO.File]::ReadAllBytes($zip)
        $end = $bytes.Length - 22
        $entries = [System.BitConverter]::ToUInt16($bytes, $end + 10)
        $directorySize = [System.BitConverter]::ToUInt32($bytes, $end + 12)
        $directoryOffset = [System.BitConverter]::ToUInt32($bytes, $end + 16)
        for ($i = $directoryOffset; $i -lt $end; $i++) { if ([System.BitConverter]::ToUInt32($bytes, $i) -eq 0x02014b50) { $bytes[$i + 5] = 0 } }
        $out = [System.IO.MemoryStream]::new()
        $writer = [System.IO.BinaryWriter]::new($out)
        $writer.Write($bytes, 0, $end)
        # Zip64 end record: signature, size of the rest (44), versions, disks, entry counts, size, offset.
        $writer.Write([uint32]0x06064b50); $writer.Write([uint64]44); $writer.Write([uint16]45); $writer.Write([uint16]45)
        $writer.Write([uint32]0); $writer.Write([uint32]0); $writer.Write([uint64]$entries); $writer.Write([uint64]$entries)
        $writer.Write([uint64]$directorySize); $writer.Write([uint64]$directoryOffset)
        # Zip64 locator: signature, disk, the Zip64 end record's offset, disk count.
        $writer.Write([uint32]0x07064b50); $writer.Write([uint32]0); $writer.Write([uint64]$end); $writer.Write([uint32]1)
        # The plain end record, with the central directory's offset left to the Zip64 record.
        $writer.Write($bytes, $end, 16); $writer.Write([uint32]::MaxValue); $writer.Write([uint16]0)
        $writer.Flush()
        [System.IO.File]::WriteAllBytes($zip, $out.ToArray())

        InModuleScope $script:moduleName -Parameters @{ Zip = $zip } {
            param($Zip)
            Set-LambdaPSZipUnixHost -Path $Zip
        }

        $updated = [System.IO.File]::ReadAllBytes($zip)
        $hosts = @(for ($i = $directoryOffset; $i -lt $end; $i++) { if ([System.BitConverter]::ToUInt32($updated, $i) -eq 0x02014b50) { $updated[$i + 5] } })
        $hosts | Should -Be @(3, 3)
    }

    It 'leaves an empty archive valid' {
        $zip = [System.IO.Path]::Combine($TestDrive, 'empty.zip')
        ([System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)).Dispose()
        $before = [System.IO.File]::ReadAllBytes($zip)

        InModuleScope $script:moduleName -Parameters @{ Zip = $zip } {
            param($Zip)
            Set-LambdaPSZipUnixHost -Path $Zip
        }

        [System.IO.File]::ReadAllBytes($zip) | Should -Be $before
    }

    It 'throws for a file that is not a zip' {
        $notZip = [System.IO.Path]::Combine($TestDrive, 'not.zip')
        Set-Content -LiteralPath $notZip -Value 'not a zip'

        { InModuleScope $script:moduleName -Parameters @{ Zip = $notZip } {
                param($Zip)
                Set-LambdaPSZipUnixHost -Path $Zip
            } } | Should -Throw '*end-of-central-directory*'
    }
}
