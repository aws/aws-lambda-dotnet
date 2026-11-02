BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSPwshArchive' -Tag 'Unit' {
    It 'returns a caller-supplied -PwshArchivePath without downloading when it matches the pinned hash' {
        InModuleScope $script:moduleName {
            $tmp = [System.IO.Path]::Combine($TestDrive, 'byo.tar.gz')
            Set-Content -LiteralPath $tmp -Value 'fake'
            $hash = (Get-FileHash -LiteralPath $tmp -Algorithm SHA256).Hash
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { $hash }
            Mock 'Invoke-WebRequest' -MockWith { throw 'must not download when BYO is supplied' }
            $result = Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64' -PwshArchivePath $tmp
            $result | Should -Be ([System.IO.Path]::GetFullPath($tmp))
            Should -Invoke 'Invoke-WebRequest' -Times 0 -Exactly
        }
    }

    It 'throws PwshArchiveHashMismatch for a -PwshArchivePath that is not the pinned tarball, and keeps the file' {
        InModuleScope $script:moduleName {
            $tmp = [System.IO.Path]::Combine($TestDrive, 'wrong-arch.tar.gz')
            Set-Content -LiteralPath $tmp -Value 'fake'
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            { Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'arm64' -PwshArchivePath $tmp } |
                Should -Throw -ErrorId 'PwshArchiveHashMismatch'
            # The file is the caller's, so a mismatch does not delete it.
            Test-Path -LiteralPath $tmp | Should -BeTrue
        }
    }

    It 'uses a -PwshArchivePath as-is when no hash is pinned for the version' {
        InModuleScope $script:moduleName {
            $tmp = [System.IO.Path]::Combine($TestDrive, 'unpinned.tar.gz')
            Set-Content -LiteralPath $tmp -Value 'fake'
            Mock 'Get-FileHash' -MockWith { throw 'must not hash when no hash is pinned' }
            $result = Get-LambdaPSPwshArchive -PwshVersion '9.9.9' -Architecture 'x86_64' -PwshArchivePath $tmp
            $result | Should -Be ([System.IO.Path]::GetFullPath($tmp))
        }
    }

    It 'does not hash a -PwshArchivePath under -ValidateOnly' {
        InModuleScope $script:moduleName {
            $tmp = [System.IO.Path]::Combine($TestDrive, 'validate.tar.gz')
            Set-Content -LiteralPath $tmp -Value 'fake'
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            Mock 'Get-FileHash' -MockWith { throw 'must not hash under -ValidateOnly' }
            Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64' -PwshArchivePath $tmp -ValidateOnly | Should -BeNullOrEmpty
        }
    }

    It 'throws PwshArchiveNotFound when -PwshArchivePath is missing' {
        InModuleScope $script:moduleName {
            { Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64' -PwshArchivePath '/no/such/file.tar.gz' } |
                Should -Throw -ErrorId 'PwshArchiveNotFound'
        }
    }

    It 'throws UnknownPwshArchiveHash for a version with no pinned hash (fail closed, no download)' {
        InModuleScope $script:moduleName {
            Mock 'Invoke-WebRequest' -MockWith { throw 'must not download an unverifiable version' }
            { Get-LambdaPSPwshArchive -PwshVersion '9.9.9' -Architecture 'x86_64' } |
                Should -Throw -ErrorId 'UnknownPwshArchiveHash'
            Should -Invoke 'Invoke-WebRequest' -Times 0 -Exactly
        }
    }

    It 'refuses a -PwshVersion with a trailing newline at binding' {
        InModuleScope $script:moduleName {
            { Get-LambdaPSPwshArchive -PwshVersion "7.4.20`n" -Architecture 'x86_64' -ValidateOnly } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Get-LambdaPSPwshArchive'
            # The hash lookup refuses the same value with the same error, so the parameter's own pattern is
            # what shows the binding refuses it.
            $pattern = ((Get-Command Get-LambdaPSPwshArchive).Parameters['PwshVersion'].Attributes |
                Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }).RegexPattern
            "7.4.20`n" -match $pattern | Should -BeFalse
        }
    }

    # \d also matches other scripts' digits, such as Arabic-Indic ones; a version is ASCII digits only.
    It 'refuses a -PwshVersion in non-ASCII digits at binding' {
        InModuleScope $script:moduleName {
            # A pinned hash, so only this cmdlet's own binding can refuse it.
            Mock 'Get-LambdaPSPwshArchiveHash' -RemoveParameterValidation 'PwshVersion' -MockWith { 'abc123' }
            { Get-LambdaPSPwshArchive -PwshVersion "7.4.$([char]0x0662)$([char]0x0660)" -Architecture 'x86_64' -ValidateOnly } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Get-LambdaPSPwshArchive'
        }
    }

    It 'checks a pinned version without downloading it under -ValidateOnly' {
        InModuleScope $script:moduleName {
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            Mock 'Invoke-WebRequest' -MockWith { throw 'must not download under -ValidateOnly' }
            Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64' -ValidateOnly | Should -BeNullOrEmpty
            Should -Invoke 'Invoke-WebRequest' -Times 0 -Exactly
        }
    }

    It 'downloads and returns the tarball when the SHA256 matches the pinned hash' {
        InModuleScope $script:moduleName {
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            Mock 'Invoke-WebRequest' -MockWith { }
            Mock 'Get-FileHash' -MockWith { [PSCustomObject]@{ Hash = 'ABC123' } }
            $result = Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64'
            $result | Should -Match 'powershell-7\.4\.20-linux-x64\.tar\.gz$'
            Should -Invoke 'Invoke-WebRequest' -Times 1 -Exactly -ParameterFilter {
                $Uri -eq 'https://github.com/PowerShell/PowerShell/releases/download/v7.4.20/powershell-7.4.20-linux-x64.tar.gz' -and
                $MaximumRetryCount -eq $script:Config.pwsh.Download.RetryCount -and
                $RetryIntervalSec -eq $script:Config.pwsh.Download.RetryDelaySeconds -and
                $ConnectionTimeoutSeconds -eq $script:Config.pwsh.Download.ConnectTimeoutSeconds -and
                # A download that stalls part-way fails rather than waiting indefinitely.
                $OperationTimeoutSeconds -eq $script:Config.pwsh.Download.OperationTimeoutSeconds
            }
        }
    }

    # Ctrl+C stops the pipeline, which skips catch blocks but runs finally blocks.
    It 'removes the partial tarball when the download is stopped part-way' {
        $record = [System.IO.Path]::Combine($TestDrive, 'download-outfile.txt')
        $ps = [powershell]::Create()
        try {
            $null = $ps.AddScript({
                    param($ModulePath, $Record)
                    $module = Import-Module $ModulePath -Force -PassThru
                    & $module {
                        param($Record)
                        function Get-LambdaPSPwshArchiveHash { 'AB' * 32 }
                        # A download in progress: part of the file is written, then the transfer is slow.
                        function Invoke-WebRequest {
                            param($Uri, $OutFile, [switch]$UseBasicParsing, $MaximumRetryCount, $RetryIntervalSec, $ConnectionTimeoutSeconds, $OperationTimeoutSeconds)
                            [System.IO.File]::WriteAllBytes($OutFile, [byte[]]::new(1024))
                            Set-Content -LiteralPath $Record -Value $OutFile
                            Start-Sleep -Seconds 60
                        }
                        Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64'
                    } $Record
                }).AddArgument($script:modulePath).AddArgument($record)
            $async = $ps.BeginInvoke()
            $deadline = [datetime]::UtcNow.AddSeconds(120)
            while (-not (Test-Path -LiteralPath $record) -and [datetime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
            Test-Path -LiteralPath $record | Should -BeTrue
            $partial = (Get-Content -LiteralPath $record -Raw).Trim()
            Test-Path -LiteralPath $partial | Should -BeTrue

            $ps.Stop()
            try { $null = $ps.EndInvoke($async) } catch { $null = $_ }

            Test-Path -LiteralPath $partial | Should -BeFalse
        }
        finally { $ps.Dispose() }
    }

    It 'throws PwshArchiveHashMismatch and does not return when the SHA256 differs' {
        InModuleScope $script:moduleName {
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            Mock 'Invoke-WebRequest' -MockWith { }
            Mock 'Get-FileHash' -MockWith { [PSCustomObject]@{ Hash = 'deadbeef' } }
            Mock 'Remove-Item' -MockWith { }
            { Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'x86_64' } |
                Should -Throw -ErrorId 'PwshArchiveHashMismatch'
            # The unverified download must not be left where something could use it.
            Should -Invoke 'Remove-Item' -Times 1 -Exactly -ParameterFilter { $LiteralPath -like '*powershell-7.4.20-linux-x64.tar.gz' }
        }
    }

    It 'deletes a partial download when the download fails' {
        InModuleScope $script:moduleName {
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            Mock 'Invoke-WebRequest' -MockWith { throw [System.Net.Http.HttpRequestException]::new('connection reset') }
            Mock 'Remove-Item' -MockWith { }
            { Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'arm64' } | Should -Throw '*connection reset*'
            Should -Invoke 'Remove-Item' -Times 1 -Exactly -ParameterFilter { $LiteralPath -like '*powershell-7.4.20-linux-arm64.tar.gz' }
        }
    }

    It 'verifies against the hash from Get-LambdaPSPwshArchiveHash (the single pinned source)' {
        InModuleScope $script:moduleName {
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { 'abc123' }
            Mock 'Invoke-WebRequest' -MockWith { }
            Mock 'Get-FileHash' -MockWith { [PSCustomObject]@{ Hash = 'abc123' } }
            $result = Get-LambdaPSPwshArchive -PwshVersion '7.4.20' -Architecture 'arm64'
            $result | Should -Match 'powershell-7\.4\.20-linux-arm64\.tar\.gz$'
            Should -Invoke 'Get-LambdaPSPwshArchiveHash' -Times 1 -Exactly -ParameterFilter {
                $PwshVersion -eq '7.4.20' -and $Architecture -eq 'arm64'
            }
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they surface,
    # and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'PwshArchiveNotFound'; Case = 'a missing -PwshArchivePath'; Download = $false; Expected = 'abc123'; Path = 'missing' }
        @{ ErrorId = 'PwshArchiveHashMismatch'; Case = 'a supplied tarball'; Download = $false; Expected = 'abc123'; Path = 'present' }
        @{ ErrorId = 'UnknownPwshArchiveHash'; Case = 'a version with no pinned hash'; Download = $true; Expected = $null; Path = $null }
        @{ ErrorId = 'PwshArchiveHashMismatch'; Case = 'a download'; Download = $true; Expected = 'abc123'; Path = $null }
    ) {
        $archive = switch ($Path) {
            'missing' { [System.IO.Path]::Combine($TestDrive, 'no-such.tar.gz') }
            'present' { $f = [System.IO.Path]::Combine($TestDrive, 'present.tar.gz'); Set-Content -LiteralPath $f -Value 'x'; $f }
            default { $null }
        }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Expected = $Expected; Archive = $archive } {
            param($ErrorId, $Expected, $Archive)
            $script:expectedHash = $Expected
            Mock 'Get-LambdaPSPwshArchiveHash' -MockWith { $script:expectedHash }
            Mock 'Invoke-WebRequest' -MockWith { }
            Mock 'Get-FileHash' -MockWith { [PSCustomObject]@{ Hash = 'DEF456' } }
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $params = @{ PwshVersion = '7.4.20'; Architecture = 'x86_64' }
            if ($Archive) { $params.PwshArchivePath = $Archive }
            $err = $null
            $collected = $null

            try { Invoke-LambdaPSTestCaller -Call { Get-LambdaPSPwshArchive @params } -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSPwshArchive' }) | Should -BeNullOrEmpty
        }
    }
}
