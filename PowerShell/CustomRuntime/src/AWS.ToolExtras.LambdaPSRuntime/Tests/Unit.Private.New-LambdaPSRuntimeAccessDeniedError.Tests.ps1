BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSRuntimeAccessDeniedError' -Tag 'Unit' {
    It 'returns a PermissionDenied RuntimeServiceAccessDenied record that targets the region' {
        InModuleScope $script:moduleName {
            $record = New-LambdaPSRuntimeAccessDeniedError -RuntimeName 'my-ps' -Region 'us-east-1'

            $record | Should -BeOfType ([System.Management.Automation.ErrorRecord])
            $record.FullyQualifiedErrorId | Should -Be 'RuntimeServiceAccessDenied'
            $record.CategoryInfo.Category | Should -Be ([System.Management.Automation.ErrorCategory]::PermissionDenied)
            $record.TargetObject | Should -Be 'us-east-1'
            $record.Exception | Should -BeOfType ([System.UnauthorizedAccessException])
        }
    }

    It 'names the runtime, the region and both causes' {
        InModuleScope $script:moduleName {
            $message = (New-LambdaPSRuntimeAccessDeniedError -RuntimeName 'my-ps' -Region 'us-east-1').Exception.Message

            $message | Should -BeLike "*GetRuntime for 'my-ps' in us-east-1*"
            $message | Should -BeLike '*may not be available in us-east-1*'
            $message | Should -BeLike '*lacks lambda:GetRuntime*'
            $message | Should -BeLike '*Nothing was created.'
        }
    }

    It 'keeps the service exception as the inner exception' {
        InModuleScope $script:moduleName {
            $serviceError = [System.InvalidOperationException]::new('Unable to determine service/operation name to be authorized')
            $record = New-LambdaPSRuntimeAccessDeniedError -RuntimeName 'my-ps' -Region 'us-east-1' -Exception $serviceError

            $record.Exception.InnerException | Should -Be $serviceError
        }
    }
}
