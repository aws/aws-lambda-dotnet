BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSDownloadedModule' -Tag 'Unit' {
    It 'builds a typed DownloadedModule whose Version sorts as a version' {
        InModuleScope $script:moduleName {
            $out = @(
                New-LambdaPSDownloadedModule -Name 'LambdaPSFake.A' -Version '1.0.10' -Source PSResourceGet -Repository 'Internal' -Path '/d/a' -Action Saved
                New-LambdaPSDownloadedModule -Name 'LambdaPSFake.A' -Version '1.0.9' -Source Local -Repository '' -Path '/d/b' -Action Copied
            )
            $out[0].PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.DownloadedModule'
            $out[0].Version | Should -BeOfType [version]
            $out[1].Repository | Should -BeNullOrEmpty
            @($out | Sort-Object Version -Descending | ForEach-Object { [string]$_.Version }) | Should -Be @('1.0.10', '1.0.9')
        }
    }
}
