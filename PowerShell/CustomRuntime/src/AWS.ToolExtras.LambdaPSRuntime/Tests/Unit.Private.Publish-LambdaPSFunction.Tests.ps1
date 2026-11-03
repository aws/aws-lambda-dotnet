BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Publish-LambdaPSFunction' -Tag 'Unit' {
    # This seam reads the package into a byte[] (the supported -Code_ZipFile / -ZipFile parameters
    # take bytes; the path-taking -ZipFilename is [Obsolete]). [System.IO.File]::ReadAllBytes is a .NET
    # static and cannot be mocked, so these tests need a REAL file on disk rather than a mocked
    # Test-Path. The path is computed from an expression both here and in each test, so no variable has
    # to cross the InModuleScope boundary.
    BeforeAll {
        $env:LAMBDAPS_SEAM_TEST_ZIP = [System.IO.Path]::Combine($TestDrive, 'package.zip')
        [System.IO.File]::WriteAllBytes($env:LAMBDAPS_SEAM_TEST_ZIP, [byte[]](0x50, 0x4B, 0x05, 0x06))
    }

    AfterAll {
        Remove-Item -Path Env:LAMBDAPS_SEAM_TEST_ZIP -ErrorAction SilentlyContinue
    }

    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'creates the function with -Runtime custom and the RuntimeConfig binding, returning its ARN' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            Mock 'Publish-LMFunction' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:aws:lambda:us-east-1:111122223333:function:hello' }
            }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful' } }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $result = Publish-LambdaPSFunction @splat

            $result.Arn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:function:hello'
            $result.Updated | Should -BeFalse
            Should -Invoke 'Publish-LMFunction' -Times 1 -Exactly -ParameterFilter {
                $Runtime -eq 'custom' -and
                $RuntimeConfig_RuntimeArn -eq 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' -and
                $RuntimeConfig_RuntimeAlias -eq 'default' -and
                $Handler -eq 'handler.ps1' -and
                $Timeout -eq 30 -and $MemorySize -eq 512
            }
        }
    }

    It 'updates the code, then the configuration, of a function that already exists' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # The existing function is bound to the requested runtime and alias unless a test says otherwise.
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            Mock 'Publish-LMFunction' -MockWith {
                throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists')
            }
            Mock 'Update-LMFunctionCode' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:aws:lambda:us-east-1:111122223333:function:hello' }
            }
            Mock 'Update-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:aws:lambda:us-east-1:111122223333:function:hello' }
            }

            $splat = @{
                FunctionName = 'hello'; Handler = 'other.ps1::Handle'; Role = 'arn:new-role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $result = Publish-LambdaPSFunction @splat

            $result.Updated | Should -BeTrue
            Should -Invoke 'Update-LMFunctionCode' -Times 1 -Exactly -ParameterFilter { $FunctionName -eq 'hello' }
            Should -Invoke 'Update-LMFunctionConfiguration' -Times 1 -Exactly -ParameterFilter {
                $FunctionName -eq 'hello' -and $Handler -eq 'other.ps1::Handle' -and $Role -eq 'arn:new-role' -and
                $Timeout -eq 60 -and $MemorySize -eq 1024 -and
                $RuntimeConfig_RuntimeArn -eq 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' -and
                $RuntimeConfig_RuntimeAlias -eq 'default' -and $Region -eq 'us-east-1' -and
                -not $PSBoundParameters.ContainsKey('Environment_Variable') -and -not $PSBoundParameters.ContainsKey('Environment_IsVariablesSet')
            }
        }
    }

    It 'says the function has its new code but its previous configuration when the configuration update fails' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # The existing function is on another runtime, so the failed update leaves it on that one.
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/old-rt'; RuntimeAlias = 'stable' } } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:aws:lambda:us-east-1:111122223333:function:hello' } }
            Mock 'Update-LMFunctionConfiguration' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:UpdateFunctionConfiguration') }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:new-role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'arm64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $err = { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'FunctionConfigurationNotApplied' -PassThru

            $err.Exception.Message | Should -BeLike "*new code and architecture (arm64)*previous configuration and runtime binding ('arn:aws:lambda:us-east-1:111122223333:runtime/old-rt', alias 'stable')*Invokes may fail until the configuration is applied*Fix the cause the service reported, then run the deploy again. The service reported: not authorized to perform lambda:UpdateFunctionConfiguration"
            $err.Exception.InnerException | Should -BeOfType ([LambdaPSTests.Fakes.AccessDeniedException])
            $err.TargetObject | Should -Be 'hello'
            Should -Invoke 'Update-LMFunctionCode' -Times 1 -Exactly
        }
    }

    # The closest cases to the one above: no alias to name, and nothing that makes invokes fail.
    It 'leaves out the alias and the invoke warning when there is no alias and nothing changed' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; Architectures = @('x86_64'); Handler = 'Module::Handlers::Invoke'
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = '' } } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
            Mock 'Update-LMFunctionConfiguration' -MockWith { throw [System.InvalidOperationException]::new('Rate exceeded') }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'; UpdateSetting = @('Timeout')
            }
            $err = { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'FunctionConfigurationNotApplied' -PassThru

            $err.Exception.Message | Should -BeLike "*runtime binding ('arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'). The configuration update failed.*"
            $err.Exception.Message | Should -Not -BeLike '*alias*'
            $err.Exception.Message | Should -Not -BeLike '*Invokes may fail*'
        }
    }

    It 'says the code update failed, and the function keeps its previous code, when that wait ends in FunctionNotActive' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # Lambda rejects the new code asynchronously: its last update ends Failed.
            $script:codeUpdated = $false
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = $(if ($script:codeUpdated) { 'Failed' } else { 'Successful' })
                    LastUpdateStatusReason = 'code update failed'
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { $script:codeUpdated = $true; [PSCustomObject]@{ FunctionArn = 'arn:f' } }
            Mock 'Update-LMFunctionConfiguration' -MockWith { }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $err = { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'FunctionNotActive' -PassThru

            $err.Exception.Message | Should -BeLike '*will not become ready on its own*The code update failed, so the function keeps its previous code and configuration. Fix the cause Lambda names, then deploy again.'
            $err.Exception.Message | Should -Not -BeLike '*has its new code*'
            $err.Exception.Message | Should -Not -BeLike '*settles*'
        }
    }

    It 'warns that invokes may fail when only the handler changes and the configuration update fails' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; Handler = 'old.ps1'; Architectures = @('x86_64')
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
            Mock 'Update-LMFunctionConfiguration' -MockWith { throw [System.InvalidOperationException]::new('Rate exceeded') }

            $splat = @{
                FunctionName = 'hello'; Handler = 'new.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'; UpdateSetting = @('Handler')
            }
            $err = { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'FunctionConfigurationNotApplied' -PassThru

            $err.Exception.Message | Should -BeLike '*Invokes may fail until the configuration is applied*handler*'
        }
    }

    It 'says the configuration was not applied when the wait between the code and configuration updates fails' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # Ready before the code update; InProgress for good after it.
            $script:codeUpdated = $false
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = $(if ($script:codeUpdated) { 'InProgress' } else { 'Successful' })
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { $script:codeUpdated = $true; [PSCustomObject]@{ FunctionArn = 'arn:f' } }
            Mock 'Update-LMFunctionConfiguration' -MockWith { }
            Mock 'Start-Sleep' -MockWith { }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $err = { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'FunctionStateTimeout' -PassThru

            $err.Exception | Should -BeOfType ([System.TimeoutException])
            $err.Exception.Message | Should -BeLike '*The configuration was not applied.*has its new code*keeps its previous configuration*Run the deploy again once the function settles*'
            Should -Invoke 'Update-LMFunctionConfiguration' -Times 0 -Exactly
        }
    }

    It 'reports the configuration Lambda returns from the configuration update' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # The existing function is bound to the requested runtime and alias unless a test says otherwise.
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            # The code update answers with the configuration from before the configuration update.
            Mock 'Update-LMFunctionCode' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:f'; Handler = 'old.ps1'; Timeout = 30; MemorySize = 512; Role = 'arn:old-role' }
            }
            # Lambda's answer differs from the request, so echoing the request back would fail this test.
            Mock 'Update-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:f'; Handler = 'returned.ps1::Handle'; Timeout = 45; MemorySize = 768; Role = 'arn:returned-role'
                    Architectures = @('x86_64'); RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'returned' } }
            }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 60; MemorySize = 1024
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $result = Publish-LambdaPSFunction @splat

            $result.Timeout | Should -Be 45
            $result.MemorySize | Should -Be 768
            $result.Handler | Should -Be 'returned.ps1::Handle'
            $result.Role | Should -Be 'arn:returned-role'
            $result.RuntimeAlias | Should -Be 'returned'
            $result.Arn | Should -Be 'arn:f'
        }
    }

    It 'applies -Architecture on update when the function is already bound to the requested runtime' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Get-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
            }
            Mock 'Update-LMFunctionCode' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:f'; Architectures = @('arm64')
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
            }
            Mock 'Update-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:f'; Architectures = @('arm64')
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
            }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'arm64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $result = Publish-LambdaPSFunction @splat

            Should -Invoke 'Update-LMFunctionCode' -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
            $result.Architecture | Should -Be 'arm64'
        }
    }

    It 'binds a function on another runtime to the requested one, with -Architecture applied to the code' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Get-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:runtime/old-rt'; RuntimeAlias = 'stable' } }
            }
            Mock 'Update-LMFunctionCode' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:f'; Architectures = @('arm64')
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:runtime/old-rt'; RuntimeAlias = 'stable' } }
            }
            Mock 'Update-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ FunctionArn = 'arn:f'; Architectures = @('arm64')
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:runtime/new-rt'; RuntimeAlias = 'default' } }
            }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:runtime/new-rt'; RuntimeAlias = 'default'
                Architecture = 'arm64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $result = Publish-LambdaPSFunction @splat

            Should -Invoke 'Update-LMFunctionCode' -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
            Should -Invoke 'Update-LMFunctionConfiguration' -Times 1 -Exactly -ParameterFilter {
                $RuntimeConfig_RuntimeArn -eq 'arn:runtime/new-rt' -and $RuntimeConfig_RuntimeAlias -eq 'default'
            }
            $result.Architecture | Should -Be 'arm64'
            $result.RuntimeArn | Should -Be 'arn:runtime/new-rt'
            $result.RuntimeAlias | Should -Be 'default'
        }
    }

    # A handler the caller did not give is kept only when it is the Module:: form or a script the
    # package has; one written for another runtime would leave the function unable to start.
    It 'sends the package''s handler in place of a kept <Case> handler only when it is not the Module:: form' -ForEach @(
        @{ Case = 'non-PowerShell'; Kept = 'app.handler'; Sent = 'handler.ps1' }
        @{ Case = 'Module::'; Kept = 'Module::Handlers::Invoke'; Sent = $null }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Kept = $Kept; Sent = $Sent } {
            param($Kept, $Sent)
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            $script:keptHandler = $Kept
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Get-LMFunctionConfiguration' -MockWith {
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; Handler = $script:keptHandler
                    RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:runtime/old-rt'; RuntimeAlias = 'stable' } }
            }
            Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
            Mock 'Update-LMFunctionConfiguration' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:runtime/new-rt'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'; UpdateSetting = @('RuntimeAlias')
            }
            $null = Publish-LambdaPSFunction @splat

            Should -Invoke 'Update-LMFunctionConfiguration' -Times 1 -Exactly -ParameterFilter { [string]$Handler -eq [string]$Sent }
        }
    }

    # The configuration is applied on the update path, so there is nothing to warn about.
    It 'does not warn when it updates an existing function' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # The existing function is bound to the requested runtime and alias unless a test says otherwise.
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            Mock 'Publish-LMFunction' -MockWith {
                throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists')
            }
            Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:x' } }

            $splat = @{
                FunctionName = 'hello'; Handler = 'renamed.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $warnings = @()
            $null = Publish-LambdaPSFunction @splat -WarningVariable warnings -WarningAction SilentlyContinue

            $warnings.Count | Should -Be 0
        }
    }

    It 'does not warn when it creates the function' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:x' } }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful' } }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $warnings = @()
            $null = Publish-LambdaPSFunction @splat -WarningVariable warnings -WarningAction SilentlyContinue

            $warnings.Count | Should -Be 0
        }
    }

    # Lambda replaces the whole environment with what it is sent, and takes string values only.
    Context '-EnvironmentVariable' {
        BeforeEach {
            $script:splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
        }

        It 'sets the environment on create, sending each value as a string' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $Environment_Variable, $Environment_IsVariablesSet, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                    [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful' } }

                # A variable named Keys or Count is read as a variable, not as the table's property.
                $null = Publish-LambdaPSFunction @splat -EnvironmentVariable @{ RETRIES = 5; Keys = 'k'; Count = 'c'; POWERSHELL_RUNTIME_VERBOSE = 'TRUE' }

                Should -Invoke 'Publish-LMFunction' -Times 1 -Exactly -ParameterFilter {
                    $Environment_IsVariablesSet -eq $true -and @($Environment_Variable.GetEnumerator()).Count -eq 4 -and
                    $Environment_Variable['RETRIES'] -is [string] -and $Environment_Variable['RETRIES'] -eq '5' -and
                    $Environment_Variable['Keys'] -eq 'k' -and $Environment_Variable['Count'] -eq 'c' -and
                    $Environment_Variable['POWERSHELL_RUNTIME_VERBOSE'] -eq 'TRUE'
                }
            }
        }

        It 'replaces the environment of an existing function, and @{} clears it' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $Environment_Variable, $Environment_IsVariablesSet, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
                function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                    [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
                Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
                Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Update-LMFunctionConfiguration' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }

                $null = Publish-LambdaPSFunction @splat -EnvironmentVariable @{ STAGE = 'prod' }
                $null = Publish-LambdaPSFunction @splat -EnvironmentVariable @{}

                Should -Invoke 'Update-LMFunctionConfiguration' -Times 1 -Exactly -ParameterFilter {
                    $Environment_IsVariablesSet -eq $true -and @($Environment_Variable.GetEnumerator()).Count -eq 1 -and $Environment_Variable['STAGE'] -eq 'prod'
                }
                Should -Invoke 'Update-LMFunctionConfiguration' -Times 1 -Exactly -ParameterFilter {
                    $Environment_IsVariablesSet -eq $true -and @($Environment_Variable.GetEnumerator()).Count -eq 0
                }
            }
        }
    }

    It 'rethrows a non-already-exists Publish-LMFunction error instead of masking it' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            # The existing function is bound to the requested runtime and alias unless a test says otherwise.
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
            # The message deliberately contains the word 'exists': a permission failure must surface
            # even when its wording looks like an already-exists condition.
            Mock 'Publish-LMFunction' -MockWith {
                throw [LambdaPSTests.Fakes.AccessDeniedException]::new(
                    'not authorized to perform lambda:CreateFunction because no identity-based policy exists')
            }
            Mock 'Update-LMFunctionCode' -MockWith { }
            Mock 'Update-LMFunctionConfiguration' -MockWith { }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            { Publish-LambdaPSFunction @splat } | Should -Throw -ExceptionType ([LambdaPSTests.Fakes.AccessDeniedException])
            Should -Invoke 'Update-LMFunctionCode' -Times 0 -Exactly
            Should -Invoke 'Update-LMFunctionConfiguration' -Times 0 -Exactly
        }
    }

    # Lambda creates a function in State Pending (and runs a code update InProgress); an invoke in that
    # window is refused with ResourceConflictException, so the seam must not return until it settles.
    Context 'waiting for the function to be ready' {
        BeforeEach {
            $script:splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
        }

        It 'waits while the new function is Pending and returns once it is Active' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f'; State = 'Pending' } }
                $script:reads = 0
                Mock 'Get-LMFunctionConfiguration' -MockWith {
                    $script:reads++
                    if ($script:reads -lt 3) { [PSCustomObject]@{ State = 'Pending'; LastUpdateStatus = 'InProgress' } }
                    else { [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful' } }
                }
                Mock 'Start-Sleep' -MockWith { }

                $result = Publish-LambdaPSFunction @splat

                $result.Arn | Should -Be 'arn:f'
                Should -Invoke 'Get-LMFunctionConfiguration' -Times 3 -Exactly -ParameterFilter { $FunctionName -eq 'hello' }
                Should -Invoke 'Start-Sleep' -Times 2 -Exactly
            }
        }

        It 'waits for an InProgress code update on an Active function' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
                function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
                Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Update-LMFunctionConfiguration' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                $script:reads = 0
                Mock 'Get-LMFunctionConfiguration' -MockWith {
                    $script:reads++
                    # Read 1 is the binding check before the code update; read 2 sees that update in
                    # progress, read 3 settled, and read 4 follows the configuration update.
                    $status = if ($script:reads -eq 2) { 'InProgress' } else { 'Successful' }
                    [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = $status
                        RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
                }
                Mock 'Start-Sleep' -MockWith { }

                $result = Publish-LambdaPSFunction @splat

                $result.Updated | Should -BeTrue
                Should -Invoke 'Get-LMFunctionConfiguration' -Times 4 -Exactly
                Should -Invoke 'Start-Sleep' -Times 1 -Exactly
                Should -Invoke 'Update-LMFunctionConfiguration' -Times 1 -Exactly
            }
        }

        It 'throws FunctionNotActive with the reason Lambda reports when the function settles to Failed' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Get-LMFunctionConfiguration' -MockWith {
                    [PSCustomObject]@{ State = 'Failed'; StateReason = 'The runtime version is not available.'; LastUpdateStatus = 'Failed' }
                }
                Mock 'Start-Sleep' -MockWith { }

                $err = $null
                try { Publish-LambdaPSFunction @splat } catch { $err = $_ }

                $err.FullyQualifiedErrorId | Should -Be 'FunctionNotActive'
                $err.Exception.Message | Should -BeLike '*The runtime version is not available.*'
                Should -Invoke 'Start-Sleep' -Times 0 -Exactly
            }
        }

        It 'throws FunctionStateTimeout when the function is still Pending after the wait budget' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Get-LMFunctionConfiguration' -MockWith { [PSCustomObject]@{ State = 'Pending'; LastUpdateStatus = 'InProgress' } }
                Mock 'Start-Sleep' -MockWith { }

                $err = $null
                try { Publish-LambdaPSFunction @splat } catch { $err = $_ }

                $err.FullyQualifiedErrorId | Should -Be 'FunctionStateTimeout'
                # The wait budget is data (Config/wait.psd1), and the message reports the time it allows.
                $wait = $script:Config.wait.Function
                $err.Exception.Message | Should -BeLike "*still Pending after $($wait.Attempts * $wait.IntervalSeconds) seconds*Get-LMFunctionConfiguration -FunctionName hello -Region us-east-1*"
                Should -Invoke 'Get-LMFunctionConfiguration' -Times $wait.Attempts -Exactly
                Should -Invoke 'Start-Sleep' -Times $wait.Attempts -Exactly -ParameterFilter { $Seconds -eq $wait.IntervalSeconds }
            }
        }

        It 'throws FunctionNotActive naming LastUpdateStatusReason when a code update fails on an Active function' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
                function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
                Mock 'Update-LMFunctionCode' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Update-LMFunctionConfiguration' -MockWith { }
                $script:reads = 0
                Mock 'Get-LMFunctionConfiguration' -MockWith {
                    $script:reads++
                    # Read 1, before the update, still shows an EARLIER failed update: that must not block.
                    [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Failed'
                        LastUpdateStatusReason = 'Code package is invalid.'; LastUpdateStatusReasonCode = 'InvalidZipFileException'
                        RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
                }

                $err = $null
                try { Publish-LambdaPSFunction @splat } catch { $err = $_ }

                $err.FullyQualifiedErrorId | Should -Be 'FunctionNotActive'
                $err.Exception.Message | Should -BeLike '*LastUpdateStatus Failed: InvalidZipFileException - Code package is invalid.*'
                Should -Invoke 'Update-LMFunctionCode' -Times 1 -Exactly
                # A failed code update is not followed by the configuration update.
                Should -Invoke 'Update-LMFunctionConfiguration' -Times 0 -Exactly
                $script:reads | Should -Be 2
            }
        }

        It 'waits for an earlier update to settle before updating the code' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
                function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
                $script:events = [System.Collections.Generic.List[string]]::new()
                Mock 'Update-LMFunctionCode' -MockWith { $script:events.Add('update'); [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Update-LMFunctionConfiguration' -MockWith { $script:events.Add('configure'); [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                $script:reads = 0
                Mock 'Get-LMFunctionConfiguration' -MockWith {
                    $script:reads++
                    $status = if ($script:reads -le 2) { 'InProgress' } else { 'Successful' }
                    $script:events.Add("read:$status")
                    [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = $status
                        RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
                }
                Mock 'Start-Sleep' -MockWith { }

                $null = Publish-LambdaPSFunction @splat

                ($script:events -join ',') | Should -Be 'read:InProgress,read:InProgress,read:Successful,update,read:Successful,configure,read:Successful'
            }
        }

        # An Inactive function does not become Active on its own; updating its code is what restores it.
        It 'updates the code of an Inactive function without waiting, then waits for it to become Active' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
                function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
                $script:events = [System.Collections.Generic.List[string]]::new()
                $script:codeUpdated = $false
                Mock 'Update-LMFunctionCode' -MockWith { $script:codeUpdated = $true; $script:events.Add('update'); [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Update-LMFunctionConfiguration' -MockWith { $script:events.Add('configure'); [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                $script:afterUpdate = 0
                Mock 'Get-LMFunctionConfiguration' -MockWith {
                    $state = if (-not $script:codeUpdated) { 'Inactive' } elseif (++$script:afterUpdate -eq 1) { 'Pending' } else { 'Active' }
                    $script:events.Add("read:$state")
                    [PSCustomObject]@{ State = $state; LastUpdateStatus = 'Successful'
                        RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } }
                }
                Mock 'Start-Sleep' -MockWith { }

                $null = Publish-LambdaPSFunction @splat

                ($script:events -join ',') | Should -Be 'read:Inactive,update,read:Pending,read:Active,configure,read:Active'
            }
        }

        It 'stops at once with FunctionNotActive when the function is <State>' -ForEach @(
            @{ State = 'Deleting' }, @{ State = 'Deactivated' }
        ) {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat; State = $State } {
                param($splat, $State)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Get-LMFunctionConfiguration' -MockWith ([scriptblock]::Create("[PSCustomObject]@{ State = '$State'; LastUpdateStatus = 'Successful' }"))
                Mock 'Start-Sleep' -MockWith { }

                $err = $null
                try { Publish-LambdaPSFunction @splat } catch { $err = $_ }

                $err.FullyQualifiedErrorId | Should -Be 'FunctionNotActive'
                $err.Exception.Message | Should -BeLike "*State $State, no reason reported*"
                Should -Invoke 'Start-Sleep' -Times 0 -Exactly
            }
        }

        It 'rethrows a configuration read error during the wait instead of retrying it' {
            InModuleScope $script:moduleName -Parameters @{ splat = $script:splat } {
                param($splat)
                function Publish-LMFunction {
                    param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                        $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
                }
                function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
                Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
                Mock 'Get-LMFunctionConfiguration' -MockWith { throw [System.UnauthorizedAccessException]::new('not authorized to perform lambda:GetFunctionConfiguration') }
                Mock 'Start-Sleep' -MockWith { }

                { Publish-LambdaPSFunction @splat } | Should -Throw -ExceptionType ([System.UnauthorizedAccessException])
                Should -Invoke 'Get-LMFunctionConfiguration' -Times 1 -Exactly
            }
        }
    }

    It 'refuses to replace the code of a function that is not bound to a customer-managed runtime' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; Runtime = 'python3.12'; RuntimeConfig = $null } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { }
            Mock 'Update-LMFunctionConfiguration' -MockWith { }

            $splat = @{
                FunctionName = 'orders'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $err = $null
            try { Publish-LambdaPSFunction @splat } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'FunctionNotRuntimeBound'
            $err.Exception.Message | Should -BeLike "*'python3.12'*"
            Should -Invoke 'Update-LMFunctionCode' -Times 0 -Exactly
            Should -Invoke 'Update-LMFunctionConfiguration' -Times 0 -Exactly
        }
    }

    It 'names a container-image function as such when refusing to replace its code' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction)
                [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; PackageType = 'Image'; Runtime = $null; RuntimeConfig = $null } }
            Mock 'Publish-LMFunction' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
            Mock 'Update-LMFunctionCode' -MockWith { }
            Mock 'Update-LMFunctionConfiguration' -MockWith { }

            $splat = @{
                FunctionName = 'orders'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            $err = $null
            try { Publish-LambdaPSFunction @splat } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'FunctionNotRuntimeBound'
            $err.Exception.Message | Should -BeLike "Function 'orders' is a container-image function and is not bound*"
            Should -Invoke 'Update-LMFunctionCode' -Times 0 -Exactly
            Should -Invoke 'Update-LMFunctionConfiguration' -Times 0 -Exactly
        }
    }

    It 'throws PackageNotFound when the .zip is missing' {
        InModuleScope $script:moduleName {
            Mock 'Test-Path' -MockWith { $false }

            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = './missing.zip'; Region = 'us-east-1'
            }
            { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'PackageNotFound'
        }
    }

    # The package goes in the request, and Lambda takes at most 50 MB zipped that way. The file is
    # extended without being written, so it takes no disk space where the file system is sparse.
    It 'throws PackageTooLarge for a .zip over 50 MB, before any AWS call, and accepts one of exactly 50 MB' {
        InModuleScope $script:moduleName {
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Publish-LMFunction' -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:f' } }
            Mock 'Get-LMFunctionConfiguration' -MockWith { [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful' } }
            Mock 'Start-Sleep' -MockWith { }
            $zip = [System.IO.Path]::Combine($TestDrive, 'large.zip')
            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $zip; Region = 'us-east-1'
            }
            try {
                $stream = [System.IO.File]::Create($zip)
                try { $stream.SetLength(50MB + 1) } finally { $stream.Dispose() }

                $err = { Publish-LambdaPSFunction @splat } | Should -Throw -ErrorId 'PackageTooLarge' -PassThru
                $err.Exception.Message | Should -BeLike "*over the 50 MB Lambda accepts in a direct upload*-Code_S3Bucket*"
                Should -Invoke 'Get-LMFunctionConfiguration' -Times 0 -Exactly
                Should -Invoke 'Publish-LMFunction' -Times 0 -Exactly

                $stream = [System.IO.File]::Open($zip, 'Open')
                try { $stream.SetLength(50MB) } finally { $stream.Dispose() }

                $null = Publish-LambdaPSFunction @splat
                Should -Invoke 'Publish-LMFunction' -Times 1 -Exactly
            }
            finally { Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue }
        }
    }

    It 'rejects a whitespace function name at parameter binding' {
        InModuleScope $script:moduleName {
            $splat = @{
                FunctionName = '   '; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = $env:LAMBDAPS_SEAM_TEST_ZIP; Region = 'us-east-1'
            }
            { Publish-LambdaPSFunction @splat } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Publish-LambdaPSFunction'
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    # Each case names the Get-LMFunctionConfiguration answer, and whether the create finds the function
    # already there (so the update path runs) or the package is missing.
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'PackageNotFound'; Exists = $false; Status = @{ State = 'Active'; LastUpdateStatus = 'Successful' }; MissingZip = $true }
        @{ ErrorId = 'FunctionNotActive'; Exists = $false; Status = @{ State = 'Failed'; LastUpdateStatus = 'Failed' }; MissingZip = $false }
        @{ ErrorId = 'FunctionStateTimeout'; Exists = $false; Status = @{ State = 'Pending'; LastUpdateStatus = 'InProgress' }; MissingZip = $false }
        @{ ErrorId = 'FunctionNotRuntimeBound'; Exists = $true; Status = @{ State = 'Active'; LastUpdateStatus = 'Successful'; Runtime = 'python3.12' }; MissingZip = $false }
        @{ ErrorId = 'FunctionConfigurationNotApplied'; Exists = $true; MissingZip = $false
            Status = @{ State = 'Active'; LastUpdateStatus = 'Successful'; RuntimeConfig = [PSCustomObject]@{ RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default' } } }
    ) {
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Exists = $Exists; Status = $Status; MissingZip = $MissingZip } {
            param($ErrorId, $Exists, $Status, $MissingZip)
            function Publish-LMFunction {
                param($FunctionName, $Handler, $Runtime, $Role, $Architecture, $Code_ZipFile, $Timeout, $MemorySize,
                    $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Region, $ClientConfig, $ErrorAction)
            }
            function Update-LMFunctionCode { param($FunctionName, $ZipFile, $Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Update-LMFunctionConfiguration { param($FunctionName, $Handler, $Role, $Timeout, $MemorySize, $RuntimeConfig_RuntimeArn, $RuntimeConfig_RuntimeAlias, $Environment_Variable, $Environment_IsVariablesSet, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMFunctionConfiguration { param($FunctionName, $Region, $ClientConfig, $ErrorAction) }
            $script:publishExists = $Exists
            $script:publishStatus = [PSCustomObject]$Status
            Mock 'Publish-LMFunction' -MockWith {
                if ($script:publishExists) { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
                [PSCustomObject]@{ FunctionArn = 'arn:f' }
            }
            Mock 'Get-LMFunctionConfiguration' -MockWith { $script:publishStatus }
            Mock 'Update-LMFunctionCode' -MockWith { }
            Mock 'Update-LMFunctionConfiguration' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('refused') }
            Mock 'Start-Sleep' -MockWith { }
            $splat = @{
                FunctionName = 'hello'; Handler = 'handler.ps1'; Role = 'arn:role'
                RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'default'
                Architecture = 'x86_64'; Timeout = 30; MemorySize = 512
                ZipPath = if ($MissingZip) { [System.IO.Path]::Combine($TestDrive, 'missing.zip') } else { $env:LAMBDAPS_SEAM_TEST_ZIP }
                Region = 'us-east-1'
            }
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call { Publish-LambdaPSFunction @splat } -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Publish-LambdaPSFunction' }) | Should -BeNullOrEmpty
        }
    }
}
