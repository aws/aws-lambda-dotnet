BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
    $script:defaultCases = @($script:pwshConfig.DefaultPatch.GetEnumerator() | ForEach-Object { @{ Line = $_.Key; Version = $_.Value } })
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Resolve-LambdaPSPwshVersion' -Tag 'Unit' {
    It 'maps PowerShell <Line> to its default pwsh <Version>' -ForEach $script:defaultCases {
        InModuleScope $script:moduleName -Parameters $_ {
            param($Line, $Version)
            Resolve-LambdaPSPwshVersion -PowerShellVersion $Line | Should -Be $Version
        }
    }

    It 'returns an explicit -PwshVersion verbatim, even for an unmapped line' {
        InModuleScope $script:moduleName {
            Resolve-LambdaPSPwshVersion -PowerShellVersion '7.9' -PwshVersion '7.9.1' | Should -Be '7.9.1'
        }
    }

    It 'throws PwshVersionMismatch for a -PwshVersion on another major.minor' {
        InModuleScope $script:moduleName {
            { Resolve-LambdaPSPwshVersion -PowerShellVersion '7.4' -PwshVersion '7.6.6' } |
                Should -Throw -ErrorId 'PwshVersionMismatch' -ExpectedMessage "*'7.6.6' is not a PowerShell 7.4 release*"
            # 7.40.1 shares the prefix '7.4' but not the line.
            { Resolve-LambdaPSPwshVersion -PowerShellVersion '7.4' -PwshVersion '7.40.1' } |
                Should -Throw -ErrorId 'PwshVersionMismatch'
        }
    }

    It 'throws UnknownPwshVersion for an unmapped line with no -PwshVersion, naming -PwshVersion' {
        InModuleScope $script:moduleName {
            { Resolve-LambdaPSPwshVersion -PowerShellVersion '7.9' } |
                Should -Throw -ErrorId 'UnknownPwshVersion' -ExpectedMessage '*Pass -PwshVersion*'
        }
    }
}

Describe 'Resolve-LambdaPSPwshVersion parameter validation' -Tag 'Unit' {
    It 'rejects a -PwshVersion that is not x.y.z: <Value>' -ForEach @(
        @{ Value = '7.4.x/../../evil' }, @{ Value = '7.4.20-rc.1' }, @{ Value = "7.4.20`n" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Value = $Value } {
            param($Value)
            { Resolve-LambdaPSPwshVersion -PowerShellVersion '7.4' -PwshVersion $Value } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Resolve-LambdaPSPwshVersion'
        }
    }

    It 'takes an empty -PwshVersion as not given' {
        InModuleScope $script:moduleName {
            Resolve-LambdaPSPwshVersion -PowerShellVersion '7.4' -PwshVersion '' | Should -Be $script:Config.pwsh.DefaultPatch['7.4']
        }
    }

    It 'rejects a -PowerShellVersion with a trailing newline' {
        InModuleScope $script:moduleName {
            { Resolve-LambdaPSPwshVersion -PowerShellVersion "7.4`n" } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Resolve-LambdaPSPwshVersion'
        }
    }

    It 'rejects a -PowerShellVersion in non-ASCII digits' {
        InModuleScope $script:moduleName {
            { Resolve-LambdaPSPwshVersion -PowerShellVersion "$([char]0x0667).$([char]0x0664)" } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Resolve-LambdaPSPwshVersion'
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Resolve-LambdaPSPwshVersion raised through a calling cmdlet' -Tag 'Unit' {
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'PwshVersionMismatch'; Call = { Resolve-LambdaPSPwshVersion -PowerShellVersion '7.4' -PwshVersion '7.6.6' } }
        @{ ErrorId = 'UnknownPwshVersion'; Call = { Resolve-LambdaPSPwshVersion -PowerShellVersion '7.99' } }
    ) {
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString() } {
            param($ErrorId, $Call)
            # Rebuilt here, so the call runs in the module and reaches its private helper.
            $Call = [scriptblock]::Create($Call)
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call $Call -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Resolve-LambdaPSPwshVersion' }) | Should -BeNullOrEmpty
        }
    }
}
