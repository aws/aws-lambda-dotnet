BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# The shell default is the resolver's first source, ahead of the profile store this process may have
# cached, so these results do not depend on the machine's credential files.
Describe 'Get-LambdaPSAWSToolsRegion' -Tag 'Unit' {
    BeforeAll {
        $script:savedShellRegion = (Get-DefaultAWSRegion).Region
    }

    AfterAll {
        if ($script:savedShellRegion) { Set-DefaultAWSRegion -Region $script:savedShellRegion -Scope Global }
    }

    BeforeEach {
        Set-DefaultAWSRegion -Region 'ap-southeast-2' -Scope Global
    }

    AfterEach {
        Clear-DefaultAWSRegion -Scope Global
    }

    It 'returns the region AWS.Tools resolves' {
        InModuleScope $script:moduleName {
            Get-LambdaPSAWSToolsRegion | Should -Be 'ap-southeast-2'
        }
    }

    # That the default profile is read from -ProfileLocation is tested in a clean process, in
    # Unit.Private.Resolve-LambdaPSRegion.Tests.ps1; the shell default wins here.
    It 'binds -ProfileLocation' {
        $config = Join-Path $TestDrive 'credentials'
        Set-Content -LiteralPath $config -Value ''
        InModuleScope $script:moduleName -Parameters @{ config = $config } {
            param($config)
            Get-LambdaPSAWSToolsRegion -ProfileLocation $config | Should -Be 'ap-southeast-2'
        }
    }
}
