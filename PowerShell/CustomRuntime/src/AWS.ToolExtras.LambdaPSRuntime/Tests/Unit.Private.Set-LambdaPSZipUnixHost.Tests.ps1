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
