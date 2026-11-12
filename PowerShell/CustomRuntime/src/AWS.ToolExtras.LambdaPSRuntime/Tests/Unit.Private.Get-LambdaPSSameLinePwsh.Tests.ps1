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
        It 'Should return the running pwsh for a release of its line' {
            $release = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor).99"
            $pwsh = Get-LambdaPSSameLinePwsh -PwshVersion $release
            $pwsh | Should -Be ([System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })))
        }

        It 'Should return $null for a release of another line' {
            # Act / Assert
            Get-LambdaPSSameLinePwsh -PwshVersion '6.0.0' | Should -BeNullOrEmpty
        }
    }
}
