BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSModulePinError' -Tag 'Unit' {
    It 'returns nothing for an exact RequiredVersion pin' {
        InModuleScope $script:moduleName {
            Get-LambdaPSModulePinError -Specification @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } | Should -BeNullOrEmpty
        }
    }

    It 'returns ModuleVersionRequired for <Case>' -ForEach @(
        @{ Case = 'a bare name'; Spec = 'AWS.Tools.S3' }
        @{ Case = 'a minimum version'; Spec = @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.274' } }
        @{ Case = 'a version range'; Spec = @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.0'; MaximumVersion = '5.9.0' } }
        @{ Case = 'a maximum only'; Spec = @{ ModuleName = 'AWS.Tools.S3'; MaximumVersion = '5.9.0' } }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Spec = $Spec } {
            param($Spec)
            $err = Get-LambdaPSModulePinError -Specification $Spec
            $err.FullyQualifiedErrorId | Should -Be 'ModuleVersionRequired'
            $err.CategoryInfo.Category | Should -Be 'InvalidArgument'
            $err.TargetObject.Name | Should -Be 'AWS.Tools.S3'
            $err.Exception.Message | Should -BeLike "Module 'AWS.Tools.S3' must pin an exact RequiredVersion (for example @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '1.0.0' }). *"
        }
    }

    It 'names the script and shows the #Requires form with -DeclaredIn' {
        InModuleScope $script:moduleName {
            $err = Get-LambdaPSModulePinError -Specification 'AWS.Tools.S3' -DeclaredIn '/src/handler.ps1'
            $err.Exception.Message | Should -BeLike "Module 'AWS.Tools.S3', declared with #Requires -Modules in '/src/handler.ps1', must pin an exact RequiredVersion (for example #Requires -Modules @{ *"
        }
    }
}
