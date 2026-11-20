BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBootstrapEnvironment' -Tag 'Unit' {
    It 'passes R-Env-Path and R-Env-LdLibraryPath for <Case>' -ForEach @(
        @{ Case = 'export lines'; Content = "#!/bin/sh`nexport PATH=/opt/powershell`nexport LD_LIBRARY_PATH=/opt/lib`nexec pwsh" }
        @{ Case = 'assignments then one export line'; Content = "#!/bin/sh`nPATH=/opt/powershell`nLD_LIBRARY_PATH=/opt/lib`nexport PATH LD_LIBRARY_PATH`nexec pwsh" }
        @{ Case = 'an absolute exec path'; Content = "#!/bin/sh`nexec /opt/powershell/pwsh -File /opt/runtime.ps1" }
        @{ Case = 'a quoted absolute exec path'; Content = "#!/bin/sh`nexec `"/opt/powershell/pwsh`" -File /opt/runtime.ps1" }
        @{ Case = 'an absolute shebang'; Content = "#!/var/runtime/powershell/pwsh -noprofile`n. /var/runtime/runtime.ps1" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $findings = @(Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))))
            @($findings.RuleId) | Should -Be @('R-Env-Path', 'R-Env-LdLibraryPath')
            $findings | ForEach-Object { $_.Passed | Should -BeTrue -Because $_.Message }
        }
    }

    It 'fails both rules for <Case>' -ForEach @(
        @{ Case = 'a bare exec pwsh'; Content = "#!/bin/sh`nexec pwsh" }
        @{ Case = 'an export only in a comment'; Content = "#!/bin/sh`n# export PATH=/opt/powershell`nexec pwsh" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $findings = @(Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))))
            $findings | ForEach-Object { $_.Passed | Should -BeFalse -Because $_.RuleId }
        }
    }

    It 'says which branch passed R-Env-Path' {
        InModuleScope $script:moduleName {
            (Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes("export PATH=/opt/bin`nexec pwsh"))))[0].Message |
                Should -Be 'Bootstrap exports PATH.'
            (Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes('exec /opt/powershell/pwsh'))))[0].Message |
                Should -BeLike '*invokes pwsh by absolute path*'
        }
    }

    It 'fails both rules for <Case>' -ForEach @(
        @{ Case = 'lower-case names, which the shell does not read as PATH'; Content = "#!/bin/sh`nexport path=/opt/powershell`nexport ld_library_path=/opt/lib`nexec pwsh" }
        @{ Case = 'exports after exec, which never run'; Content = "#!/bin/sh`nexec pwsh`nexport PATH=/opt/powershell`nexport LD_LIBRARY_PATH=/opt/lib" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $findings = @(Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))))
            $findings | ForEach-Object { $_.Passed | Should -BeFalse -Because $_.RuleId }
        }
    }

    It 'passes both rules for <Case>' -ForEach @(
        @{ Case = 'PATH and LD_LIBRARY_PATH set through exec env'; Content = "#!/bin/sh`nexec env PATH=/opt/powershell LD_LIBRARY_PATH=/opt/lib pwsh" }
        @{ Case = 'an export before the assignment'; Content = "#!/bin/sh`nexport PATH LD_LIBRARY_PATH`nPATH=/opt/powershell`nLD_LIBRARY_PATH=/opt/lib`nexec pwsh" }
        @{ Case = 'exports and exec on one line'; Content = "#!/bin/sh`nexport PATH=/opt/powershell LD_LIBRARY_PATH=/opt/lib; exec pwsh" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $findings = @(Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))))
            $findings | ForEach-Object { $_.Passed | Should -BeTrue -Because $_.Message }
        }
    }

    # Neither a pass nor a failure: these shapes are outside what the rule can judge.
    It 'reports both rules as unverified for <Case>' -ForEach @(
        @{ Case = 'an assignment that is never exported'; Content = "#!/bin/sh`nPATH=/opt/powershell`nLD_LIBRARY_PATH=/opt/lib`nexec pwsh" }
        @{ Case = 'an export line that names other variables'; Content = "#!/bin/sh`nPATH=/opt/powershell`nLD_LIBRARY_PATH=/opt/lib`nexport FOO`nexec pwsh" }
        @{ Case = 'an upper-case EXEC, which is not the exec builtin'; Content = "#!/bin/sh`nEXEC /var/runtime/powershell/pwsh" }
        @{ Case = 'pwsh as the shebang interpreter'; Content = "#!/opt/powershell/pwsh -NoProfile -File /opt/runtime.ps1" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            $findings = @(Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))))
            $findings.Count | Should -Be 2
            $findings | ForEach-Object { $_.Unverified | Should -BeTrue -Because $_.RuleId }
        }
    }

    It 'still fails both rules for a bare pwsh with no PATH or LD_LIBRARY_PATH at all' {
        InModuleScope $script:moduleName {
            $findings = @(Test-LambdaPSBootstrapEnvironment -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`nexec pwsh"))))
            $findings | ForEach-Object { $_.Passed | Should -BeFalse -Because $_.RuleId }
        }
    }
}
