BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Zip.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Write-LambdaPSZipArchive' -Tag 'Unit' {
    It 'marks the named entries executable, drops only top-level exclusions, and leaves no partial file' {
        $stage = Join-Path $TestDrive 'zip-stage'
        $null = New-Item -ItemType Directory -Path (Join-Path $stage 'sub')
        'x' | Set-Content -LiteralPath (Join-Path $stage 'bootstrap')
        'x' | Set-Content -LiteralPath (Join-Path $stage 'README.md')
        'x' | Set-Content -LiteralPath (Join-Path $stage 'sub/README.md')
        $zip = Join-Path $TestDrive 'out/package.zip'

        InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
            param($Stage, $Zip)
            Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip -ExcludeTopLevel 'readme.md' -ExecutableEntry 'bootstrap'
        }

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
        try {
            @($archive.Entries.FullName | Sort-Object) | Should -Be @('bootstrap', 'sub/README.md')
            ($archive.Entries | Where-Object FullName -eq 'bootstrap').ExternalAttributes | Should -Be ([Convert]::ToInt32('100755', 8) -shl 16)
            ($archive.Entries | Where-Object FullName -eq 'sub/README.md').ExternalAttributes | Should -Be ([Convert]::ToInt32('100644', 8) -shl 16)
        }
        finally {
            $archive.Dispose()
        }
        @(Get-ChildItem -LiteralPath (Split-Path -Parent $zip) -Filter '*.partial') | Should -BeNullOrEmpty
    }

    # Extractors such as Info-ZIP apply the Unix mode only when an entry's "version made by" host is
    # Unix (3). On a Unix host .NET writes 3 itself; Unit.Private.Set-LambdaPSZipUnixHost covers a zip
    # built as if on Windows. Read every central-directory record back.
    It 'records Unix as the host of every entry, so extractors apply the modes' {
        $stage = Join-Path $TestDrive 'zip-host-stage'
        $null = New-Item -ItemType Directory -Path (Join-Path $stage 'powershell')
        'x' | Set-Content -LiteralPath (Join-Path $stage 'bootstrap')
        'x' | Set-Content -LiteralPath (Join-Path $stage 'powershell/pwsh')
        'x' | Set-Content -LiteralPath (Join-Path $stage 'handler.ps1')
        $zip = Join-Path $TestDrive 'zip-host/package.zip'

        InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
            param($Stage, $Zip)
            Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip -ExecutableEntry 'bootstrap', 'powershell/pwsh'
        }

        $hosts = @(Get-ZipEntryHost -ZipPath $zip)
        @($hosts).Count | Should -Be 3
        @($hosts | Where-Object { $_ -ne 3 }) | Should -BeNullOrEmpty
        # The archive still reads back, with its modes.
        Get-ZipEntryMode -ZipPath $zip -EntryName 'powershell/pwsh' | Should -Be ([Convert]::ToInt32('100755', 8))
    }

    # On a Unix host .NET already writes Unix as the host, so only the call itself shows the Windows fix is in.
    It 'marks every entry as made on Unix, on the partial archive before it is moved into place' {
        $stage = Join-Path $TestDrive 'zip-call-stage'
        $null = New-Item -ItemType Directory -Path $stage
        'x' | Set-Content -LiteralPath (Join-Path $stage 'bootstrap')
        $zip = Join-Path $TestDrive 'zip-call/package.zip'

        InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
            param($Stage, $Zip)
            Mock 'Set-LambdaPSZipUnixHost' -MockWith { }
            Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip
            Should -Invoke 'Set-LambdaPSZipUnixHost' -Times 1 -Exactly -ParameterFilter { $Path -like '*.partial' }
        }
    }

    # The same files give the same bytes: entries in ordinal order of their names, each with one fixed time.
    It 'writes the same bytes for the same files, whatever their order on disk and their modification times' {
        $first = Join-Path $TestDrive 'repro-a'
        $second = Join-Path $TestDrive 'repro-b'
        foreach ($stage in $first, $second) { $null = New-Item -ItemType Directory -Path (Join-Path $stage 'lib') -Force }
        # Created in a different order, with different times.
        foreach ($name in 'b.txt', 'a.txt', 'lib/c.txt', 'Z.txt') { "content of $name" | Set-Content -LiteralPath (Join-Path $first $name) }
        foreach ($name in 'lib/c.txt', 'Z.txt', 'a.txt', 'b.txt') { "content of $name" | Set-Content -LiteralPath (Join-Path $second $name) }
        Get-ChildItem -LiteralPath $second -Recurse -File | ForEach-Object { $_.LastWriteTime = [datetime]'2001-02-03' }
        $zipA = Join-Path $TestDrive 'repro-a.zip'
        $zipB = Join-Path $TestDrive 'repro-b.zip'

        InModuleScope $script:moduleName -Parameters @{ A = $first; B = $second; ZipA = $zipA; ZipB = $zipB } {
            param($A, $B, $ZipA, $ZipB)
            Write-LambdaPSZipArchive -Path $A -DestinationPath $ZipA
            Write-LambdaPSZipArchive -Path $B -DestinationPath $ZipB
        }

        (Get-FileHash -LiteralPath $zipB -Algorithm SHA256).Hash | Should -Be (Get-FileHash -LiteralPath $zipA -Algorithm SHA256).Hash
        $names = Get-ZipEntryNames -ZipPath $zipA
        # Ordinal order puts 'Z' before 'a'; the names never differ only in case, so they are distinct files
        # on a case-insensitive file system too.
        $names | Should -Be @('Z.txt', 'a.txt', 'b.txt', 'lib/c.txt')
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zipA)
        try { @($archive.Entries | Where-Object { $_.LastWriteTime.Year -ne 1980 }) | Should -BeNullOrEmpty }
        finally { $archive.Dispose() }
    }

    # Off Windows '\' is part of a file name, not a folder separator, so a name holding it is refused
    # rather than stored as a different path (where it could also replace a real file of that path).
    It 'refuses a file with a backslash in its name, writing no package' -Skip:$IsWindows {
        $stage = Join-Path $TestDrive 'backslash-stage'
        $null = New-Item -ItemType Directory -Path (Join-Path $stage 'lib') -Force
        'real' | Set-Content -LiteralPath (Join-Path $stage 'lib/data.txt')
        # Written through .NET: PowerShell's own path handling reads '\' as a separator on every OS.
        [System.IO.File]::WriteAllText([System.IO.Path]::Combine($stage, 'lib\data.txt'), 'other')
        $zip = Join-Path $TestDrive 'backslash-out/package.zip'

        { InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
                param($Stage, $Zip)
                Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip
            } } | Should -Throw -ErrorId 'BackslashInFileName'
        Test-Path -LiteralPath $zip | Should -BeFalse
        @(Get-ChildItem -LiteralPath (Split-Path -Parent $zip) -Filter '*.partial') | Should -BeNullOrEmpty
    }

    # A file link would ship the file it points to, which can be outside -Path; a folder link would ship nothing.
    It 'refuses a <Case>, writing no package' -ForEach @(
        @{ Case = 'file link to a file outside -Path'; Folder = $false }
        @{ Case = 'folder link'; Folder = $true }
    ) {
        if ($IsWindows) { Set-ItResult -Skipped -Because 'creating a symbolic link needs elevation on Windows'; return }
        $stage = Join-Path $TestDrive "link-stage-$Folder"
        $null = New-Item -ItemType Directory -Path (Join-Path $stage 'powershell/dir') -Force
        'a' | Set-Content -LiteralPath (Join-Path $stage 'powershell/dir/a.txt')
        $outside = Join-Path $TestDrive "outside-$Folder.txt"
        'secret' | Set-Content -LiteralPath $outside
        $target = if ($Folder) { Join-Path $stage 'powershell/dir' } else { $outside }
        $null = New-Item -ItemType SymbolicLink -Path (Join-Path $stage 'powershell/linked') -Target $target
        $zip = Join-Path $TestDrive "link-out-$Folder/package.zip"

        { InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
                param($Stage, $Zip)
                Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip
            } } | Should -Throw -ErrorId 'PackageEntryIsLink'
        Test-Path -LiteralPath $zip | Should -BeFalse
    }

    # Lambda's quotas: 50 MB zipped for a direct upload, 250 MB unzipped for code and layers together.
    Context 'Lambda''s package size limits' {
        It 'warns about the <Case>' -ForEach @(
            @{ Case = 'direct-upload limit for a zip over 50 MB'; Random = $true; Bytes = 51MB; Expected = "*is 51* MB, over the 50 MB Lambda accepts in a direct upload*-Code_S3Bucket*" }
            @{ Case = 'unzipped limit for files over 250 MB'; Random = $false; Bytes = 251MB; Expected = '*unzips to 251 MB, over the 250 MB*' }
        ) {
            $stage = Join-Path $TestDrive "size-$Bytes"
            $null = New-Item -ItemType Directory -Path $stage
            $file = [System.IO.File]::Create((Join-Path $stage 'payload.bin'))
            try {
                # Random bytes do not compress, so the zip is as large as the file; zeros compress to almost nothing.
                if ($Random) {
                    $buffer = [byte[]]::new(1MB)
                    for ($i = 0; $i -lt $Bytes / 1MB; $i++) {
                        [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
                        $file.Write($buffer, 0, $buffer.Length)
                    }
                }
                else { $file.SetLength($Bytes) }
            }
            finally { $file.Dispose() }
            $zip = Join-Path $TestDrive "size-$Bytes.zip"

            $warnings = InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
                param($Stage, $Zip)
                Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip -WarningVariable w -WarningAction SilentlyContinue
                $w
            }

            @($warnings).Count | Should -Be 1
            "$($warnings[0])" | Should -BeLike $Expected
            Test-Path -LiteralPath $zip | Should -BeTrue -Because 'the package is still written'
        }

        It 'does not warn for a package within both limits' {
            $stage = Join-Path $TestDrive 'size-small'
            $null = New-Item -ItemType Directory -Path $stage
            'x' | Set-Content -LiteralPath (Join-Path $stage 'handler.ps1')

            $warnings = InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = (Join-Path $TestDrive 'size-small.zip') } {
                param($Stage, $Zip)
                Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip -WarningVariable w -WarningAction SilentlyContinue
                $w
            }

            $warnings | Should -BeNullOrEmpty
        }
    }

    It 'names a file in a subfolder with a forward slash' {
        $stage = Join-Path $TestDrive 'subfolder-stage'
        $null = New-Item -ItemType Directory -Path (Join-Path $stage 'lib') -Force
        'real' | Set-Content -LiteralPath (Join-Path $stage 'lib/data.txt')
        $zip = Join-Path $TestDrive 'subfolder.zip'

        InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
            param($Stage, $Zip)
            Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip
        }
        Get-ZipEntryNames -ZipPath $zip | Should -Be @('lib/data.txt')
    }

    # The archive is written beside the destination and swapped in only when complete, so a failed
    # rebuild keeps the previous package. A file this user cannot read makes the write fail part-way
    # (root reads it anyway, hence the skip).
    It 'keeps an existing package and leaves no partial file when the write fails' -Skip:($IsWindows -or (& id -u) -eq '0') {
        $stage = Join-Path $TestDrive 'zip-fail-stage'
        $null = New-Item -ItemType Directory -Path $stage
        'x' | Set-Content -LiteralPath (Join-Path $stage 'a.txt')
        $unreadable = Join-Path $stage 'z.txt'
        'x' | Set-Content -LiteralPath $unreadable
        & chmod 000 $unreadable
        $zip = Join-Path $TestDrive 'zip-fail/package.zip'
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $zip)
        'an older package' | Set-Content -LiteralPath $zip

        try {
            { InModuleScope $script:moduleName -Parameters @{ Stage = $stage; Zip = $zip } {
                    param($Stage, $Zip)
                    Write-LambdaPSZipArchive -Path $Stage -DestinationPath $Zip
                } } | Should -Throw
        }
        finally { & chmod 644 $unreadable }

        Get-Content -LiteralPath $zip -Raw | Should -Match 'an older package'
        @(Get-ChildItem -LiteralPath (Split-Path -Parent $zip) -Filter '*.partial') | Should -BeNullOrEmpty
    }
}
