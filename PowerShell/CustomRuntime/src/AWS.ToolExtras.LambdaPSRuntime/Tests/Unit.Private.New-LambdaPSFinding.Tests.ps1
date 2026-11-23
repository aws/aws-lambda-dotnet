BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSFinding' -Tag 'Unit' {
    It 'types the finding and derives Severity <Severity> from Passed <Passed>' -ForEach @(
        @{ Passed = $true; Severity = 'Information' }
        @{ Passed = $false; Severity = 'Error' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Passed = $Passed; Severity = $Severity } {
            param($Passed, $Severity)
            $finding = New-LambdaPSFinding -RuleId 'R-Test' -Passed $Passed -Message 'message'

            $finding.PSObject.TypeNames[0] | Should -Be 'AWS.ToolExtras.LambdaPSRuntime.Finding'
            @($finding.PSObject.Properties.Name) | Should -Be @('RuleId', 'Severity', 'Passed', 'Message', 'Path')
            $finding.RuleId | Should -Be 'R-Test'
            $finding.Passed | Should -Be $Passed
            $finding.Severity | Should -Be $Severity
            $finding.Message | Should -Be 'message'
            $null -eq $finding.Path | Should -BeTrue
        }
    }

    It 'records the project path when -Path is given' {
        InModuleScope $script:moduleName {
            $finding = New-LambdaPSFinding -RuleId 'R-Test' -Passed $true -Message 'message' -Path '/work/rt1'

            $finding.Path | Should -BeExactly '/work/rt1'
        }
    }
}
