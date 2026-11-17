# A deploy or build that would fail must also fail under -WhatIf. These tests run the real scaffolder,
# packager and package builder (only AWS calls and the container check are mocked), so a check that
# runs only after a stage's ShouldProcess makes the preview succeed and the test fail.
BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSRuntimeDeployment -WhatIf' -Tag 'Component' {
    BeforeEach {
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'docker' } }
        Mock 'Get-STSCallerIdentity' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Account = '111122223333' } }
        Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = $RuntimeName } }
        Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
            throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')
        }
        Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime alias does not exist') }
        Mock 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -MockWith { }
        Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { $script:pwshConfig.DefaultPatch[$PowerShellVersion] }
        Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -MockWith { if ($ImageId) { throw [LambdaPSTests.Fakes.ImageNotFoundException]::new('The image does not exist') } }
        # A preview must not reach AWS: every AWS write the deploy makes throws if it is called.
        foreach ($command in 'New-LMCRuntime', 'New-LMCRuntimeVersion', 'New-LMCRuntimeAlias', 'Update-LMCRuntimeAlias',
            'New-ECRRepository', 'Set-ECRRepositoryPolicy', 'Get-ECRLoginCommand') {
            Mock $command -ModuleName $script:moduleName -MockWith { throw 'must not call AWS under -WhatIf' }
        }
        $script:projectPath = Join-Path $TestDrive "rt-$([guid]::NewGuid().ToString('N'))"
        $script:splat = @{ RuntimeName = 'rt'; PowerShellVersion = '7.4.*'; RuntimeAlias = 'default'; Region = 'us-east-1'; ProjectPath = $script:projectPath }
    }

    It 'previews a valid deploy without writing anything' {
        $null = New-LambdaPSRuntimeDeployment @script:splat -WhatIf
        Test-Path -LiteralPath $script:projectPath | Should -BeFalse
        foreach ($command in 'New-LMCRuntime', 'New-LMCRuntimeVersion', 'New-LMCRuntimeAlias', 'Update-LMCRuntimeAlias',
            'New-ECRRepository', 'Set-ECRRepositoryPolicy', 'Get-ECRLoginCommand') {
            Should -Invoke $command -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    It 'fails with <ErrorId> when <Case>' -ForEach @(
        @{ Case = 'the PowerShell version has no known pwsh build'; ErrorId = 'UnknownPwshVersion'; Extra = @{ PowerShellVersion = '7.9.*' } }
        @{ Case = '-IncludeModule is not pinned'; ErrorId = 'ModuleVersionRequired'; Extra = @{ IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.1' } } }
        @{ Case = 'a module is missing under -MissingModuleAction Stop'; ErrorId = 'ModuleNotFoundLocally'; Extra = @{ IncludeModule = @{ ModuleName = 'LambdaPSTests.NotInstalled'; RequiredVersion = '1.0.0' }; MissingModuleAction = 'Stop' } }
    ) {
        $splat = $script:splat.Clone()
        foreach ($key in $Extra.Keys) { $splat[$key] = $Extra[$key] }
        { New-LambdaPSRuntimeDeployment @splat -WhatIf } | Should -Throw -ErrorId "$ErrorId,New-LambdaPSRuntimeDeployment"
        Test-Path -LiteralPath $script:projectPath | Should -BeFalse
    }
}

Describe 'New-LambdaPSFunctionDeployment -WhatIf' -Tag 'Component' {
    BeforeEach {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' }
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64') }
        # A new function: the read of an existing one finds none.
        Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith { $null }
        # The writes. The preview still makes its read-only calls, such as the function's configuration.
        foreach ($command in 'Publish-LMFunction', 'Update-LMFunctionCode', 'Update-LMFunctionConfiguration') {
            Mock $command -ModuleName $script:moduleName -MockWith { throw 'must not change AWS under -WhatIf' }
        }
        $script:handler = Join-Path $TestDrive "handler-$([guid]::NewGuid().ToString('N')).ps1"
        Set-Content -LiteralPath $script:handler -Value 'param($LambdaInput, $LambdaContext)'
        $script:package = Join-Path $TestDrive "fn-$([guid]::NewGuid().ToString('N')).zip"
        $script:splat = @{
            FunctionName = 'hello'; ProjectPath = $script:handler; RuntimeName = 'rt'
            Role = 'arn:aws:iam::111122223333:role/hello-role'; Region = 'us-east-1'; PackagePath = $script:package
        }
    }

    It 'previews a valid deploy without writing the package' {
        # A pinned module that is installed, so a real run would stage it: ModulesStaged is empty below
        # only because this is a preview.
        $installed = Join-Path $TestDrive "whatif-mods-$([guid]::NewGuid().ToString('N'))"
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.WhatIf' -Version '1.0.0'
        $savedModulePath = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $result = New-LambdaPSFunctionDeployment @script:splat -IncludeModule @{ ModuleName = 'LambdaPSFake.WhatIf'; RequiredVersion = '1.0.0' } -WhatIf
        }
        finally { $env:PSModulePath = $savedModulePath }
        Test-Path -LiteralPath $script:package | Should -BeFalse
        # The result still comes back, with nothing deployed or packaged.
        $result.FunctionArn | Should -BeNullOrEmpty
        $result.Updated | Should -BeFalse
        @($result.ModulesStaged).Count | Should -Be 0
        foreach ($command in 'Publish-LMFunction', 'Update-LMFunctionCode', 'Update-LMFunctionConfiguration') {
            Should -Invoke $command -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    It 'fails with <ErrorId> when <Case>' -ForEach @(
        @{ Case = '-IncludeModule is not pinned'; ErrorId = 'ModuleVersionRequired'; Requires = $null; Extra = @{ IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.1' } } }
        @{ Case = 'the handler''s #Requires is not pinned'; ErrorId = 'ModuleVersionRequired'; Requires = '#Requires -Modules AWS.Tools.S3'; Extra = @{} }
        @{ Case = 'a module is missing under -MissingModuleAction Stop'; ErrorId = 'ModuleNotFoundLocally'; Requires = $null; Extra = @{ IncludeModule = @{ ModuleName = 'LambdaPSTests.NotInstalled'; RequiredVersion = '1.0.0' }; MissingModuleAction = 'Stop' } }
    ) {
        if ($Requires) { Set-Content -LiteralPath $script:handler -Value @($Requires, 'param($LambdaInput, $LambdaContext)') }
        $splat = $script:splat.Clone()
        foreach ($key in $Extra.Keys) { $splat[$key] = $Extra[$key] }
        { New-LambdaPSFunctionDeployment @splat -WhatIf } | Should -Throw -ErrorId "$ErrorId,New-LambdaPSFunctionDeployment"
        Test-Path -LiteralPath $script:package | Should -BeFalse
    }

    It 'fails with SourceNotPowerShell for a single file that is not a .ps1' {
        $text = Join-Path $TestDrive 'notps.txt'
        Set-Content -LiteralPath $text -Value 'x'
        $splat = $script:splat.Clone()
        $splat.ProjectPath = $text
        { New-LambdaPSFunctionDeployment @splat -WhatIf } | Should -Throw -ErrorId 'SourceNotPowerShell,New-LambdaPSFunctionDeployment'
    }
}

Describe 'Invoke-LambdaPSFunction -WhatIf' -Tag 'Component' {
    It 'invokes nothing for any of the functions piped in' {
        Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith { throw 'must not call AWS under -WhatIf' }

        $result = @('fn-a', 'fn-b' | Invoke-LambdaPSFunction -Payload @{ ping = $true } -WhatIf)

        $result | Should -BeNullOrEmpty
        Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 0 -Exactly
    }
}

Describe 'Build-LambdaPSRuntimePackage -WhatIf' -Tag 'Component' {
    BeforeAll {
        $script:layer = Join-Path $TestDrive 'layer'
        $null = New-LambdaPSRuntimeProject -Path $script:layer -Target Layer -PowerShellVersion '7.4.*'
    }

    It 'fails with <ErrorId> when <Case>' -ForEach @(
        @{ Case = 'no SHA256 is pinned for the pwsh version'; ErrorId = 'UnknownPwshArchiveHash'; Extra = @{ PowerShellVersion = '7.4.2' } }
        @{ Case = '-PwshArchivePath does not exist'; ErrorId = 'PwshArchiveNotFound'; Extra = @{ PwshArchivePath = 'TestDrive:/nope.tar.gz' } }
    ) {
        $out = Join-Path $TestDrive "layer-$([guid]::NewGuid().ToString('N')).zip"
        { Build-LambdaPSRuntimePackage -Path $script:layer -OutFile $out @Extra -WhatIf } |
            Should -Throw -ErrorId "$ErrorId,Build-LambdaPSRuntimePackage"
        Test-Path -LiteralPath $out | Should -BeFalse
    }
}

# -WhatIf on the cmdlets that write files or run docker must write nothing and run nothing.
Describe '-WhatIf writes nothing' -Tag 'Component' {
    It 'Build-LambdaPSRuntimeImage runs no docker build' {
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'docker' } }
        Mock 'Invoke-LambdaPSExternalTool' -ModuleName $script:moduleName -MockWith { }
        $project = Join-Path $TestDrive 'image-whatif'
        $null = New-LambdaPSRuntimeProject -Path $project -PowerShellVersion '7.4.*'

        $result = Build-LambdaPSRuntimeImage -Path $project -ImageUri '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v1' -WhatIf

        $result | Should -BeNullOrEmpty
        Should -Invoke 'Invoke-LambdaPSExternalTool' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'Build-LambdaPSRuntimePackage writes no package and downloads nothing' {
        Mock 'Invoke-WebRequest' -ModuleName $script:moduleName -MockWith { throw 'must not download under -WhatIf' }
        $project = Join-Path $TestDrive 'layer-whatif'
        $null = New-LambdaPSRuntimeProject -Path $project -PowerShellVersion '7.4.*' -Target Layer
        $out = Join-Path $TestDrive 'layer-whatif.zip'

        $result = Build-LambdaPSRuntimePackage -Path $project -OutFile $out -WhatIf

        $result | Should -BeNullOrEmpty
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'Compress-LambdaPSFunctionPackage writes no package' {
        $handler = Join-Path $TestDrive 'compress-whatif.ps1'
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)'
        $out = Join-Path $TestDrive 'compress-whatif.zip'

        $result = Compress-LambdaPSFunctionPackage -Path $handler -OutFile $out -WhatIf

        $result | Should -BeNullOrEmpty
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'Build-LambdaPSRuntimePackage names the Layer package in its What if line' {
        $project = Join-Path $TestDrive 'layer-whatif-text'
        $null = New-LambdaPSRuntimeProject -Path $project -PowerShellVersion '7.4.*' -Target Layer
        $out = Join-Path $TestDrive 'wi.zip'

        $result = Invoke-InChildPwsh -Command "Build-LambdaPSRuntimePackage -Path '$project' -OutFile '$out' -WhatIf"

        $result.Output | Should -BeLike '*What if: Performing the operation "Build container-free Layer package" on target "*wi.zip".*'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'Compress-LambdaPSFunctionPackage names the package in its What if line' {
        $handler = Join-Path $TestDrive 'compress-whatif-text.ps1'
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)'
        $out = Join-Path $TestDrive 'wi.zip'

        $result = Invoke-InChildPwsh -Command "Compress-LambdaPSFunctionPackage -Path '$handler' -OutFile '$out' -WhatIf"

        $result.Output | Should -BeLike '*What if: Performing the operation "Create function package" on target "*wi.zip".*'
        Test-Path -LiteralPath $out | Should -BeFalse
    }

    It 'Save-LambdaPSModule downloads nothing and creates no folder' {
        Mock 'Save-LambdaPSModuleFromRepository' -ModuleName $script:moduleName -MockWith { throw 'must not download under -WhatIf' }
        Mock 'Save-LambdaPSAWSToolsModule' -ModuleName $script:moduleName -MockWith { throw 'must not download under -WhatIf' }
        $path = Join-Path $TestDrive 'save-whatif'

        $null = Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSTests.NotInstalled'; RequiredVersion = '1.0.0' } -Path $path -WhatIf

        Test-Path -LiteralPath $path | Should -BeFalse
    }
}
