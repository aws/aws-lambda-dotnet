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
    Describe 'Get-LambdaPSPwshRepositoryRelease' -Tag 'Unit' {
        BeforeAll {
            $script:primaryXml = @'
<?xml version="1.0" encoding="UTF-8"?>
<metadata xmlns="http://linux.duke.edu/metadata/common" xmlns:rpm="http://linux.duke.edu/metadata/rpm" packages="6">
<package type="rpm"><name>powershell</name><arch>x86_64</arch><version epoch="0" ver="7.6.10" rel="1.rh"/></package>
<package type="rpm"><name>powershell</name><arch>x86_64</arch><version epoch="0" ver="7.6.9" rel="1.rh"/></package>
<package type="rpm"><name>powershell</name><arch>x86_64</arch><version epoch="0" ver="7.4.22" rel="1.rh"/></package>
<package type="rpm"><name>powershell-lts</name><arch>x86_64</arch><version epoch="0" ver="7.6.11" rel="1.rh"/></package>
<package type="rpm"><name>powershell-preview</name><arch>x86_64</arch><version epoch="0" ver="7.7.0" rel="1.rh"/></package>
<package type="rpm"><name>powershell</name><arch>aarch64</arch><version epoch="0" ver="7.6.12" rel="1.rh"/></package>
</metadata>
'@
            $script:primaryGz = [System.IO.Path]::Combine($TestDrive, 'primary.xml.gz')
            $file = [System.IO.File]::Create($script:primaryGz)
            $gzip = [System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionMode]::Compress)
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($script:primaryXml)
            $gzip.Write($bytes, 0, $bytes.Length); $gzip.Dispose(); $file.Dispose()
            $script:primarySha = (Get-FileHash -LiteralPath $script:primaryGz -Algorithm SHA256).Hash.ToLowerInvariant()

            $script:newRepomd = {
                param([string]$Href, [string]$Sha)
                @"
<?xml version="1.0" encoding="UTF-8"?>
<repomd xmlns="http://linux.duke.edu/metadata/repo">
  <data type="filelists"><checksum type="sha256">$('0' * 64)</checksum><location href="repodata/f-filelists.xml.gz"/></data>
  <data type="primary"><checksum type="sha256">$Sha</checksum><location href="$Href"/></data>
</repomd>
"@
            }
        }

        BeforeEach {
            $script:repomd = & $script:newRepomd 'repodata/abc-primary.xml.gz' $script:primarySha
            Mock 'Invoke-WebRequest' -ParameterFilter { $Uri -like '*/repomd.xml' } -MockWith {
                [PSCustomObject]@{ Content = $script:repomd }
            }
            Mock 'Invoke-WebRequest' -ParameterFilter { $OutFile } -MockWith {
                $script:downloaded = $OutFile
                Copy-Item -LiteralPath $script:primaryGz -Destination $OutFile
            }
        }

        It 'returns the x86_64 powershell releases of the line, oldest first' {
            Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6 |
                Should -Be @('7.6.9', '7.6.10')
        }

        It 'reads the primary list repomd.xml names, under the configured repository, and removes it afterwards' {
            $null = Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6
            Should -Invoke 'Invoke-WebRequest' -Times 1 -Exactly -ParameterFilter {
                $Uri -eq "$($script:Config.pwsh.PackageRepository.Url)/repodata/abc-primary.xml.gz" -and
                $OperationTimeoutSeconds -eq $script:Config.pwsh.PackageRepository.OperationTimeoutSeconds
            }
            Test-Path -LiteralPath $script:downloaded | Should -BeFalse
        }

        It 'throws PwshRepositoryLookupFailed when the primary list does not have the SHA256 repomd.xml names' {
            $script:repomd = & $script:newRepomd 'repodata/abc-primary.xml.gz' ('a' * 64)
            { Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6 } |
                Should -Throw -ErrorId 'PwshRepositoryLookupFailed' -ExpectedMessage '*has SHA256*'
        }

        It 'throws PwshRepositoryLookupFailed for a primary location outside repodata/: <Href>' -ForEach @(
            @{ Href = 'https://example.com/primary.xml.gz' }, @{ Href = 'repodata/../../primary.xml.gz' }
        ) {
            $script:repomd = & $script:newRepomd $Href $script:primarySha
            { Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6 } |
                Should -Throw -ErrorId 'PwshRepositoryLookupFailed'
            Should -Invoke 'Invoke-WebRequest' -Times 0 -Exactly -ParameterFilter { $OutFile }
        }

        It 'throws PwshRepositoryLookupFailed when the repository cannot be reached' {
            Mock 'Invoke-WebRequest' -ParameterFilter { $Uri -like '*/repomd.xml' } -MockWith { throw 'Name or service not known' }
            { Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6 } |
                Should -Throw -ErrorId 'PwshRepositoryLookupFailed' -ExpectedMessage '*Name or service not known*'
        }

        It 'refuses a repomd.xml with a DTD' {
            $script:repomd = '<?xml version="1.0"?><!DOCTYPE r [<!ENTITY x SYSTEM "file:///etc/passwd">]><repomd>&x;</repomd>'
            { Get-LambdaPSPwshRepositoryRelease -PowerShellVersion 7.6 } |
                Should -Throw -ErrorId 'PwshRepositoryLookupFailed'
        }
    }
}
