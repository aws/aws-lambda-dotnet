BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Resolve-LambdaPSRegion' -Tag 'Unit' {
    BeforeAll {
        # These tests run in this process: each step that reads the profile store is mocked, given its
        # own file, or asked for a profile no store holds, so what the process has cached cannot change
        # the result. Region variables are cleared, and the SDK pointed at empty config files with no
        # instance metadata, all the same.
        $script:savedRegionEnvironment = @{}
        foreach ($name in 'AWS_REGION', 'AWS_DEFAULT_REGION', 'AWS_PROFILE', 'AWS_CONFIG_FILE', 'AWS_SHARED_CREDENTIALS_FILE', 'AWS_EC2_METADATA_DISABLED') {
            $script:savedRegionEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $script:emptyConfig = Join-Path $TestDrive 'aws-config'
        Set-Content -LiteralPath $script:emptyConfig -Value ''
    }

    BeforeEach {
        foreach ($name in 'AWS_REGION', 'AWS_DEFAULT_REGION', 'AWS_PROFILE') { [Environment]::SetEnvironmentVariable($name, $null) }
        $env:AWS_CONFIG_FILE = $script:emptyConfig
        $env:AWS_SHARED_CREDENTIALS_FILE = $script:emptyConfig
        $env:AWS_EC2_METADATA_DISABLED = 'true'
        ('Amazon.Runtime.FallbackRegionFactory' -as [type])::Reset()
    }

    AfterAll {
        foreach ($name in $script:savedRegionEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $script:savedRegionEnvironment[$name])
        }
        ('Amazon.Runtime.FallbackRegionFactory' -as [type])::Reset()
    }

    It 'uses the shell default region first' {
        InModuleScope $script:moduleName {
            Mock 'Get-DefaultAWSRegion' -MockWith { [PSCustomObject]@{ Region = 'ap-southeast-2' } }
            $env:AWS_REGION = 'us-west-2'
            Resolve-LambdaPSRegion | Should -Be 'ap-southeast-2'
        }
    }

    It 'uses the region of the -ProfileName profile, over AWS.Tools'' resolver and the SDK chain' {
        $config = Join-Path $TestDrive 'profile-config'
        Set-Content -LiteralPath $config -Value "[deploy]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example`nregion = eu-central-1"
        InModuleScope $script:moduleName -Parameters @{ config = $config } {
            param($config)
            Mock 'Get-DefaultAWSRegion' -MockWith { }
            Mock 'Get-LambdaPSAWSToolsRegion' -MockWith { 'ap-northeast-1' }
            Mock 'Get-LambdaPSSdkFallbackRegion' -MockWith { 'us-west-2' }
            Resolve-LambdaPSRegion -CredentialParameter @{ ProfileName = 'deploy'; ProfileLocation = $config } | Should -Be 'eu-central-1'
        }
    }

    # The two store-reading steps are mocked here, so these tests do not depend on what this
    # process has cached; the clean-process Describe below runs them for real.
    It 'uses AWS.Tools'' resolver before the SDK chain' {
        InModuleScope $script:moduleName {
            Mock 'Get-DefaultAWSRegion' -MockWith { }
            Mock 'Get-LambdaPSAWSToolsRegion' -MockWith { 'eu-central-1' }
            Mock 'Get-LambdaPSSdkFallbackRegion' -MockWith { 'us-west-2' }

            Resolve-LambdaPSRegion | Should -Be 'eu-central-1'
            Should -Invoke 'Get-LambdaPSSdkFallbackRegion' -Times 0 -Exactly
        }
    }

    It 'passes -ProfileLocation to AWS.Tools'' resolver' {
        InModuleScope $script:moduleName {
            Mock 'Get-DefaultAWSRegion' -MockWith { }
            Mock 'Get-LambdaPSAWSToolsRegion' -MockWith { 'eu-central-1' }

            Resolve-LambdaPSRegion -CredentialParameter @{ ProfileLocation = '/work/credentials' } | Should -Be 'eu-central-1'
            Should -Invoke 'Get-LambdaPSAWSToolsRegion' -Times 1 -Exactly -ParameterFilter { $ProfileLocation -eq '/work/credentials' }
        }
    }

    # The named profile is looked up in the default store, where no profile has this name.
    It 'falls through to the SDK chain when neither profile has a region' {
        InModuleScope $script:moduleName {
            Mock 'Get-DefaultAWSRegion' -MockWith { }
            Mock 'Get-LambdaPSAWSToolsRegion' -MockWith { }
            Mock 'Get-LambdaPSSdkFallbackRegion' -MockWith { 'us-west-2' }

            Resolve-LambdaPSRegion -CredentialParameter @{ ProfileName = 'lambdaps-test-no-such-profile' } | Should -Be 'us-west-2'
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces RegionNotResolved from the calling cmdlet, and names no internal helper in -ErrorVariable' {
        InModuleScope $script:moduleName {
            Mock 'Get-DefaultAWSRegion' -MockWith { }
            Mock 'Get-LambdaPSAWSToolsRegion' -MockWith { }
            Mock 'Get-LambdaPSSdkFallbackRegion' -MockWith { }
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call { Resolve-LambdaPSRegion } -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RegionNotResolved,Invoke-LambdaPSTestCaller'
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain 'RegionNotResolved,Invoke-LambdaPSTestCaller'
            @($ids | Where-Object { $_ -like '*,Resolve-LambdaPSRegion' }) | Should -BeNullOrEmpty
        }
    }

    It 'throws RegionNotResolved when no step finds a region' {
        InModuleScope $script:moduleName {
            Mock 'Get-DefaultAWSRegion' -MockWith { }
            Mock 'Get-LambdaPSAWSToolsRegion' -MockWith { }
            Mock 'Get-LambdaPSSdkFallbackRegion' -MockWith { }

            { Resolve-LambdaPSRegion } | Should -Throw -ErrorId 'RegionNotResolved'
        }
    }
}

