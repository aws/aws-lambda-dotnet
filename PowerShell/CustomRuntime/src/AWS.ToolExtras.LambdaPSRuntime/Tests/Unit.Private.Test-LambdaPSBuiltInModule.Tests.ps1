BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSBuiltInModule' -Tag 'Unit' {
    It 'returns $true for PowerShell''s own module <Name>' -ForEach @(
        @{ Name = 'Microsoft.PowerShell.Core' }, @{ Name = 'Microsoft.PowerShell.Diagnostics' }, @{ Name = 'Microsoft.PowerShell.Host' }
        @{ Name = 'Microsoft.PowerShell.Management' }, @{ Name = 'Microsoft.PowerShell.Security' }, @{ Name = 'Microsoft.PowerShell.Utility' }
        @{ Name = 'CimCmdlets' }, @{ Name = 'Microsoft.WSMan.Management' }, @{ Name = 'PSDiagnostics' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Name = $Name } {
            param($Name)
            Test-LambdaPSBuiltInModule -Name $Name | Should -BeTrue
        }
    }

    It 'ignores case' {
        InModuleScope $script:moduleName {
            Test-LambdaPSBuiltInModule -Name 'microsoft.powershell.utility' | Should -BeTrue
        }
    }

    It 'returns $false for <Name>, which ships with PowerShell but is updated on its own, or is not PowerShell''s' -ForEach @(
        @{ Name = 'PSReadLine' }, @{ Name = 'Microsoft.PowerShell.ThreadJob' }, @{ Name = 'Microsoft.PowerShell.PSResourceGet' }
        @{ Name = 'PowerShellGet' }, @{ Name = 'PackageManagement' }, @{ Name = 'Microsoft.PowerShell.Archive' }
        @{ Name = 'AWS.Tools.Common' }, @{ Name = 'Microsoft.PowerShell.Utility.Extra' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Name = $Name } {
            param($Name)
            Test-LambdaPSBuiltInModule -Name $Name | Should -BeFalse
        }
    }
}
