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
    Describe 'Get-LambdaPSSameLinePwsh' -Tag 'Unit' {
        BeforeAll {
            $release = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor).99"
            $probe = [System.IO.Path]::Combine($TestDrive, 'same-line-probe')
        }

        It 'Should return the running pwsh for a release of its line' {
            $pwsh = Get-LambdaPSSameLinePwsh -PwshVersion $release -Destination $probe
            $pwsh | Should -Be ([System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })))
        }

        It 'Should return $null for a release of another line' {
            Get-LambdaPSSameLinePwsh -PwshVersion '6.0.0' -Destination $probe | Should -BeNullOrEmpty
        }

        # As on a host without the OpenSSL that Add-Type loads: the cold-start fallback, not ContextCompileFailed.
        It 'Should return $null when the running pwsh cannot compile here' {
            Mock 'Invoke-LambdaPSContextPrecompile' -MockWith { throw [System.Management.Automation.ErrorRecord]::new([System.InvalidOperationException]::new('no OpenSSL'), 'ContextCompileFailed', 'InvalidResult', $null) }
            Get-LambdaPSSameLinePwsh -PwshVersion $release -Destination $probe | Should -BeNullOrEmpty
        }
    }
}
