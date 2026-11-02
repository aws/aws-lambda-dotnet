BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBootstrapGlobalization' -Tag 'Unit' {
    It 'passes for <Target> when the bootstrap enables invariant mode with <Case>' -ForEach @(
        foreach ($target in 'Layer', 'SelfContainedZip') {
            @{ Target = $target; Case = 'export =1'; Content = "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'export =true'; Content = "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'a double-quoted value'; Content = "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=`"1`"`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'a single-quoted value'; Content = "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT='true'`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'an assignment then an export'; Content = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'a command prefix'; Content = 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 exec /opt/powershell/pwsh' }
        }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content; Target = $Target } {
            param($Content, $Target)
            $finding = Test-LambdaPSBootstrapGlobalization -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target $Target
            $finding.RuleId | Should -Be 'R-Globalization'
            $finding.Passed | Should -BeTrue -Because $finding.Message
        }
    }

    It 'fails for <Target> with <Case>' -ForEach @(
        foreach ($target in 'Layer', 'SelfContainedZip') {
            @{ Target = $target; Case = 'no flag'; Content = 'exec /opt/powershell/pwsh' }
            @{ Target = $target; Case = 'the flag set to 0'; Content = "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'the flag set to 10'; Content = "export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=10`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'an assignment that is never exported'; Content = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec /opt/powershell/pwsh" }
            @{ Target = $target; Case = 'the export only in a comment'; Content = "# export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec /opt/powershell/pwsh" }
        }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content; Target = $Target } {
            param($Content, $Target)
            $finding = Test-LambdaPSBootstrapGlobalization -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target $Target
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike 'Bootstrap does not set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT for pwsh (export it, or set it on the pwsh command).*'
        }
    }

    It 'reports not applicable (passing) for <Target>' -ForEach @(@{ Target = 'CMR' }, @{ Target = 'Container' }) {
        InModuleScope $script:moduleName -Parameters @{ Target = $Target } {
            param($Target)
            $finding = Test-LambdaPSBootstrapGlobalization -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes('exec pwsh'))) -Target $Target
            $finding.Passed | Should -BeTrue
            $finding.Message | Should -BeLike 'Not applicable:*'
        }
    }

    It 'fails for Layer with <Case>' -ForEach @(
        @{ Case = 'the export after exec, which never runs'; Content = "#!/bin/sh`nexec /opt/powershell/pwsh`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1" }
        @{ Case = 'a later override to 0'; Content = "#!/bin/sh`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0`nexec /opt/powershell/pwsh" }
        @{ Case = 'a lower-case name, which .NET does not read'; Content = "#!/bin/sh`nexport dotnet_system_globalization_invariant=1`nexec /opt/powershell/pwsh" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            (Test-LambdaPSBootstrapGlobalization -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target Layer).Passed | Should -BeFalse
        }
    }

    It 'passes for Layer with <Case>' -ForEach @(
        @{ Case = 'exec env'; Content = "#!/bin/sh`nexec env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /opt/powershell/pwsh" }
        @{ Case = 'several names on one export'; Content = "#!/bin/sh`nexport A=1 DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec /opt/powershell/pwsh" }
        @{ Case = 'an export before the assignment'; Content = "#!/bin/sh`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`nDOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec /opt/powershell/pwsh" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Content = $Content } {
            param($Content)
            (Test-LambdaPSBootstrapGlobalization -Bootstrap (Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Content))) -Target Layer).Passed | Should -BeTrue
        }
    }
}
