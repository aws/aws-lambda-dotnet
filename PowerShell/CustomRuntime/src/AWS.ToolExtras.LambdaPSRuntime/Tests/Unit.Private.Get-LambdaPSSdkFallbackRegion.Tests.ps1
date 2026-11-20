BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# AWS_REGION is the SDK chain's first source, ahead of the profile store this process may have
# cached, so this result does not depend on the machine's credential files.
Describe 'Get-LambdaPSSdkFallbackRegion' -Tag 'Unit' {
    BeforeAll {
        $script:savedRegion = $env:AWS_REGION
    }

    AfterAll {
        $env:AWS_REGION = $script:savedRegion
        ('Amazon.Runtime.FallbackRegionFactory' -as [type])::Reset()
    }

    It 'returns the region of AWS_REGION' {
        $env:AWS_REGION = 'us-west-2'
        ('Amazon.Runtime.FallbackRegionFactory' -as [type])::Reset()
        InModuleScope $script:moduleName {
            Get-LambdaPSSdkFallbackRegion | Should -Be 'us-west-2'
        }
    }
}