# The SDK reads the profile store once per process and caches it, so every test that depends on
# what the store holds (or lacks) runs in a child pwsh whose store files are the test's own:
# pointing AWS_CONFIG_FILE somewhere new afterwards has no effect, even after
# FallbackRegionFactory::Reset().
Describe 'Resolve-LambdaPSRegion in a clean process' -Tag 'Unit' {
    BeforeAll {
        # Resolves the region in a child pwsh that reads $Config as its only credential files.
        function Invoke-ResolveInChild {
            param(
                [Parameter(Mandatory)] [string]$Config,
                [hashtable]$Environment = @{},
                [string]$ParameterText = '@{}'
            )
            $setEnvironment = ($Environment.GetEnumerator() | ForEach-Object { "`$env:$($_.Key) = '$($_.Value)'" }) -join "`n"
            $command = @"
foreach (`$name in 'AWS_REGION', 'AWS_DEFAULT_REGION', 'AWS_PROFILE') { [Environment]::SetEnvironmentVariable(`$name, `$null) }
`$env:AWS_CONFIG_FILE = '$Config'
`$env:AWS_SHARED_CREDENTIALS_FILE = '$Config'
`$env:AWS_EC2_METADATA_DISABLED = 'true'
$setEnvironment
Clear-AWSDefaultConfiguration -SkipProfileStore
'REGION:' + (& (Get-Module AWS.ToolExtras.LambdaPSRuntime) { Resolve-LambdaPSRegion -CredentialParameter $ParameterText })
"@
            (Invoke-InChildPwsh -Command $command).Output
        }

        $script:emptyConfig = Join-Path $TestDrive 'empty-config'
        Set-Content -LiteralPath $script:emptyConfig -Value ''
    }

    It 'uses AWS_REGION when nothing earlier supplies a region' {
        Invoke-ResolveInChild -Config $script:emptyConfig -Environment @{ AWS_REGION = 'us-west-2' } |
            Should -BeLike '*REGION:us-west-2*'
    }

    It 'falls through to AWS_REGION when the named profile has no region' {
        $config = Join-Path $TestDrive 'profile-no-region'
        Set-Content -LiteralPath $config -Value "[plain]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"

        Invoke-ResolveInChild -Config $script:emptyConfig -Environment @{ AWS_REGION = 'us-west-2' } -ParameterText "@{ ProfileName = 'plain'; ProfileLocation = '$config' }" |
            Should -BeLike '*REGION:us-west-2*'
    }

    It 'uses the region of the AWS_PROFILE profile' {
        $config = Join-Path $TestDrive 'env-profile-config'
        Set-Content -LiteralPath $config -Value "[envprofile]`nregion = sa-east-1"

        Invoke-ResolveInChild -Config $config -Environment @{ AWS_PROFILE = 'envprofile' } |
            Should -BeLike '*REGION:sa-east-1*'
    }

    It 'uses the default profile region over AWS_REGION, as AWS.Tools does' {
        $config = Join-Path $TestDrive 'default-profile-config'
        Set-Content -LiteralPath $config -Value "[default]`nregion = eu-central-1"

        Invoke-ResolveInChild -Config $config -Environment @{ AWS_REGION = 'us-west-2' } |
            Should -BeLike '*REGION:eu-central-1*'
    }

    It 'reads the default profile from -ProfileLocation' {
        $config = Join-Path $TestDrive 'located-default-config'
        Set-Content -LiteralPath $config -Value "[default]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example`nregion = ap-northeast-1"

        Invoke-ResolveInChild -Config $script:emptyConfig -Environment @{ AWS_REGION = 'us-west-2' } -ParameterText "@{ ProfileLocation = '$config' }" |
            Should -BeLike '*REGION:ap-northeast-1*'
    }

    # AWS_DEFAULT_REGION is not a region source: neither AWS.Tools nor the SDK reads it.
    It 'throws RegionNotResolved when only AWS_DEFAULT_REGION is set' {
        Invoke-ResolveInChild -Config $script:emptyConfig -Environment @{ AWS_DEFAULT_REGION = 'ap-south-1' } |
            Should -BeLike '*ERROR: RegionNotResolved*'
    }

    It 'throws RegionNotResolved when nothing supplies a region' {
        Invoke-ResolveInChild -Config $script:emptyConfig |
            Should -BeLike '*ERROR: RegionNotResolved*'
    }
}

# Set-DefaultAWSRegion without -Scope sets the region in the scope it runs in, such as a deploy script,
# where the module's own code cannot see it. A public cmdlet passes its caller's session state, so the
# region a script set is found. Each test runs in a child pwsh, which has no default region of its own.
Describe 'Resolve-LambdaPSRegion with the caller''s session state' -Tag 'Unit' {
    BeforeAll {
        # Defines Invoke-LambdaPSTestDeploy, a module cmdlet that resolves the region as $ResolveText does,
        # and runs $ScriptText as a script file that calls it.
        function Invoke-DeployScriptInChild {
            param(
                [Parameter(Mandatory)] [string]$ResolveText,
                [Parameter(Mandatory)] [string]$ScriptText,
                [hashtable]$Environment = @{}
            )
            $deployScript = Join-Path $TestDrive "deploy-$([guid]::NewGuid().ToString('n')).ps1"
            Set-Content -LiteralPath $deployScript -Value $ScriptText
            $setEnvironment = ($Environment.GetEnumerator() | ForEach-Object { "`$env:$($_.Key) = '$($_.Value)'" }) -join "`n"
            $command = @"
foreach (`$name in 'AWS_REGION', 'AWS_DEFAULT_REGION', 'AWS_PROFILE') { [Environment]::SetEnvironmentVariable(`$name, `$null) }
`$env:AWS_CONFIG_FILE = '$script:emptyConfig'
`$env:AWS_SHARED_CREDENTIALS_FILE = '$script:emptyConfig'
`$env:AWS_EC2_METADATA_DISABLED = 'true'
$setEnvironment
& (Get-Module AWS.ToolExtras.LambdaPSRuntime) {
    function Invoke-LambdaPSTestDeploy {
        [CmdletBinding()]
        param()
        $ResolveText
    }
    Set-Item -Path 'function:global:Invoke-LambdaPSTestDeploy' -Value `${function:Invoke-LambdaPSTestDeploy}
}
& '$deployScript'
"@
            (Invoke-InChildPwsh -Command $command).Output
        }

        $script:emptyConfig = Join-Path $TestDrive 'caller-empty-config'
        Set-Content -LiteralPath $script:emptyConfig -Value ''
    }

    It 'uses the region a script set with Set-DefaultAWSRegion, over AWS_REGION' {
        Invoke-DeployScriptInChild -ResolveText 'Resolve-LambdaPSRegion -SessionState $PSCmdlet.SessionState' -Environment @{ AWS_REGION = 'us-west-2' } -ScriptText @'
Set-DefaultAWSRegion -Region ap-south-1
'REGION:' + (Invoke-LambdaPSTestDeploy)
'@ | Should -BeLike '*REGION:ap-south-1*'
    }

    It 'goes on to the next source when the caller set no region' {
        Invoke-DeployScriptInChild -ResolveText 'Resolve-LambdaPSRegion -SessionState $PSCmdlet.SessionState' -Environment @{ AWS_REGION = 'us-west-2' } -ScriptText @'
'REGION:' + (Invoke-LambdaPSTestDeploy)
'@ | Should -BeLike '*REGION:us-west-2*'
    }

    It 'uses a global Set-DefaultAWSRegion when no session state is passed' {
        Invoke-DeployScriptInChild -ResolveText 'Resolve-LambdaPSRegion' -Environment @{ AWS_REGION = 'us-west-2' } -ScriptText @'
Set-DefaultAWSRegion -Region ap-south-1 -Scope Global
'REGION:' + (Invoke-LambdaPSTestDeploy)
'@ | Should -BeLike '*REGION:ap-south-1*'
    }
}
