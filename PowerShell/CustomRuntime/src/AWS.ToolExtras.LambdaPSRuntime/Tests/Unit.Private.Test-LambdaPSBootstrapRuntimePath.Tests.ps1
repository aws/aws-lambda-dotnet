BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBootstrapRuntimePath' -Tag 'Unit' {
    It 'passes <Target> for a bootstrap that references <Path>' -ForEach @(
        @{ Target = 'CMR'; Path = '/var/runtime' }
        @{ Target = 'Container'; Path = '/var/runtime' }
        @{ Target = 'Layer'; Path = '/opt' }
        @{ Target = 'SelfContainedZip'; Path = '/var/task' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Content = "#!/bin/sh`nexec $Path/powershell/pwsh" } {
            param($Target, $Content)
            $finding = Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target $Target
            $finding.RuleId | Should -Be 'R-RuntimePath'
            $finding.Passed | Should -BeTrue -Because $finding.Message
        }
    }

    It 'fails <Target> with the Missing message when the bootstrap never references <Path>' -ForEach @(
        @{ Target = 'CMR'; Path = '/var/runtime' }
        @{ Target = 'Container'; Path = '/var/runtime' }
        @{ Target = 'Layer'; Path = '/opt' }
        @{ Target = 'SelfContainedZip'; Path = '/var/task' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Path = $Path } {
            param($Target, $Path)
            $finding = Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`nexec /usr/bin/pwsh"))) -Target $Target
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike "Bootstrap does not reference $Path.*"
        }
    }

    It 'fails <Target> with the Forbidden message when the bootstrap references /var/runtime' -ForEach @(
        @{ Target = 'Layer'; Path = '/opt' }
        @{ Target = 'SelfContainedZip'; Path = '/var/task' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Content = "#!/bin/sh`ncd /var/runtime`nexec $Path/powershell/pwsh" } {
            param($Target, $Content)
            $finding = Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target $Target
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike 'Bootstrap references /var/runtime, which is the CMR runtime-image path.*'
        }
    }

    It 'ignores a path that is named only in a comment' {
        InModuleScope $script:moduleName {
            $content = "#!/bin/sh`n# Not /var/runtime: a layer mounts at /opt.`nexec /opt/powershell/pwsh"
            (Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($content))) -Target Layer).Passed | Should -BeTrue
        }
    }

    It 'ignores a path named only in a trailing comment' {
        InModuleScope $script:moduleName {
            $content = "#!/bin/sh`nexec /opt/powershell/pwsh -File /opt/runtime.ps1 # not /var/runtime"
            (Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($content))) -Target Layer).Passed | Should -BeTrue
        }
    }

    It 'matches paths case-sensitively and whole (<Case>)' -ForEach @(
        @{ Case = '/VAR/RUNTIME is not /var/runtime'; Content = "#!/bin/sh`nexec /VAR/RUNTIME/powershell/pwsh"; Target = 'CMR' }
        @{ Case = '/optional is not /opt'; Content = "#!/bin/sh`nexec /optional/powershell/pwsh"; Target = 'Layer' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content; Target = $Target } {
            param($Content, $Target)
            $finding = Test-LambdaPSBootstrapRuntimePath -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target $Target
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike 'Bootstrap does not reference*'
        }
    }
}
