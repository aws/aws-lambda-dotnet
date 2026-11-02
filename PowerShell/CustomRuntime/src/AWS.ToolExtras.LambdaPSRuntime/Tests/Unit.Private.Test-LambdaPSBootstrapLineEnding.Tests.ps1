BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBootstrapLineEnding' -Tag 'Unit' {
    It 'fails R-LineEndings for <Case>' -ForEach @(
        @{ Case = 'CRLF line endings'; Content = "#!/bin/sh`r`nexec pwsh`r`n" }
        @{ Case = 'a lone carriage return'; Content = "#!/bin/sh`rexec pwsh`n" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $finding = Test-LambdaPSBootstrapLineEnding -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content)))
            $finding.RuleId | Should -Be 'R-LineEndings'
            $finding.Passed | Should -BeFalse
        }
    }

    It 'passes LF line endings' {
        InModuleScope $script:moduleName {
            $finding = Test-LambdaPSBootstrapLineEnding -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`nexec pwsh`n")))
            $finding.Passed | Should -BeTrue
            $finding.Message | Should -Be 'Bootstrap uses LF line endings.'
        }
    }
}
