BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
    $script:defaultCases = @($script:pwshConfig.DefaultPatch.GetEnumerator() | ForEach-Object { @{ Line = $_.Key; Version = $_.Value } })
    $script:hashCases = @(foreach ($entry in $script:pwshConfig.ArchiveSha256.GetEnumerator()) {
            @{ Version = $entry.Key; Architecture = 'x86_64'; Hash = $entry.Value.x64 }
            @{ Version = $entry.Key; Architecture = 'arm64'; Hash = $entry.Value.arm64 }
        })
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSPwshArchiveHash' -Tag 'Unit' {
    It 'pins both tarball hashes for the default pwsh <Version> of PowerShell <Line>' -ForEach $script:defaultCases {
        InModuleScope $script:moduleName -Parameters $_ {
            param($Version)
            Get-LambdaPSPwshArchiveHash -PwshVersion $Version -Architecture 'x86_64' | Should -Match '^[0-9a-f]{64}$'
            Get-LambdaPSPwshArchiveHash -PwshVersion $Version -Architecture 'arm64' | Should -Match '^[0-9a-f]{64}$'
        }
    }

    It 'returns the SHA256 pinned in Config/pwsh.psd1 for <Version> <Architecture>' -ForEach $script:hashCases {
        InModuleScope $script:moduleName -Parameters $_ {
            param($Version, $Architecture, $Hash)
            Get-LambdaPSPwshArchiveHash -PwshVersion $Version -Architecture $Architecture | Should -Be $Hash
        }
    }

    It 'returns $null for a version with no pinned hash' {
        InModuleScope $script:moduleName {
            Get-LambdaPSPwshArchiveHash -PwshVersion '9.9.9' -Architecture 'arm64' | Should -BeNullOrEmpty
        }
    }
}

Describe 'Get-LambdaPSPwshArchiveHash parameter validation' -Tag 'Unit' {
    It 'rejects a -PwshVersion with a trailing newline' {
        InModuleScope $script:moduleName {
            { Get-LambdaPSPwshArchiveHash -PwshVersion "7.4.20`n" -Architecture arm64 } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Get-LambdaPSPwshArchiveHash'
        }
    }

    It 'rejects a -PwshVersion in non-ASCII digits' {
        InModuleScope $script:moduleName {
            { Get-LambdaPSPwshArchiveHash -PwshVersion "7.4.$([char]0x0662)$([char]0x0660)" -Architecture arm64 } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Get-LambdaPSPwshArchiveHash'
        }
    }
}
