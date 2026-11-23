BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBootstrapReservedPath' -Tag 'Unit' {
    It 'fails R-ReservedWrite for <Content>' -ForEach @(
        @{ Content = 'echo x > /var/runtime/state'; Offender = '/var/runtime' }
        @{ Content = 'echo x >> /var/task/log'; Offender = '/var/task' }
        @{ Content = 'echo x >/opt/cache'; Offender = '/opt' }
        @{ Content = 'echo x > /opt'; Offender = '/opt' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = "#!/bin/sh`n$Content" ; Offender = $Offender } {
            param($Content, $Offender)
            $finding = Test-LambdaPSBootstrapReservedPath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content)))
            $finding.RuleId | Should -Be 'R-ReservedWrite'
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike "*: $Offender. *"
        }
    }

    It 'passes for <Case>' -ForEach @(
        @{ Case = 'a write to /tmp'; Content = 'echo x > /tmp/state' }
        @{ Case = 'a path that only starts like /opt'; Content = 'echo x > /optional/state' }
        @{ Case = 'a read from a reserved path'; Content = 'cat /var/runtime/runtime.ps1 < /opt/input' }
        @{ Case = 'a redirect shown in a comment'; Content = '# Do not do this: echo x > /opt/cache' }
        @{ Case = 'an indented comment'; Content = '    # echo x >> /var/task/log' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = "#!/bin/sh`n$Content`nexec /opt/powershell/pwsh" } {
            param($Content)
            (Test-LambdaPSBootstrapReservedPath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content)))).Passed | Should -BeTrue
        }
    }

    It 'fails R-ReservedWrite for <Case>' -ForEach @(
        @{ Case = 'a quoted redirect target'; Content = 'echo x > "/var/runtime/log"' }
        @{ Case = 'a single-quoted append'; Content = "echo x >> '/opt/log'" }
        @{ Case = 'tee'; Content = 'echo x | tee /var/task/log' }
        @{ Case = 'tee -a after other files'; Content = 'echo x | tee -a /tmp/log /opt/log' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = "#!/bin/sh`n$Content`nexec /opt/powershell/pwsh" } {
            param($Content)
            (Test-LambdaPSBootstrapReservedPath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content)))).Passed | Should -BeFalse
        }
    }

    It 'passes for <Case>' -ForEach @(
        @{ Case = 'a trailing comment that shows a redirect'; Content = 'echo ok # never: echo x > /opt/log' }
        @{ Case = 'a redirect into a path that differs only in case'; Content = 'echo x > /OPT/log' }
        @{ Case = 'tee into /tmp'; Content = 'echo x | tee /tmp/log' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = "#!/bin/sh`n$Content`nexec /opt/powershell/pwsh" } {
            param($Content)
            (Test-LambdaPSBootstrapReservedPath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content)))).Passed | Should -BeTrue
        }
    }
}
