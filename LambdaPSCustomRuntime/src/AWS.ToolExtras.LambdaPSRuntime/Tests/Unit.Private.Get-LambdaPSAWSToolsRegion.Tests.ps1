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

}

# The SDK reads the profile store once per process, so -ProfileLocation is checked in a child pwsh
# whose default credential files are empty and that has no shell default.
Describe 'Get-LambdaPSAWSToolsRegion -ProfileLocation' -Tag 'Unit' {
    BeforeAll {
        . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
        $script:emptyConfig = Join-Path $TestDrive 'empty-config'
        Set-Content -LiteralPath $script:emptyConfig -Value ''
        $script:located = Join-Path $TestDrive 'located-credentials'
        Set-Content -LiteralPath $script:located -Value "[default]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example`nregion = ap-northeast-1"

        function Invoke-RegionInChild {
            param([string]$ArgumentText)
            $command = @"
foreach (`$name in 'AWS_REGION', 'AWS_DEFAULT_REGION', 'AWS_PROFILE') { [Environment]::SetEnvironmentVariable(`$name, `$null) }
`$env:AWS_CONFIG_FILE = '$script:emptyConfig'
`$env:AWS_SHARED_CREDENTIALS_FILE = '$script:emptyConfig'
`$env:AWS_EC2_METADATA_DISABLED = 'true'
'REGION:[' + (& (Get-Module AWS.ToolExtras.LambdaPSRuntime) { Get-LambdaPSAWSToolsRegion $ArgumentText }) + ']'
"@
            (Invoke-InChildPwsh -Command $command).Output
        }
    }

    It 'reads the default profile''s region from -ProfileLocation' {
        Invoke-RegionInChild -ArgumentText "-ProfileLocation '$script:located'" | Should -BeLike '*REGION:`[ap-northeast-1`]*'
    }

    It 'returns nothing without -ProfileLocation when the default files hold no region' {
        Invoke-RegionInChild -ArgumentText '' | Should -BeLike '*REGION:`[`]*'
    }
}
