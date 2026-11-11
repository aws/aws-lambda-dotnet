# InModuleScope wraps the tests at discovery, so the module is imported then too.
BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' {
    Describe 'Get-LambdaPSHostPwsh' -Tag 'Unit' {
        BeforeEach {
            $script:destination = [System.IO.Path]::Combine($TestDrive, [guid]::NewGuid().ToString('N'))
        }

        It 'Should return the unpacked pwsh only when it can compile (<Case>), and remove the tarball' -Skip:$IsWindows -ForEach @(
            @{ Case = 'compiles'; Script = "exec '$([System.Environment]::ProcessPath)' `"`$@`"" }
            @{ Case = 'exits 1, as without OpenSSL or another library it needs'; Script = 'exit 1' }
            @{ Case = 'starts but writes no assembly'; Script = 'exit 0' }
        ) {
            # Arrange
            $archive = [System.IO.Path]::Combine($TestDrive, "powershell-$([guid]::NewGuid().ToString('N')).tar.gz")
            $file = [System.IO.File]::Create($archive)
            $writer = [System.Formats.Tar.TarWriter]::new([System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionLevel]::Fastest))
            $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, './pwsh')
            $entry.Mode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute'
            $entry.DataStream = [System.IO.MemoryStream]::new([System.Text.Encoding]::ASCII.GetBytes("#!/bin/sh`n$Script`n"))
            $writer.WriteEntry($entry)
            $writer.Dispose()
            Mock Get-LambdaPSPwshArchive { $archive } -ParameterFilter { $PwshVersion -eq '7.6.9' -and $Architecture -eq 'x86_64' }

            # Act
            $pwsh = Get-LambdaPSHostPwsh -PwshVersion 7.6.9 -Architecture x86_64 -Destination $script:destination

            # Assert
            if ($Case -eq 'compiles') { $pwsh | Should -Be ([System.IO.Path]::Combine($script:destination, 'pwsh')) } else { $pwsh | Should -BeNullOrEmpty }
            Test-Path -LiteralPath $archive | Should -BeFalse
            Should -Invoke Get-LambdaPSPwshArchive -Times 1 -Exactly
        }

        It 'Should return $null, with <Warnings> warnings, when the fetch fails with <ErrorId>' -ForEach @(
            @{ ErrorId = 'PwshArchiveDownloadFailed'; Warnings = 0 }
            @{ ErrorId = 'PwshArchiveHashMismatch'; Warnings = 1 }
        ) {
            # Arrange
            $record = [System.Management.Automation.ErrorRecord]::new([System.Exception]::new('fetch failed'), $ErrorId, 'NotSpecified', $null)
            Mock Get-LambdaPSPwshArchive { throw $record }

            # Act
            $pwsh = Get-LambdaPSHostPwsh -PwshVersion 7.6.9 -Architecture x86_64 -Destination $script:destination -WarningVariable raised -WarningAction SilentlyContinue

            # Assert
            $pwsh | Should -BeNullOrEmpty
            $raised.Count | Should -Be $Warnings
            Should -Invoke Get-LambdaPSPwshArchive -Times 1 -Exactly
        }

        It 'Should return $null and remove the tarball when it cannot be unpacked' {
            # Arrange
            $archive = [System.IO.Path]::Combine($TestDrive, 'truncated.tar.gz')
            Set-Content -LiteralPath $archive -Value 'not a gzip stream'
            Mock Get-LambdaPSPwshArchive { $archive }

            # Act
            $pwsh = Get-LambdaPSHostPwsh -PwshVersion 7.6.9 -Architecture x86_64 -Destination $script:destination

            # Assert
            $pwsh | Should -BeNullOrEmpty
            Test-Path -LiteralPath $archive | Should -BeFalse
            Should -Invoke Get-LambdaPSPwshArchive -Times 1 -Exactly
        }
    }
}
