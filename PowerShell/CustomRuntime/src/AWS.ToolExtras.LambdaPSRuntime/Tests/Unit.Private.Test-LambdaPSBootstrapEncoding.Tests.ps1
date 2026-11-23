BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBootstrapEncoding' -Tag 'Unit' {
    It 'fails R-Encoding for a bootstrap that starts with a <Name> byte order mark' -ForEach @(
        @{ Name = 'UTF-8 (EF BB BF)'; Encoding = [System.Text.UTF8Encoding]::new($true) }
        @{ Name = 'UTF-16 LE (FF FE)'; Encoding = [System.Text.UnicodeEncoding]::new($false, $true) }
        @{ Name = 'UTF-16 BE (FE FF)'; Encoding = [System.Text.UnicodeEncoding]::new($true, $true) }
        @{ Name = 'UTF-32 LE (FF FE 00 00)'; Encoding = [System.Text.UTF32Encoding]::new($false, $true) }
        @{ Name = 'UTF-32 BE (00 00 FE FF)'; Encoding = [System.Text.UTF32Encoding]::new($true, $true) }
    ) {
        $bytes = [byte[]]($Encoding.GetPreamble() + $Encoding.GetBytes("#!/bin/sh`nexec pwsh"))
        InModuleScope $script:moduleName -Parameters @{ Bytes = $bytes; Name = $Name } {
            param($Bytes, $Name)
            $finding = Test-LambdaPSBootstrapEncoding -Bootstrap (Read-LambdaPSBootstrap -Bytes $Bytes)
            $finding.RuleId | Should -Be 'R-Encoding'
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike "Bootstrap starts with a $Name byte order mark.*"
        }
    }

    It 'passes <Case>' -ForEach @(
        @{ Case = 'a bootstrap without a byte order mark'; Bytes = [System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`nexec pwsh") }
        @{ Case = 'a mark that is not at the start'; Bytes = [byte[]]([System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`n# ") + [byte[]](0xEF, 0xBB, 0xBF)) }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Bytes = $Bytes } {
            param($Bytes)
            (Test-LambdaPSBootstrapEncoding -Bootstrap (Read-LambdaPSBootstrap -Bytes $Bytes)).Passed | Should -BeTrue
        }
    }

    # The kernel runs a script only when its first two bytes are '#!'.
    It 'fails R-Encoding for a bootstrap that does not start with "#!" (<Case>)' -ForEach @(
        @{ Case = 'a leading blank line'; Content = "`n#!/bin/sh`nexec pwsh" }
        @{ Case = 'a comment above the shebang'; Content = "# Copyright`n#!/bin/sh`nexec pwsh" }
        @{ Case = 'a leading space'; Content = " #!/bin/sh`nexec pwsh" }
        @{ Case = 'no shebang'; Content = 'exec pwsh' }
        @{ Case = 'an empty bootstrap'; Content = '' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $finding = Test-LambdaPSBootstrapEncoding -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content)))
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike 'Bootstrap does not start with "#!".*'
        }
    }
}
