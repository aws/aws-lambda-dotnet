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

        It 'Should unpack the host-architecture tarball, keeping the execute bit, and remove it' -Skip:$IsWindows {
            # Arrange
            $archive = [System.IO.Path]::Combine($TestDrive, 'powershell.tar.gz')
            $file = [System.IO.File]::Create($archive)
            $writer = [System.Formats.Tar.TarWriter]::new([System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionLevel]::Fastest))
            $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, './pwsh')
            $entry.Mode = [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute'
            $writer.WriteEntry($entry)
            $writer.Dispose()
            Mock Get-LambdaPSPwshArchive { $archive } -ParameterFilter { $PwshVersion -eq '7.6.9' -and $Architecture -eq 'x86_64' }

            # Act
            $pwsh = Get-LambdaPSHostPwsh -PwshVersion 7.6.9 -Architecture x86_64 -Destination $script:destination

            # Assert
            $pwsh | Should -Be ([System.IO.Path]::Combine($script:destination, 'pwsh'))
            [System.IO.File]::GetUnixFileMode($pwsh).HasFlag([System.IO.UnixFileMode]::UserExecute) | Should -BeTrue
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
    }
}
