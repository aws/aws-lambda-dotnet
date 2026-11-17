BeforeAll {
    # The scaffold cmdlets, the deploy orchestrator and the AWS seams are in one module, so the
    # deployment cmdlet's collaborators (New-LambdaPSRuntimeProject, Build-LambdaPSRuntimeImage, and
    # the seams) are all mockable by -ModuleName.
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
    # The version a deploy of PowerShell 7.4 to a runtime with no versions is named.
    $script:defaultVersion = "$($script:pwshConfig.DefaultPatch['7.4'])-1"
    # -RuntimeAlias is mandatory; the tests that aren't about it deploy to 'default'.
    $PSDefaultParameterValues['New-LambdaPSRuntimeDeployment:RuntimeAlias'] = 'default'
}

AfterAll {
    $PSDefaultParameterValues.Remove('New-LambdaPSRuntimeDeployment:RuntimeAlias')
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSRuntimeDeployment' -Tag 'Unit' {
    BeforeEach {
        # Run each test from TestDrive, so a relative -ProjectPath stays there.
        Push-Location $TestDrive
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{
                PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.ContainerRuntime'
                Name       = 'docker'; Path = '/usr/bin/docker'
            }
        }
        Mock 'Get-STSCallerIdentity' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Account = '111122223333' } }
        # Runtime preflight: by default the runtime exists and the requested version does not.
        Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = $RuntimeName } }
        Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
            throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')
        }
        Mock 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -MockWith { }
        # Microsoft's package repository has nothing newer than the module's pin.
        Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { $script:pwshConfig.DefaultPatch[$PowerShellVersion] }
        # ... and has no alias yet.
        Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
            throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime alias does not exist')
        }
        # ... and the repository has no image tagged with it.
        Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -MockWith {
            if ($ImageId) { throw [LambdaPSTests.Fakes.ImageNotFoundException]::new('The image does not exist') }
        }
        Mock 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -MockWith { }
        Mock 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -MockWith { $RepositoryName }
        # The push build reports the pushed image's digest, as buildx does.
        Mock 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ Tag = $Tag; ImageUri = $ImageUri; ImageDigest = $(if ($ImageUri) { 'sha256:abc123' }); Architecture = $Architecture; Pushed = [bool]$ImageUri }
        }
        Mock 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ RuntimeName = $RuntimeName; ImageUri = $ImageUri; Version = 'v1'; Arn = "arn:aws:lambda:us-east-1:111122223333:runtime/$RuntimeName/version/v1" }
        }
        Mock 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -MockWith { $RuntimeAlias }
    }

    AfterEach {
        Pop-Location
    }

    Context 'happy path with no modules' {
        It 'returns a deployment summary with the correctly composed ImageUri' {
            $splat = @{
                RuntimeName       = 'my-ps'
                PowerShellVersion = '7.4'
                Region            = 'us-east-1'
                RepositoryName    = 'powershell-runtime'
            }
            $result = New-LambdaPSRuntimeDeployment @splat

            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.RuntimeDeployment'
            $result.ImageUri | Should -Be "111122223333.dkr.ecr.us-east-1.amazonaws.com/powershell-runtime:$script:defaultVersion"
            $result.RuntimeArn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
            $result.RuntimeName | Should -Be 'my-ps'
            $result.PowerShellVersion | Should -Be '7.4'
            $result.Region | Should -Be 'us-east-1'
            $result.RepositoryName | Should -Be 'powershell-runtime'
            $result.RuntimeVersion | Should -Be $script:defaultVersion
            $result.RuntimeAlias | Should -Be 'default'
            $result.Architecture | Should -Be 'x86_64'
            $result.PSObject.Properties.Name | Should -Not -Contain 'ContainerRuntime'
            $result.ModulesStaged | Should -BeNullOrEmpty
        }

        It 'forwards an empty module set to the scaffolder when no modules are supplied' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
            }
            $null = New-LambdaPSRuntimeDeployment @splat

            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $IncludeModule.Count -eq 0 }
        }
    }

    Context 'module forwarding' {
        It 'forwards -Module (alias) to the scaffolder' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
                Module = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            $null = New-LambdaPSRuntimeDeployment @splat

            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $IncludeModule[0].Name -eq 'AWS.Tools.S3' -and $IncludeModule[0].RequiredVersion -eq '5.0.103' }
        }

        It 'forwards -MissingModuleAction to the scaffolder, defaulting to Save' -ForEach @(
            @{ Given = $null; Expected = 'Save' }
            @{ Given = 'Install'; Expected = 'Install' }
            @{ Given = 'Stop'; Expected = 'Stop' }
        ) {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
                IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            if ($Given) { $splat.MissingModuleAction = $Given }
            $null = New-LambdaPSRuntimeDeployment @splat

            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $MissingModuleAction -eq $Expected }
        }

        It 'accepts the canonical -IncludeModule name and a list of modules' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
                IncludeModule = @(
                    @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' },
                    @{ ModuleName = 'AWS.Tools.DynamoDBv2'; RequiredVersion = '5.0.103' }
                )
            }
            $null = New-LambdaPSRuntimeDeployment @splat

            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $IncludeModule.Count -eq 2 }
        }

        # ModulesStaged is read from the scaffolded modules/ folder -- what the image contains --
        # not echoed from -IncludeModule, which omits the dependencies the scaffolder follows.
        It 'reports ModulesStaged from the scaffolded modules folder, including dependencies' {
            Mock 'Get-LambdaPSStagedModule' -ModuleName $script:moduleName -MockWith {
                , [object[]]@('AWS.Tools.Common', 'AWS.Tools.S3' | ForEach-Object {
                        [PSCustomObject]@{ PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.StagedModule'; Name = $_; Version = [version]'5.0.103' }
                    })
            }
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
                IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            $result = New-LambdaPSRuntimeDeployment @splat

            $result.ModulesStaged.Name | Should -Be @('AWS.Tools.Common', 'AWS.Tools.S3')
            $result.ModulesStaged[1].Version | Should -Be ([version]'5.0.103')
            Should -Invoke 'Get-LambdaPSStagedModule' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Path -match 'my-ps-runtime-[^\\/]+[\\/]modules$' }
        }

        It 'reports no ModulesStaged under -WhatIf, because nothing was scaffolded' {
            Mock 'Get-LambdaPSStagedModule' -ModuleName $script:moduleName -MockWith { }
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
                IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            $result = New-LambdaPSRuntimeDeployment @splat -WhatIf

            $result.ModulesStaged | Should -BeNullOrEmpty
            Should -Invoke 'Get-LambdaPSStagedModule' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'repository defaulting' {
        It 'defaults the ECR repository to the runtime name when -RepositoryName is omitted' {
            $splat = @{
                RuntimeName = 'my-powershell-runtime'; PowerShellVersion = '7.4'; Region = 'us-east-1'
            }
            $result = New-LambdaPSRuntimeDeployment @splat

            $result.RepositoryName | Should -Be 'my-powershell-runtime'
            $result.ImageUri | Should -Be "111122223333.dkr.ecr.us-east-1.amazonaws.com/my-powershell-runtime:$script:defaultVersion"
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RepositoryName -eq 'my-powershell-runtime' }
        }

        # A runtime name allows '.' next to another separator ('my..runtime'); an ECR repository name does not.
        It 'throws InvalidRepositoryName asking for -RepositoryName when the runtime name is not a valid ECR name' {
            $splat = @{
                RuntimeName = 'my..powershell-runtime'; PowerShellVersion = '7.4'; Region = 'us-east-1'
            }
            $err = { New-LambdaPSRuntimeDeployment @splat } |
                Should -Throw -ErrorId 'InvalidRepositoryName,New-LambdaPSRuntimeDeployment' -PassThru
            # Every character of the name is allowed, so the message names the rule that fails.
            $err.Exception.Message | Should -BeLike '*(lowercase letters and digits, 2-256 characters, in ''/''-separated parts where letters or digits are joined by one ''.'', one ''_'', two ''_'', or any number of ''-'')*'
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'names the separator rule when an explicit -RepositoryName is not a valid ECR name' {
            $err = { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName 'a..b' } |
                Should -Throw -ErrorId 'InvalidRepositoryName,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "Repository 'a..b' *(lowercase letters and digits, 2-256 characters, in '/'-separated parts where letters or digits are joined by one '.', one '_', two '_', or any number of '-')*"
            # Refused before the pwsh lookup reads Microsoft's package repository.
            Should -Invoke 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # The closest valid names: ECR joins letters or digits with any number of '-', or two '_'.
        It 'defaults the repository to a runtime name such as <Name>, which ECR accepts' -ForEach @(
            @{ Name = 'my--rt' }, @{ Name = 'my__rt' }
        ) {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName $Name -PowerShellVersion 7.4 -Region us-east-1 -WhatIf
            $result.RepositoryName | Should -Be $Name
        }

        It 'uses an explicit -RepositoryName even when the runtime name would be invalid' {
            $splat = @{
                RuntimeName = 'my..runtime'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'my-runtime'
            }
            $result = New-LambdaPSRuntimeDeployment @splat

            $result.RepositoryName | Should -Be 'my-runtime'
        }
    }

    # The service's runtime name rule: lowercase letters, digits, '_', '.' and '-', 2-128 characters,
    # starting and ending with a letter or digit.
    Context 'runtime name' {
        It 'rejects a -RuntimeName the service refuses: <Name>' -ForEach @(
            @{ Name = 'My-Runtime'; Value = 'My-Runtime' }, @{ Name = 'My-PS'; Value = 'My-PS' }, @{ Name = 'one character'; Value = 'r' }
            @{ Name = '-rt'; Value = '-rt' }, @{ Name = 'rt-'; Value = 'rt-' }, @{ Name = '_rt'; Value = '_rt' }
            @{ Name = 'my ps'; Value = 'my ps' }, @{ Name = 'a trailing newline'; Value = "my-ps`n" }
            @{ Name = '129 characters'; Value = ('r' * 129) }
        ) {
            { New-LambdaPSRuntimeDeployment -RuntimeName $Value -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'accepts a -RuntimeName such as <Value>' -ForEach @(
            @{ Value = 'rt' }, @{ Value = 'my-ps' }, @{ Value = 'my_ps.7-4' }, @{ Value = '7rt' }, @{ Value = ('r' * 55) }
        ) {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName $Value -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -WhatIf
            $result.RuntimeName | Should -Be $Value
        }

        # Lambda leaves the version or alias CREATING over these totals (measured in eu-west-1).
        It 'throws <ErrorId> when -RuntimeName and -<Parameter> total more than <Max> characters' -ForEach @(
            @{ Parameter = 'RuntimeVersion'; Value = '7.6.9-1'; Max = 63; ErrorId = 'RuntimeVersionNameTooLong' }
            @{ Parameter = 'RuntimeAlias'; Value = ('p' * 91); Max = 147; ErrorId = 'RuntimeAliasNameTooLong' }
        ) {
            $splat = @{ RuntimeName = ('r' * 57); PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'; $Parameter = $Value }

            { New-LambdaPSRuntimeDeployment @splat } |
                Should -Throw -ErrorId "$ErrorId,New-LambdaPSRuntimeDeployment" -ExpectedMessage "*total $($Max + 1) characters*"
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'throws RuntimeVersionNameTooLong for a default version name that takes the total over 63 characters' {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName 'rt' -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -WhatIf
            $name = 'r' * (64 - $result.RuntimeVersion.Length)

            { New-LambdaPSRuntimeDeployment -RuntimeName $name -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt } |
                Should -Throw -ErrorId 'RuntimeVersionNameTooLong,New-LambdaPSRuntimeDeployment' -ExpectedMessage "*version name '$($result.RuntimeVersion)' total 64 characters*"
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    # A refused alias name would otherwise surface only at the service: in the preflight when the runtime
    # exists, and at the alias stage, after the version is published, when it does not. So it is checked
    # at binding.
    Context 'runtime alias name' {
        It 'requires -RuntimeAlias, and completes it with default and the runtime''s aliases' {
            Mock 'Get-LMCRuntimeAliasList' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'test' } }
            $line = 'New-LambdaPSRuntimeDeployment -RuntimeName my-ps -Region us-east-1 -RuntimeAlias '

            $completion = TabExpansion2 -inputScript $line -cursorColumn $line.Length

            (Get-Command New-LambdaPSRuntimeDeployment).Parameters['RuntimeAlias'].Attributes.Where({ $_ -is [Parameter] }).Mandatory | Should -BeTrue
            $completion.CompletionMatches.CompletionText | Should -Be @('default', 'test')
        }

        It 'rejects a -RuntimeAlias the service refuses: <Name>' -ForEach @(
            @{ Name = 'Prod Live'; Value = 'Prod Live' }, @{ Name = 'prod-'; Value = 'prod-' }, @{ Name = '.prod'; Value = '.prod' }
            @{ Name = 'prod/live'; Value = 'prod/live' }, @{ Name = 'a trailing newline'; Value = "prod`n" }
            @{ Name = '129 characters'; Value = ('p' * 129) }
        ) {
            { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeAlias $Value -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'accepts a -RuntimeAlias such as <Value>' -ForEach @(
            @{ Value = 'default' }, @{ Value = 'p' }, @{ Value = 'Prod_2.1-a' }, @{ Value = '_live' }, @{ Value = ('p' * 128) }
        ) {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeAlias $Value -WhatIf
            $result.RuntimeAlias | Should -Be $Value
        }

        # Matched case-sensitively: ignoring case, U+212A KELVIN SIGN matches 'k' and binds, and the service
        # then refuses the name, after the image is pushed (a version) or published (an alias).
        It 'rejects a -<Parameter> with a Kelvin sign' -ForEach @(
            @{ Parameter = 'RuntimeAlias' }, @{ Parameter = 'RuntimeVersion' }
        ) {
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'; $Parameter = "prod$([char]0x212A)" }
            { New-LambdaPSRuntimeDeployment @splat -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'stage calls' {
        It 'invokes every state-changing seam exactly once in the happy path' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
            }
            $null = New-LambdaPSRuntimeDeployment @splat

            Should -Invoke 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -Times 1 -Exactly
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Path -match 'my-ps-runtime-[^\\/]+$' -and $PowerShellVersion -eq '7.4' }
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RepositoryName -eq 'rt' }
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Tag -eq "rt:$script:defaultVersion" -and -not $ImageUri }
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { -not $Tag -and $ImageUri -like "*/rt:$script:defaultVersion" }
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RuntimeName -eq 'my-ps' -and $Architecture -eq 'x86_64' -and $RuntimeAlias -eq 'default' }
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RuntimeAlias -eq 'default' }
        }
    }

    Context 'module repository' {
        It 'forwards -ModuleRepository to the scaffolder' {
            $null = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -ModuleRepository Internal
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ModuleRepository -eq 'Internal' }
        }
    }

    Context 'parameter aliases' {
        It 'does not accept -Repository or -Version as aliases of -RepositoryName and -RuntimeVersion' {
            $parameters = (Get-Command New-LambdaPSRuntimeDeployment).Parameters
            $parameters['RepositoryName'].Aliases | Should -Not -Contain 'Repository'
            $parameters['RuntimeVersion'].Aliases | Should -Not -Contain 'Version'
            { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Region us-east-1 -Version v7 -WhatIf } |
                Should -Throw -ErrorId 'NamedParameterNotFound,New-LambdaPSRuntimeDeployment'
        }

        # -RuntimeVersion is also the image tag; a value docker cannot tag would pass -WhatIf and fail
        # only at the build, after -ProjectPath was replaced.
        # It is also the runtime version name, which cannot end in '.' or '-' either.
        It 'rejects a -RuntimeVersion that is not a valid image tag and version name: <Name>' -ForEach @(
            @{ Name = 'v1+build'; Value = 'v1+build' }, @{ Name = 'v1:rc'; Value = 'v1:rc' }, @{ Name = 'v 1'; Value = 'v 1' }
            @{ Name = '-v1'; Value = '-v1' }, @{ Name = 'v1-'; Value = 'v1-' }, @{ Name = 'v1.'; Value = 'v1.' }
            @{ Name = 'a trailing newline'; Value = "v1`n" }, @{ Name = '129 characters'; Value = ('v' * 129) }
        ) {
            { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Region us-east-1 -RuntimeVersion $Value -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # A value read from a file (Get-Content -Raw) carries a trailing newline; '$' would let it through.
        It 'rejects a trailing newline in -<Parameter> at binding' -ForEach @(
            @{ Parameter = 'PowerShellVersion'; Value = "7.4`n" }
            @{ Parameter = 'PwshVersion'; Value = "7.4.20`n" }
        ) {
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt' }
            $splat[$Parameter] = $Value
            { New-LambdaPSRuntimeDeployment @splat -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment'
        }

        # The binding error says what form is expected, rather than showing the regular expression.
        It 'explains the expected form when -<Parameter> is <Value>' -ForEach @(
            @{ Parameter = 'PowerShellVersion'; Value = '7'; Expected = "'7' is not a PowerShell version in major.minor form, for example 7.4." }
            @{ Parameter = 'PwshVersion'; Value = '7.4'; Expected = "'7.4' is not a pwsh release in x.y.z form, for example 7.4.20." }
        ) {
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt' }
            $splat[$Parameter] = $Value
            $err = { New-LambdaPSRuntimeDeployment @splat -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "*$Expected*"
            $err.Exception.Message | Should -Not -BeLike '*does not match the*pattern*'
        }

        # \d also matches other scripts' digits, such as Arabic-Indic ones; a version is ASCII digits only.
        It 'rejects -<Parameter> in non-ASCII digits at binding' -ForEach @(
            @{ Parameter = 'PowerShellVersion'; Value = "$([char]0x0667).$([char]0x0664)" }
            @{ Parameter = 'PwshVersion'; Value = "7.4.$([char]0x0662)$([char]0x0660)" }
        ) {
            # The pwsh version lookup has a pattern of its own; this checks the cmdlet's.
            Mock 'Resolve-LambdaPSPwshVersion' -ModuleName $script:moduleName -RemoveParameterValidation 'PowerShellVersion' -MockWith { '7.4.20' }
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt' }
            $splat[$Parameter] = $Value
            { New-LambdaPSRuntimeDeployment @splat -WhatIf } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeDeployment'
            # Refused at binding, so before any AWS call rather than by a helper further on.
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'accepts a -RuntimeVersion such as <Value>' -ForEach @(
            @{ Value = 'v1' }, @{ Value = '7' }, @{ Value = '_v1' }, @{ Value = '2024.10.01' }, @{ Value = 'release_7-4' }
            @{ Value = 'V1.2.3-rc.1' }, @{ Value = ('a' * 58) }
        ) {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Region us-east-1 -RuntimeVersion $Value -WhatIf
            $result.ImageUri | Should -BeLike "*:$Value"
        }

        It 'offers no container-CLI choice, because the build is hardcoded to docker buildx' {
            (Get-Command New-LambdaPSRuntimeDeployment -ErrorAction Stop).Parameters.Keys |
                Should -Not -Contain 'ContainerRuntime'
        }
    }

    Context 'pwsh version' {
        It 'forwards -PwshVersion to the scaffolder and reports it' {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { '7.4.3' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion 7.4.3 -RepositoryName rt -Region us-east-1
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $PwshVersion -eq '7.4.3' }
            $result.PwshVersion | Should -Be '7.4.3'
        }

        It 'throws PwshVersionNotFound for an x86_64 -PwshVersion the package repository does not list, before the build' {
            { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion 7.4.99 -RepositoryName rt -Region us-east-1 } |
                Should -Throw -ErrorId 'PwshVersionNotFound,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'reports the default pwsh release for -PowerShellVersion when -PwshVersion is omitted' {
            $default = InModuleScope $script:moduleName { $script:Config.pwsh.DefaultPatch['7.4'] }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.PwshVersion | Should -Be $default
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $PwshVersion -eq $default }
        }

        It 'throws UnknownPwshVersion for an unmapped line before any toolchain or AWS check' {
            { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.9 -RepositoryName rt -Region us-east-1 } |
                Should -Throw -ErrorId 'UnknownPwshVersion,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'one-step patching' {
        It 'installs the newest release in the package repository on x86_64, and names the version after it' {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { '7.4.21', '7.4.99' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.PwshVersion | Should -Be '7.4.99'
            $result.RuntimeVersion | Should -Be '7.4.99-1'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $PwshVersion -eq '7.4.99' }
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $RuntimeVersion -eq '7.4.99-1' -and $ReleaseNote -eq 'pwsh 7.4.99'
            }
        }

        It 'installs a pinned release on arm64, and warns that a newer one exists' {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { '7.4.99' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Architecture arm64 -RepositoryName rt -Region us-east-1 -WarningVariable warnings -WarningAction SilentlyContinue
            $result.PwshVersion | Should -Be $script:pwshConfig.DefaultPatch['7.4']
            "$warnings" | Should -BeLike '*pwsh 7.4.99 is the newest*'
        }

        It 'falls back to the pinned release, with a warning, when the package repository cannot be read' {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { throw 'offline' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -WarningVariable warnings -WarningAction SilentlyContinue
            $result.PwshVersion | Should -Be $script:pwshConfig.DefaultPatch['7.4']
            "$warnings" | Should -BeLike '*pinned release*offline*'
        }

        It 'throws PwshReleaseOlderThanAlias, before anything is built, when the pinned fallback is older than the alias''s release' {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { throw 'offline' }
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'default'; RuntimeVersion = '7.4.999-1' } }
            $err = { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'PwshReleaseOlderThanAlias,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike '*Pass -PwshVersion 7.4.999 to keep it*Nothing was built or pushed.'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'throws PwshReleaseOlderThanAlias when the alias''s version, named with -RuntimeVersion, has a newer release in its release note' {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { throw 'offline' }
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'prod-2' } }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -ParameterFilter { $Name -eq 'prod-2' } -MockWith { [PSCustomObject]@{ Name = 'prod-2'; ReleaseNotes = 'pwsh 7.4.999' } }
            { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -WarningAction SilentlyContinue } |
                Should -Throw -ErrorId 'PwshReleaseOlderThanAlias,New-LambdaPSRuntimeDeployment'
        }

        It 'deploys the pinned release when the alias runs a newer release <Case>' -ForEach @(
            @{ Case = 'of another PowerShell line'; Release = '7.6.99'; Architectures = @('x86_64') }
            @{ Case = 'on another architecture'; Release = '7.4.999'; Architectures = @('arm64') }
        ) {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { throw 'offline' }
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = "$Release-1"; Compatibility = [PSCustomObject]@{ Architectures = $Architectures } }
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -WarningAction SilentlyContinue
            $result.PwshVersion | Should -Be $script:pwshConfig.DefaultPatch['7.4']
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'publishes the pinned release''s -2 on an arm64 rerun when the alias is on its -1' {
            $pin = $script:pwshConfig.DefaultPatch['7.4']
            Mock 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = "$pin-1" } }
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = "$pin-1"; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -Architecture arm64 -RepositoryName rt -Region us-east-1 -WarningAction SilentlyContinue
            $result.RuntimeVersion | Should -Be "$pin-2"
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'moves the alias back to an older release given with -PwshVersion' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'default'; RuntimeVersion = '7.4.999-1' } }
            $pin = $script:pwshConfig.DefaultPatch['7.4']
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion $pin -RepositoryName rt -Region us-east-1
            $result.RuntimeVersion | Should -Be "$pin-1"
            $result.PreviousRuntimeVersion | Should -Be '7.4.999-1'
        }

        It 'names the version one past the highest suffix the runtime has for the release' {
            Mock 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -MockWith {
                $release = $script:pwshConfig.DefaultPatch['7.4']
                "$release-1", "$release-3", '7.4.1-9', "$release-x", 'v1' | ForEach-Object { [PSCustomObject]@{ Name = $_ } }
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.RuntimeVersion | Should -Be "$($script:pwshConfig.DefaultPatch['7.4'])-4"
            Should -Invoke 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Name -eq $result.RuntimeVersion }
        }

        It 'skips a suffix whose image tag is left in the repository, with one image list' {
            $release = $script:pwshConfig.DefaultPatch['7.4']
            Mock 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = "$release-1" } }
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -ParameterFilter { -not $ImageId } -MockWith {
                [PSCustomObject]@{ ImageTags = @("$release-1") }, [PSCustomObject]@{ ImageTags = @("$release-2", 'latest') }
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.RuntimeVersion | Should -Be "$release-3"
            Should -Invoke 'Get-ECRImageMetadata' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RepositoryName -eq 'rt' -and $Filter_TagStatus -eq 'TAGGED' -and $Filter_ImageStatus -eq 'ANY' -and -not $ImageId }
        }

        It 'skips a tag left in the repository for the first version of a new runtime' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -ParameterFilter { -not $ImageId } -MockWith { [PSCustomObject]@{ ImageTags = @($script:defaultVersion) } }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.RuntimeVersion | Should -Be "$($script:pwshConfig.DefaultPatch['7.4'])-2"
        }

        It 'names the version from the runtime alone when the repository does not exist yet' {
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -ParameterFilter { -not $ImageId } -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('no repository'))
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.RuntimeVersion | Should -Be $script:defaultVersion
        }

        It 'throws RuntimeImageTagCheckFailed, before anything is built, when the image tags cannot be listed' {
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -ParameterFilter { -not $ImageId } -MockWith {
                throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform: ecr:DescribeImages')
            }
            $err = { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 } |
                Should -Throw -ErrorId 'RuntimeImageTagCheckFailed,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "*image tags of ECR repository 'rt'*ecr:DescribeImages. Nothing was built or pushed."
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'uses an explicit -RuntimeVersion as given, without listing versions' {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -RuntimeVersion v7
            $result.RuntimeVersion | Should -Be 'v7'
            Should -Invoke 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Get-ECRImageMetadata' -ModuleName $script:moduleName -Times 0 -Exactly -ParameterFilter { -not $ImageId }
        }

        It 'names the first version of a new runtime after the pwsh release with -1, without listing versions' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            $result.RuntimeVersion | Should -Be $script:defaultVersion
            Should -Invoke 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'throws RuntimeVersionListFailed, before anything is built, when the versions cannot be listed' {
            Mock 'Get-LMCRuntimeVersionList' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:ListRuntimeVersions') }
            $err = { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 } |
                Should -Throw -ErrorId 'RuntimeVersionListFailed,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike '*lambda:ListRuntimeVersions. Pass -RuntimeVersion*Nothing was built or pushed.'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'returns the version the alias moved from' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = '7.4.20-2'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 6>$null
            $result.PreviousRuntimeVersion | Should -Be '7.4.20-2'
        }
    }

    # An arm64 image installs pwsh from a tarball checked against a pinned SHA256. Without one the build
    # can only fail, after the scaffold; so the preflight refuses it.
    Context 'arm64 pwsh hash' {
        It 'refuses arm64 for a pwsh version with no pinned arm64 hash, under -WhatIf too, before scaffolding and printing no release' -ForEach @(
            @{ WhatIf = $true }, @{ WhatIf = $false }
        ) {
            $project = [System.IO.Path]::Combine($TestDrive, "arm64-$WhatIf")

            $err = $null
            try { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion 7.4.13 -Architecture arm64 -Region us-east-1 -RepositoryName rt -ProjectPath $project -WhatIf:$WhatIf -InformationVariable info }
            catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'PwshArm64HashNotPinned,New-LambdaPSRuntimeDeployment'
            "$info" | Should -Not -BeLike '*installs pwsh*'
            $err.Exception.Message | Should -BeLike '*7.4.13*-Architecture x86_64*-PwshVersion*Nothing was created.'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # The advice names only versions -PowerShellVersion accepts; another line's would fail with PwshVersionMismatch.
        It 'names only pinned arm64 versions of the same PowerShell line in the advice' {
            # Read from Config/pwsh.psd1, so a pin bump does not break the test.
            $pinned74 = InModuleScope $script:moduleName {
                @($script:Config.pwsh.ArchiveSha256.Keys | Where-Object { $_.StartsWith('7.4.') } | Sort-Object { [version]$_ }) -join ', '
            }
            $err = { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion 7.4.13 -Architecture arm64 -Region us-east-1 -RepositoryName rt -WhatIf } |
                Should -Throw -ErrorId 'PwshArm64HashNotPinned,New-LambdaPSRuntimeDeployment' -PassThru
            $pinned74 | Should -Not -BeNullOrEmpty
            $err.Exception.Message | Should -BeLike "*a -PwshVersion with a pinned arm64 hash ($pinned74). Nothing was created."
            $err.Exception.Message | Should -Not -Match '7\.[56]\.'
            $err.CategoryInfo.Category | Should -Be ([System.Management.Automation.ErrorCategory]::InvalidArgument)
        }

        It 'leaves the version list out when no version of the line has a pinned arm64 hash' {
            Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName $script:moduleName -ParameterFilter { $Architecture -eq 'arm64' } -MockWith { $null }
            $err = { New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion 7.4.20 -Architecture arm64 -Region us-east-1 -RepositoryName rt -WhatIf } |
                Should -Throw -ErrorId 'PwshArm64HashNotPinned,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike '*Use -Architecture x86_64, or a -PwshVersion with a pinned arm64 hash. Nothing was created.'
        }

        It 'deploys <Case>' -ForEach @(
            @{ Case = 'arm64 for a pwsh version with a pinned arm64 hash'; PwshVersion = '7.4.20'; Architecture = 'arm64' }
            @{ Case = 'x86_64 for a pwsh version with no pinned arm64 hash'; PwshVersion = '7.4.13'; Architecture = 'x86_64' }
        ) {
            Mock 'Get-LambdaPSPwshRepositoryRelease' -ModuleName $script:moduleName -MockWith { '7.4.13', '7.4.20' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -PwshVersion $PwshVersion -Architecture $Architecture -Region us-east-1 -RepositoryName rt -WarningAction SilentlyContinue
            $result.PwshVersion | Should -Be $PwshVersion
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly
        }
    }

    Context 'region' {
        It 'uses the region AWS.Tools would use when -Region is omitted' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { 'ap-southeast-2' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt

            $result.Region | Should -Be 'ap-southeast-2'
            $result.ImageUri | Should -Be "111122223333.dkr.ecr.ap-southeast-2.amazonaws.com/rt:$script:defaultVersion"
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'ap-southeast-2' }
        }

        # Region names are lowercase, and the ECR pull grant must match the runtime ARN Lambda sends,
        # so a region given in another case is lowercased before anything is built or created.
        It 'lowercases a -Region given as <Given>' -ForEach @(
            @{ Given = 'EU-WEST-1' }, @{ Given = 'Eu-West-1' }
        ) {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region $Given

            $result.Region | Should -BeExactly 'eu-west-1'
            $result.ImageUri | Should -BeExactly "111122223333.dkr.ecr.eu-west-1.amazonaws.com/rt:$script:defaultVersion"
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $RuntimeArn -ceq 'arn:aws:lambda:eu-west-1:111122223333:runtime/my-ps' -and $Region -ceq 'eu-west-1'
            }
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -ceq 'eu-west-1' }
        }

        It 'lowercases a resolved region too' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { 'AP-SOUTHEAST-2' }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt
            $result.Region | Should -BeExactly 'ap-southeast-2'
        }

        # A deploy script that sets its own default region and credentials (in its own scope, not the
        # global one) gets them used, as any AWS.Tools cmdlet it calls would.
        It 'uses the region and credentials a calling script set with Set-DefaultAWSRegion and Set-AWSCredential' {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [CmdletBinding()] param($Region, $ClientConfig, $Credential, $ProfileName, $ProfileLocation)
                    $script:LambdaPSCalls.Add("STS region=$Region credential=$($null -ne $Credential)")
                    [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-LMCRuntime { [CmdletBinding()] param($RuntimeName, $Region, $ClientConfig, $Credential, $ProfileName, $ProfileLocation)
                    $script:LambdaPSCalls.Add("GetRuntime region=$Region credential=$($null -ne $Credential)")
                    throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist')) }
                function script:New-LambdaPSRuntimeProject { }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, 'script-scope-rt')
            # A script block's local scope, as in a function of the script: AWS.Tools reads global settings
            # itself, so only a non-global scope needs the caller's session state.
            $command = "& { Set-DefaultAWSRegion -Region ap-northeast-3 -Scope Local; Set-AWSCredential -AccessKey AKIAEXAMPLE -SecretKey example -Scope Local; " +
                "`$r = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -RepositoryName rt -ProjectPath '$project' -WhatIf; 'REGION=' + `$r.Region }"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command

            $result.Output | Should -BeLike '*REGION=ap-northeast-3*'
            $result.Calls | Should -Contain 'STS region=ap-northeast-3 credential=True'
            $result.Calls | Should -Contain 'GetRuntime region=ap-northeast-3 credential=True'
        }

        It 'composes the image URI with the partition''s DNS suffix' {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region cn-north-1
            $result.ImageUri | Should -Be "111122223333.dkr.ecr.cn-north-1.amazonaws.com.cn/rt:$script:defaultVersion"
        }

        It 'does not resolve a region when -Region is given' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { throw 'must not resolve' }
            $null = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1
            Should -Invoke 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'image digest and pull grant' {
        It 'publishes the version by the digest of the image it pushed' {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1

            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $ImageUri -eq '111122223333.dkr.ecr.us-east-1.amazonaws.com/rt@sha256:abc123'
            }
            $result.ImageUri | Should -Be "111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:$script:defaultVersion"
            $result.ImageDigest | Should -Be 'sha256:abc123'
        }

        It 'warns and publishes by tag when buildx reports no digest' {
            Mock 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ ImageUri = $ImageUri; Pushed = [bool]$ImageUri }
            }
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -WarningVariable warnings -WarningAction SilentlyContinue

            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $ImageUri -eq "111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:$script:defaultVersion"
            }
            $result.ImageDigest | Should -BeNullOrEmpty
            ($warnings -join ' ') | Should -BeLike '*did not report the pushed image''s digest*'
        }

        It 'scopes the ECR pull grant to the runtime''s ARN' {
            $null = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region cn-north-1
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $AccountId -eq '111122223333' -and $RuntimeArn -eq 'arn:aws-cn:lambda:cn-north-1:111122223333:runtime/my-ps'
            }
        }
    }

    Context 'confirmation' {
        It 'asks once per stage: every call inside a stage runs with -Confirm:$false' {
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt' }
            $null = New-LambdaPSRuntimeDeployment @splat

            $noConfirm = { $Confirm -eq $false }
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $noConfirm
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 2 -Exactly -ParameterFilter $noConfirm
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $noConfirm
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $noConfirm
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $noConfirm
        }
    }

    Context 'credential parameters' {
        It 'passes -ProfileName to every seam and AWS call' {
            # A credential file of the test's own that defines the profile.
            $credentialFile = [System.IO.Path]::Combine($TestDrive, 'credentials')
            Set-Content -LiteralPath $credentialFile -Value "[dev]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'; ProfileName = 'dev'; ProfileLocation = $credentialFile }
            $null = New-LambdaPSRuntimeDeployment @splat

            $hasProfile = { $CredentialParameter.ProfileName -eq 'dev' }
            Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'dev' }
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'dev' }
            Should -Invoke 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'dev' }
            Should -Invoke 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'dev' }
        }
    }

    Context 'alias parameter' {
        It 'accepts the canonical -RuntimeAlias and points it at the version' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'
                RuntimeAlias = 'staging'
            }
            $result = New-LambdaPSRuntimeDeployment @splat
            $result.RuntimeAlias | Should -Be 'staging'
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RuntimeAlias -eq 'staging' }
        }
    }

    Context 'alias failure' {
        BeforeEach {
            $script:aliasSplat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'; RuntimeVersion = 'v7' }
            Mock 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('User is not authorized to perform lambda:UpdateRuntimeAlias') }
        }

        It 'fails with RuntimeAliasUpdateFailed naming <Command> when the alias <Case>' -TestCases @(
            @{ Case = 'does not exist'; Exists = $false; Command = 'New-LMCRuntimeAlias' }
            @{ Case = 'exists'; Exists = $true; Command = 'Update-LMCRuntimeAlias' }
        ) {
            if ($Exists) {
                Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                    [PSCustomObject]@{ RuntimeVersion = 'v6'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
                }
            }
            $err = { New-LambdaPSRuntimeDeployment @script:aliasSplat } | Should -Throw -ErrorId 'RuntimeAliasUpdateFailed,New-LambdaPSRuntimeDeployment' -PassThru

            $err.Exception.Message | Should -Be ("Runtime 'my-ps' version 'v7' is published and ACTIVE, but alias 'default' could not be " +
                "pointed at it. The version exists, so running the deploy again is refused; point the alias with $Command " +
                "-RuntimeName my-ps -Name default -RuntimeVersion v7 -Region us-east-1. User is not authorized to perform " +
                "lambda:UpdateRuntimeAlias")
            $err.Exception.InnerException | Should -BeOfType ([LambdaPSTests.Fakes.AccessDeniedException])
            $err.CategoryInfo.Category | Should -Be 'WriteError'
            $err.TargetObject | Should -Be 'my-ps/default'
        }
    }

    Context 'architecture mapping' {
        It 'maps arm64 to linux/arm64 for the build' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'; Architecture = 'arm64'
            }
            $result = New-LambdaPSRuntimeDeployment @splat

            $result.Architecture | Should -Be 'arm64'
            # Both the local build and the push build target the platform.
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 2 -Exactly `
                -ParameterFilter { $Architecture -eq 'arm64' }
        }

        # A wrong-architecture image is caught at publish; the alias must not then be moved onto it.
        It 'does not point the alias when publishing rejects the image architecture' {
            Mock 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new('image is [x86_64]'),
                    'RuntimeImageArchitectureMismatch', [System.Management.Automation.ErrorCategory]::InvalidResult, 'v1')
            }
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'; Architecture = 'arm64'
            }

            { New-LambdaPSRuntimeDeployment @splat } | Should -Throw -ErrorId 'RuntimeImageArchitectureMismatch,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # The build platform alone is only half of it: the published version must DECLARE the
        # architecture too, or the function deploy has nothing to check a mismatch against.
        It 'declares arm64 on the published runtime version' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'; Architecture = 'arm64'
            }
            $null = New-LambdaPSRuntimeDeployment @splat

            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Architecture -eq 'arm64' }
        }
    }

    Context 'WhatIf' {
        It 'invokes no state-changing seam under -WhatIf' {
            $splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'
                Region = 'us-east-1'; RepositoryName = 'rt'
                Module = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            $null = New-LambdaPSRuntimeDeployment @splat -WhatIf

            # The scaffold is previewed (so its checks run) but never run for real.
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $WhatIf }
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly -ParameterFilter { -not $WhatIf }
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # What if lines go to the host, so the preview is read from a child pwsh's console.
        It 'names the image URI, the version and the alias in the What if lines' {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist')) }
                function script:New-LambdaPSRuntimeProject { }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, 'whatif-rt')
            $command = "New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v7 -ProjectPath '$project' -WhatIf"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command

            $whatIf = @($result.Output -split "`n" | Where-Object { $_ -like 'What if:*' })
            # The stage text names every change: the pull grant in the repository policy, and a runtime it creates.
            $whatIf | Should -Contain 'What if: Performing the operation "Build image, ensure ECR repository ''rt'' and its Lambda pull grant, and push image" on target "111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v7".'
            $whatIf | Should -Contain 'What if: Performing the operation "Create runtime ''my-ps'' and publish Lambda runtime version ''v7'' from image ''111122223333.dkr.ecr.us-east-1.amazonaws.com/rt:v7''" on target "my-ps".'
            $whatIf | Should -Contain 'What if: Performing the operation "Point alias at v7" on target "my-ps/default".'
        }

        It 'drops the scaffold advice to pin an arm64 hash, and passes on its other warnings <Case>' -ForEach @(
            @{ Case = 'when the scaffold succeeds'; Fail = '' }
            @{ Case = 'when the scaffold then fails'; Fail = "throw 'scaffold failed'" }
        ) {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
                # An advanced function, so -WarningAction and -WarningVariable apply as they do to the real scaffold.
                function script:New-LambdaPSRuntimeProject {
                    [CmdletBinding(SupportsShouldProcess)]
                    param($Path, $PowerShellVersion, $PwshVersion, $IncludeModule, $MissingModuleAction, [switch]$Force)
                    Write-Warning 'No SHA256 is pinned for the pwsh 7.4.99 arm64 tarball, so the scaffolded ''Dockerfile'' can build linux/amd64 only.'
                    Write-Warning 'other scaffold warning'
                    #FAIL
                }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, "scaffold-warnings-$([bool]$Fail)")
            $command = "`$null = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -ProjectPath '$project' -WhatIf"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString().Replace('#FAIL', $Fail)) -Command $command

            $result.Output | Should -BeLike '*other scaffold warning*'
            if ($Fail) { $result.Output | Should -BeLike '*ERROR: scaffold failed*' }
            $result.Output | Should -Not -BeLike '*arm64 tarball*'
        }

        It '<Case>' -ForEach @(
            @{ Case = 'prints the pwsh release, and names the version the alias moves from under -WhatIf'; WhatIf = $true; Extra = '' }
            @{ Case = 'does not print the pwsh release when -InformationAction SilentlyContinue is given'; WhatIf = $false; Extra = ' -InformationAction SilentlyContinue' }
        ) {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'my-ps' } }
                function script:Get-LMCRuntimeVersion { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')) }
                function script:Get-LMCRuntimeAlias { [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'v6' } }
                function script:New-LambdaPSRuntimeProject { }
                function script:Get-LambdaPSStagedModule { }
                function script:Build-LambdaPSRuntimeImage { }
                function script:Initialize-LambdaPSEcrRepository { }
                function script:Publish-LambdaPSRuntimeVersion { [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v7' } }
                function script:Set-LambdaPSRuntimeAlias { }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, "information-$WhatIf")
            $command = "`$null = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v7 -ProjectPath '$project' -WarningAction SilentlyContinue -WhatIf:`$$WhatIf$Extra"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command

            $result.Output | Should -Not -BeLike '*ERROR:*'
            if ($Extra) { $result.Output | Should -Not -BeLike '*installs pwsh*' }
            else {
                $result.Output | Should -BeLike '*Preflight: the image installs pwsh 7.4.*'
                $result.Output | Should -BeLike '*What if: Performing the operation "Point alias at v7 (from v6)" on target "my-ps/default".*'
            }
        }

        # A runtime that exists is published to, not created.
        It 'words the scaffold and publish stages for <Case>' -ForEach @(
            @{ Case = 'an existing empty directory and an existing runtime'; Scaffold = 'Scaffold PowerShell 7.4 runtime project'; Publish = 'Publish Lambda runtime version' }
        ) {
            $project = [System.IO.Path]::Combine($TestDrive, 'words')
            $null = New-Item -ItemType Directory -Path $project
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'my-ps' } }
                function script:Get-LMCRuntimeVersion { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')) }
                function script:Get-LMCRuntimeAlias { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime alias does not exist')) }
                function script:New-LambdaPSRuntimeProject { }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $command = "New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v7 -ProjectPath '$project' -WhatIf"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command

            $whatIf = @($result.Output -split "`n" | Where-Object { $_ -like 'What if:*' })
            $whatIf | Should -Contain "What if: Performing the operation `"$Scaffold`" on target `"$project`"."
            @($whatIf | Where-Object { $_ -like "*`"$Publish 'v7' from image*" }).Count | Should -Be 1
        }

        # Declining a middle stage skips the stages after it, with a warning for each, and leaves their
        # fields empty; declining the alias stage alone says how to point the alias later.
        It 'runs <Ran> and warns about the rest when -Confirm is answered <Answers>' -ForEach @(
            @{ Answers = @('Y', 'N'); Ran = 'Scaffold'; Warnings = @('Skipped publishing the runtime version, because the build and push stage was declined', "Skipped pointing alias 'default' at 'v7', because the build and push stage was declined") }
            @{ Answers = @('Y', 'Y', 'N'); Ran = 'Scaffold,Build,Repository,Push'; Warnings = @("Skipped pointing alias 'default' at 'v7', because the publish stage was declined") }
            @{ Answers = @('Y', 'Y', 'Y', 'N'); Ran = 'Scaffold,Build,Repository,Push,Publish'; Warnings = @("Alias 'default' was not pointed at 'v7', because that stage was declined. Point it later with New-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v7 -Region us-east-1.") }
        ) {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist')) }
                function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
                function script:Get-LambdaPSStagedModule { }
                function script:Build-LambdaPSRuntimeImage { param($Path, $Architecture, $Tag, $ImageUri)
                    $script:LambdaPSCalls.Add($(if ($ImageUri) { 'Push' } else { 'Build' }))
                    if ($ImageUri) { [PSCustomObject]@{ ImageUri = $ImageUri; ImageDigest = 'sha256:abc123' } } }
                function script:Initialize-LambdaPSEcrRepository { $script:LambdaPSCalls.Add('Repository') }
                function script:Publish-LambdaPSRuntimeVersion { $script:LambdaPSCalls.Add('Publish'); [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v7' } }
                function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, "decline-$($Answers.Count)")
            $command = "`$r = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v7 -ProjectPath '$project' -Confirm; 'ARN=' + `$r.RuntimeArn"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command -Answer $Answers

            ($result.Calls -join ',') | Should -Be $Ran
            $output = $result.Output -replace '\s+', ' '
            foreach ($warning in $Warnings) { $output | Should -BeLike "*$warning*" }
            if ($Ran -notlike '*Publish*') { $output | Should -BeLike '*ARN= *' }
        }

        # The preflight read says whether the alias exists, so the warning names the one command that applies.
        It 'names only <AliasCommand> when the alias stage is declined and the alias <Case>' -ForEach @(
            @{ Case = 'exists'; AliasCommand = 'Update-LMCRuntimeAlias'; Other = 'New-LMCRuntimeAlias'; Alias = '[PSCustomObject]@{ Name = ''default''; RuntimeVersion = ''v6'' }' }
            @{ Case = 'does not exist'; AliasCommand = 'New-LMCRuntimeAlias'; Other = 'Update-LMCRuntimeAlias'; Alias = 'throw [System.InvalidOperationException]::new(''not found'', [LambdaPSTests.Fakes.ResourceNotFoundException]::new(''Runtime alias does not exist''))' }
        ) {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'my-ps' } }
                function script:Get-LMCRuntimeVersion { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')) }
                function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
                function script:Get-LambdaPSStagedModule { }
                function script:Build-LambdaPSRuntimeImage { param($Path, $Architecture, $Tag, $ImageUri)
                    if ($ImageUri) { [PSCustomObject]@{ ImageUri = $ImageUri; ImageDigest = 'sha256:abc123' } } }
                function script:Initialize-LambdaPSEcrRepository { }
                function script:Publish-LambdaPSRuntimeVersion { $script:LambdaPSCalls.Add('Publish'); [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v7' } }
                function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, "decline-alias-$($AliasCommand)")
            $command = "`$null = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v7 -ProjectPath '$project' -Confirm"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString() + "`nfunction script:Get-LMCRuntimeAlias { $Alias }") -Command $command -Answer 'Y', 'Y', 'Y', 'N'

            ($result.Calls -join ',') | Should -Be 'Scaffold,Publish'
            $output = $result.Output -replace '\s+', ' '
            $output | Should -BeLike "*Point it later with $AliasCommand -RuntimeName my-ps -Name default -RuntimeVersion v7 -Region us-east-1.*"
            $output | Should -Not -BeLike "*$Other*"
        }

        # Under -Confirm each stage asks once; accepting them all runs every stage, in order, and the version is
        # published by the digest the push reported. The prompt text itself is not read: a redirected console
        # does not show it on every platform.
        It 'runs every stage in order, publishing by the pushed digest, when each -Confirm prompt is accepted' {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist')) }
                function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
                function script:Get-LambdaPSStagedModule { }
                function script:Build-LambdaPSRuntimeImage { param($Path, $Architecture, $Tag, $ImageUri)
                    $script:LambdaPSCalls.Add($(if ($ImageUri) { 'Push' } else { 'Build' }))
                    if ($ImageUri) { [PSCustomObject]@{ ImageUri = $ImageUri; ImageDigest = 'sha256:abc123' } } }
                function script:Initialize-LambdaPSEcrRepository { $script:LambdaPSCalls.Add('Repository') }
                function script:Publish-LambdaPSRuntimeVersion { param($RuntimeName, $ImageUri) $script:LambdaPSCalls.Add("Publish $ImageUri") }
                function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, 'confirm-rt')
            $command = "New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v7 -ProjectPath '$project' -Confirm"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command -Answer 'Y', 'Y', 'Y', 'Y'

            $result.Calls | Should -Be @('Scaffold', 'Build', 'Repository', 'Push',
                'Publish 111122223333.dkr.ecr.us-east-1.amazonaws.com/rt@sha256:abc123', 'Alias')
        }

        It 'still runs the read-only runtime preflight under -WhatIf' {
            $splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt' }
            $null = New-LambdaPSRuntimeDeployment @splat -WhatIf

            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 1 -Exactly
            Should -Invoke 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly
        }
    }

    # Without these checks, each problem would surface only at publish, after the scaffold, the ECR
    # repository and the image push already exist.
    Context 'checks before side effects' {
        BeforeEach {
            $script:splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt' }
        }

        It 'throws RuntimeServiceAccessDenied and creates nothing when Lambda refuses GetRuntime (wrong region)' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith {
                throw [System.InvalidOperationException]::new('Unable to determine service/operation name to be authorized',
                    [LambdaPSTests.Fakes.AccessDeniedException]::new('Unable to determine service/operation name to be authorized'))
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeServiceAccessDenied,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -Match 'us-east-1'
            $err.Exception.Message | Should -Match 'may not be available in us-east-1'
            $err.Exception.Message | Should -Match 'lambda:GetRuntime'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'fails the same way under -WhatIf, rather than previewing a deploy that cannot succeed' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith {
                throw [LambdaPSTests.Fakes.AccessDeniedException]::new('denied')
            }
            { New-LambdaPSRuntimeDeployment @script:splat -WhatIf } |
                Should -Throw -ErrorId 'RuntimeServiceAccessDenied,New-LambdaPSRuntimeDeployment'
        }

        It 'throws RuntimeVersionExists and builds nothing when the runtime already has -RuntimeVersion' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                # State is an SDK ConstantClass, which converts to its Value string.
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE' }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -Match "version 'v1' \(ACTIVE\)"
            # A run that published v1 but failed at the alias stage is finished with the alias command.
            $err.Exception.Message | Should -BeLike '*Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v1 -Region us-east-1*'
            Should -Invoke 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RuntimeName -eq 'my-ps' -and $Name -eq 'v1' -and $Region -eq 'us-east-1' }
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'throws RuntimeImageTagExists and builds nothing when the repository already has the -RuntimeVersion tag' {
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ ImageTags = @('v1') } }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeImageTagExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike "*'rt' in region 'us-east-1' already has an image tagged 'v1'*"
            # Removing a version leaves its image, so the message names the command that frees the tag.
            $err.Exception.Message | Should -BeLike "*Remove-ECRImageBatch -RepositoryName rt -ImageId @{ ImageTag = 'v1' } -Region us-east-1."
            Should -Invoke 'Get-ECRImageMetadata' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RepositoryName -eq 'rt' -and $ImageId[0].ImageTag -eq 'v1' -and $Region -eq 'us-east-1' }
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'deploys when the repository does not exist yet' {
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('no repository'))
            }

            $null = New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1

            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 2 -Exactly
        }

        It 'throws RuntimeImageTagCheckFailed and builds nothing when the image tag cannot be read' {
            Mock 'Get-ECRImageMetadata' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform: ecr:DescribeImages') }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeImageTagCheckFailed,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike "*'rt' in region 'us-east-1' to check that it has no image tagged 'v1': not authorized to perform: ecr:DescribeImages. Nothing was built or pushed."
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # A run that stopped with RuntimeImageArchitectureMismatch leaves an ACTIVE version of the wrong
        # architecture; moving the alias onto it is exactly what that error prevented.
        It 'names no alias command for an existing version whose image is another architecture' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -Architecture arm64 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -Not -BeLike '*Update-LMCRuntimeAlias*'
            # The main sentence already says to pass a new -RuntimeVersion; the advice adds only the fact.
            $err.Exception.Message | Should -BeLike "*untouched. Its image is [[]x86_64], not 'arm64'."
            ([regex]::Matches($err.Exception.Message, 'new -RuntimeVersion')).Count | Should -Be 1
            $err.Exception.Message | Should -Not -BeLike '*do not point an alias*'
        }

        # Publish warns, but carries on, for a version that reports no architecture; so does this advice.
        It 'names the alias command for an existing ACTIVE version that reports no architecture' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @() } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -Architecture arm64 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike '*Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v1*'
        }

        It 'names no alias command for an existing version that is FAILED' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'FAILED'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -Not -BeLike '*Update-LMCRuntimeAlias*'
            $err.Exception.Message | Should -Not -BeLike '*still being created*'
        }

        # Running again is refused, so a version an earlier run left in CREATING gets the commands that finish it.
        It 'says an existing CREATING version is still being created and gives the commands to finish it' {
            $credentialFile = [System.IO.Path]::Combine($TestDrive, 'credentials')
            Set-Content -LiteralPath $credentialFile -Value "[dev]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'CREATING'; Compatibility = [PSCustomObject]@{ Architectures = @() } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -Architecture arm64 -ProfileName dev -ProfileLocation $credentialFile } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike "*version 'v1' (CREATING)*"
            $err.Exception.Message | Should -BeLike "*untouched. It is still being created; check with Get-LMCRuntimeVersion -RuntimeName my-ps -Name v1 -Region us-east-1 -ProfileName 'dev' -ProfileLocation '$credentialFile'. Once it is ACTIVE and its Compatibility.Architectures is 'arm64', point the alias at it with Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v1 -Region us-east-1 -ProfileName 'dev' -ProfileLocation '$credentialFile' (New-LMCRuntimeAlias if the alias does not exist yet)."
        }

        # An alias already on the existing version needs no command; one on another version still gets it.
        It 'names no alias command when the alias already points at the existing ACTIVE version' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'v1'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike "*untouched. Alias 'default' already points at it."
            $err.Exception.Message | Should -Not -BeLike '*LMCRuntimeAlias*'
        }

        It 'names the alias command when the alias points at <Case>' -ForEach @(
            @{ Case = 'another version'; AliasVersion = 'v0' }
            @{ Case = 'a version whose name differs only in case'; AliasVersion = 'V1' }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = $AliasVersion; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.Exception.Message | Should -BeLike '*run Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v1 -Region us-east-1*'
            $err.Exception.Message | Should -Not -BeLike '*already points at it*'
        }

        # The service's text is one sentence of the message, ended with a full stop whether or not it has one.
        It 'separates the service''s text from the rest of the <Id> message when it <Case>' -ForEach @(
            @{ Id = 'RuntimeVersionCheckFailed'; Case = 'has no full stop'; Text = 'not authorized to perform lambda:GetRuntimeVersion' }
            @{ Id = 'RuntimeVersionCheckFailed'; Case = 'ends with one'; Text = 'Rate exceeded.' }
            @{ Id = 'RuntimeAliasCheckFailed'; Case = 'has no full stop'; Text = 'not authorized to perform lambda:GetRuntimeAlias' }
            @{ Id = 'RuntimeAliasCheckFailed'; Case = 'ends with one'; Text = 'Rate exceeded.' }
        ) {
            $failing = if ($Id -eq 'RuntimeVersionCheckFailed') { 'Get-LMCRuntimeVersion' } else { 'Get-LMCRuntimeAlias' }
            Mock $failing -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new($Text) }
            $err = { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } | Should -Throw -ErrorId "$Id,New-LambdaPSRuntimeDeployment" -PassThru

            $err.Exception.Message | Should -BeLike "*: $($Text.TrimEnd('.')). Nothing was built or pushed."
            $err.Exception.Message | Should -Not -BeLike '*..*'
        }

        # A service message that is empty adds nothing; one that ends in a stop inside quotes or brackets is
        # already a sentence.
        It 'words the <Id> message for service text that <Case>' -ForEach @(
            @{ Id = 'RuntimeVersionCheckFailed'; Case = 'is empty'; Text = ''; Ending = 'to check that it is new. Nothing was built or pushed.' }
            @{ Id = 'RuntimeVersionCheckFailed'; Case = 'is only spaces'; Text = '   '; Ending = 'to check that it is new. Nothing was built or pushed.' }
            @{ Id = 'RuntimeVersionCheckFailed'; Case = 'ends in a quoted stop'; Text = 'User said "no."'; Ending = ': User said "no." Nothing was built or pushed.' }
            @{ Id = 'RuntimeAliasCheckFailed'; Case = 'is empty'; Text = ''; Ending = 'to check its architecture. Nothing was built or pushed.' }
            @{ Id = 'RuntimeAliasCheckFailed'; Case = 'ends in a bracketed stop'; Text = 'Denied (see the policy.)'; Ending = ': Denied (see the policy.) Nothing was built or pushed.' }
        ) {
            $failing = if ($Id -eq 'RuntimeVersionCheckFailed') { 'Get-LMCRuntimeVersion' } else { 'Get-LMCRuntimeAlias' }
            Mock $failing -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new($Text) }
            $err = { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } | Should -Throw -ErrorId "$Id,New-LambdaPSRuntimeDeployment" -PassThru

            $err.Exception.Message.EndsWith($Ending) | Should -BeTrue -Because $err.Exception.Message
        }

        # The version check comes first, so a refused version read stops the deploy before the alias is read.
        It 'stops with RuntimeVersionCheckFailed, without reading the alias, when both reads would be refused' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('denied') }
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('denied') }
            { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } |
                Should -Throw -ErrorId 'RuntimeVersionCheckFailed,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'reads the alias for the RuntimeVersionExists advice with the caller''s region, client config and profile' {
            $credentialFile = [System.IO.Path]::Combine($TestDrive, 'credentials')
            Set-Content -LiteralPath $credentialFile -Value "[dev]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -Region eu-central-1 -ProfileName dev -ProfileLocation $credentialFile } |
                Should -Throw -ErrorId 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $Region -eq 'eu-central-1' -and $null -ne $ClientConfig -and $ProfileName -eq 'dev' -and $ProfileLocation -eq $credentialFile
            }
        }

        It 'names the region in RuntimeVersionCheckFailed, as RuntimeAliasCheckFailed does' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('denied') }
            $err = { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } | Should -Throw -ErrorId 'RuntimeVersionCheckFailed,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "Could not read version 'v1' of runtime 'my-ps' in region 'us-east-1' to check that it is new: denied. Nothing was built or pushed."
        }

        It 'throws RuntimeVersionCheckFailed and builds nothing when the version cannot be read' {
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                throw [System.UnauthorizedAccessException]::new('not authorized to perform lambda:GetRuntimeVersion')
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionCheckFailed,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -Match 'GetRuntimeVersion'
            $err.Exception.Message | Should -Match 'Nothing was built or pushed'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'deploys a new version of an existing runtime' {
            $result = New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v3
            $result.RuntimeVersion | Should -Be 'v3'
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'skips the version check when the runtime does not exist yet' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith {
                throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist')
            }
            $null = New-LambdaPSRuntimeDeployment @script:splat
            Should -Invoke 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'rethrows any other GetRuntime failure without creating anything' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith { throw [System.TimeoutException]::new('network') }
            { New-LambdaPSRuntimeDeployment @script:splat } | Should -Throw '*network*'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'runs the stages in order, building locally before it ensures the ECR repository' {
            $script:calls = [System.Collections.Generic.List[string]]::new()
            Mock 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -MockWith { $script:calls.Add('scaffold') }
            Mock 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -MockWith {
                $script:calls.Add($(if ($ImageUri) { 'push' } else { 'build' }))
                [PSCustomObject]@{ ImageUri = $ImageUri; ImageDigest = $(if ($ImageUri) { 'sha256:abc123' }) }
            }
            Mock 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -MockWith { $script:calls.Add('repo'); $RepositoryName }
            Mock 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                $script:calls.Add('publish')
                [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v1' }
            }
            Mock 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -MockWith { $script:calls.Add('alias') }
            $null = New-LambdaPSRuntimeDeployment @script:splat

            $script:calls -join ',' | Should -Be 'scaffold,build,repo,push,publish,alias'
        }

        It 'labels the verbose stage lines Preflight, then Stage 1 to 4' {
            $verbose = @(New-LambdaPSRuntimeDeployment @script:splat -Verbose 4>&1 |
                Where-Object { $_ -is [System.Management.Automation.VerboseRecord] } | ForEach-Object { "$_" })
            $labels = @($verbose | Where-Object { $_ -match '\A(Preflight|Stage \d+):' } | ForEach-Object { $Matches[1] })

            $labels | Should -Be @('Preflight', 'Stage 1', 'Stage 2', 'Stage 3', 'Stage 4')
            @($verbose -like "Stage 1: Scaffold runtime project at '$([System.IO.Path]::GetTempPath())*my-ps-runtime-*'.") | Should -HaveCount 1
            $verbose | Should -Contain "Stage 4: Point alias 'default' at '$script:defaultVersion'."
        }

        It 'creates no ECR repository when the local build fails' {
            Mock 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -ParameterFilter { -not $ImageUri } -MockWith {
                throw [System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new("External tool 'docker' exited with code 1."),
                    'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, 'docker')
            }
            { New-LambdaPSRuntimeDeployment @script:splat } | Should -Throw -ErrorId 'ExternalToolFailed,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    # Functions bound to an alias pick up a move on their next cold start, and a function's
    # architecture does not follow the alias, so a move to another architecture is said out loud.
    Context 'alias architecture preflight' {
        BeforeEach {
            $script:splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'; RuntimeVersion = 'v2' }
        }

        It 'throws RuntimeAliasPendingDeletion, under -WhatIf too, when the alias is scheduled for deletion' -ForEach @(
            @{ WhatIf = $true }, @{ WhatIf = $false }
        ) {
            # Arrange
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'v1'; State = 'PENDING_DELETION' }
            }

            # Act
            $err = { New-LambdaPSRuntimeDeployment @script:splat -WhatIf:$WhatIf } |
                Should -Throw -ErrorId 'RuntimeAliasPendingDeletion,New-LambdaPSRuntimeDeployment' -PassThru

            # Assert
            $err.Exception.Message | Should -BeExactly "Alias 'default' of runtime 'my-ps' is scheduled for deletion. Pass another -RuntimeAlias, or run again once Lambda has purged it. Nothing was built or pushed."
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'warns, naming both architectures, when the alias moves from <Current> to <New> (WhatIf: <WhatIf>)' -ForEach @(
            @{ Current = 'x86_64'; New = 'arm64'; WhatIf = $true; Verb = 'would move' }
            @{ Current = 'arm64'; New = 'x86_64'; WhatIf = $false; Verb = 'moves' }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'v1'; Compatibility = [PSCustomObject]@{ Architectures = @($Current) } }
            }
            $null = New-LambdaPSRuntimeDeployment @script:splat -Architecture $New -WhatIf:$WhatIf -WarningVariable warnings -WarningAction SilentlyContinue

            $moved = @($warnings | Where-Object { "$_" -like '*this deploy * it to*' })
            $moved.Count | Should -Be 1
            # A preview moves nothing, so it says what the deploy would do.
            "$($moved[0])" | Should -BeLike "*points at a ``[$Current``] version; this deploy $Verb it to 'v2', which is '$New'*"
            "$($moved[0])" | Should -BeLike "*built for ``[$Current``] fail to invoke from their next cold start*"
            Should -Invoke 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $RuntimeName -eq 'my-ps' -and $Name -eq 'default' -and $Region -eq 'us-east-1'
            }
        }

        # A version can support both; moving the alias to a single-architecture one drops the other.
        It 'warns, naming the dropped architecture, when the alias moves from a multi-architecture version' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64', 'arm64') } }
            }
            $null = New-LambdaPSRuntimeDeployment @script:splat -Architecture x86_64 -WarningVariable warnings -WarningAction SilentlyContinue

            @($warnings).Count | Should -Be 1
            "$($warnings[0])" | Should -BeLike "*points at a ``[x86_64, arm64``] version; this deploy moves it to 'v2', which is 'x86_64'*"
            "$($warnings[0])" | Should -BeLike "*built for ``[arm64``] fail to invoke*"
        }

        # When the version exists the deploy refuses and moves nothing, so nothing warns about a move.
        It 'does not warn when an existing <State> version refuses the run' -ForEach @(
            @{ State = 'ACTIVE'; VersionArchitecture = 'arm64'; Architecture = 'x86_64' }
            @{ State = 'FAILED'; VersionArchitecture = 'x86_64'; Architecture = 'x86_64' }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = $State; Compatibility = [PSCustomObject]@{ Architectures = @($VersionArchitecture) } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -Architecture $Architecture -WarningVariable warnings -WarningAction SilentlyContinue } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            @($warnings).Count | Should -Be 0
        }

        It 'names the existing version''s own architecture, and does not warn, when that version is another architecture' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'v1'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v1'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v1 -Architecture x86_64 -WarningVariable warnings -WarningAction SilentlyContinue } catch { $err = $_ }

            $err.Exception.Message | Should -BeLike "*untouched. Its image is [[]arm64], not 'x86_64'."
            @($warnings).Count | Should -Be 0
        }

        # The advice names Update-LMCRuntimeAlias; running it is a real move, so the advice says what it drops.
        It 'notes the architecture move inside the Update-LMCRuntimeAlias advice, without a warning' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v2'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -Architecture arm64 -WarningVariable warnings -WarningAction SilentlyContinue } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike '*run Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v2*'
            $err.Exception.Message | Should -BeLike "*Running Update-LMCRuntimeAlias moves the alias off a [[]x86_64] version: functions bound to it that were built for [[]x86_64] fail to invoke from their next cold start until they are redeployed with -Architecture arm64.*"
            @($warnings).Count | Should -Be 0
        }

        # The advised command points the alias at the existing version, so the note is about that version's
        # architectures, not -Architecture.
        It 'gives no move note when <Case>' -ForEach @(
            @{ Case = 'the existing version supports the alias''s architecture too'; Version = @('x86_64', 'arm64'); AliasVersion = 'v1'; Alias = @('arm64'); Architecture = 'x86_64' }
            @{ Case = 'the existing version reports no architecture'; Version = @(); AliasVersion = 'v1'; Alias = @('arm64'); Architecture = 'x86_64' }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = $AliasVersion; Compatibility = [PSCustomObject]@{ Architectures = $Alias } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v2'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = $Version } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -Architecture $Architecture -WarningVariable warnings -WarningAction SilentlyContinue } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike '*run Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v2*'
            $err.Exception.Message | Should -Not -BeLike '*Running Update-LMCRuntimeAlias moves the alias*'
            @($warnings).Count | Should -Be 0
        }

        # Version names are case-sensitive, so an alias on 'V2' is on another version than 'v2'.
        It 'notes the move when the alias is on a version whose name differs only in case' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'V2'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v2'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -RuntimeVersion v2 -Architecture x86_64 } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike '*Running Update-LMCRuntimeAlias moves the alias off a `[arm64`] version: functions bound to it that were built for `[arm64`] fail to invoke*'
        }

        It 'names only the architecture the existing version lacks when the alias is on a multi-architecture version' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; RuntimeVersion = 'v1'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64', 'arm64') } }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v2'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -Architecture x86_64 } catch { $err = $_ }

            $err.Exception.Message | Should -BeLike '*Running Update-LMCRuntimeAlias moves the alias off a `[x86_64, arm64`] version: functions bound to it that were built for `[arm64`] fail to invoke*'
        }

        It 'gives the Update-LMCRuntimeAlias advice without a note when <Case>' -ForEach @(
            @{ Case = 'the alias is on the same architecture'; Alias = 'same' }
            @{ Case = 'the alias does not exist'; Alias = 'missing' }
            @{ Case = 'the alias cannot be read'; Alias = 'denied' }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                if ($Alias -eq 'same') { [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } } }
                elseif ($Alias -eq 'missing') { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime alias does not exist') }
                else { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntimeAlias') }
            }
            Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'v2'; State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }
            $err = $null
            try { New-LambdaPSRuntimeDeployment @script:splat -Architecture arm64 -WarningVariable warnings -WarningAction SilentlyContinue } catch { $err = $_ }

            # The version check comes first, so a refused alias read does not hide RuntimeVersionExists.
            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists,New-LambdaPSRuntimeDeployment'
            $err.Exception.Message | Should -BeLike '*run Update-LMCRuntimeAlias*'
            $err.Exception.Message | Should -Not -BeLike '*Running Update-LMCRuntimeAlias moves the alias*'
            @($warnings).Count | Should -Be 0
        }

        It 'does not warn when <Case>' -ForEach @(
            @{ Case = 'the alias stays on the same architecture'; Architectures = @('x86_64') }
            @{ Case = 'the alias reports no architecture'; Architectures = @() }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = $Architectures } }
            }
            $null = New-LambdaPSRuntimeDeployment @script:splat -WhatIf -WarningVariable warnings
            @($warnings).Count | Should -Be 0
        }

        It 'does not warn, and does not read the alias, when the runtime does not exist yet' {
            Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith {
                throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist')
            }
            $null = New-LambdaPSRuntimeDeployment @script:splat -WhatIf -WarningVariable warnings
            @($warnings).Count | Should -Be 0
            Should -Invoke 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'stops before anything is created when the alias cannot be read' {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntimeAlias')
            }
            { New-LambdaPSRuntimeDeployment @script:splat } | Should -Throw '*lambda:GetRuntimeAlias*'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # A refused read is not a missing alias, so it has an ID of its own and keeps the service's error.
        It 'stops with RuntimeAliasCheckFailed, wrapping the service error, when the alias read is refused (WhatIf <WhatIf>)' -ForEach @(
            @{ WhatIf = $false }, @{ WhatIf = $true }
        ) {
            Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith {
                throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntimeAlias')
            }
            $err = { New-LambdaPSRuntimeDeployment @script:splat -WhatIf:$WhatIf } |
                Should -Throw -ErrorId 'RuntimeAliasCheckFailed,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "Could not read alias 'default' of runtime 'my-ps' in region 'us-east-1' to check its architecture: not authorized to perform lambda:GetRuntimeAlias. Nothing was built or pushed."
            $err.Exception.InnerException.GetType().Name | Should -Be 'AccessDeniedException'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # Declining the first prompt runs nothing, yet the architecture warning was shown: it is written before
        # anything is confirmed. Its order against the decline's own warning shows that; the prompt text itself
        # is not read, because a redirected console does not show it on every platform.
        It 'warns before anything is confirmed, and declining the first stage runs nothing' {
            $stub = {
                function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker' } }
                function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
                function script:Get-ECRImageMetadata { }
                function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'my-ps' } }
                function script:Get-LMCRuntimeVersion { throw [System.InvalidOperationException]::new('not found', [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')) }
                function script:Get-LMCRuntimeAlias { [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }
                function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
                function script:Build-LambdaPSRuntimeImage { $script:LambdaPSCalls.Add('Build') }
                function script:Initialize-LambdaPSEcrRepository { $script:LambdaPSCalls.Add('Repository') }
                function script:Publish-LambdaPSRuntimeVersion { $script:LambdaPSCalls.Add('Publish') }
                function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
            }
            $fakeTypes = [System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1')
            $project = [System.IO.Path]::Combine($TestDrive, 'confirm-arch-rt')
            $command = "New-LambdaPSRuntimeDeployment -RuntimeName my-ps -RuntimeAlias default -PowerShellVersion 7.4 -Region us-east-1 -RepositoryName rt -RuntimeVersion v2 -Architecture arm64 -ProjectPath '$project' -Confirm"

            $result = Invoke-InChildPwsh -Stub (". '$fakeTypes'`n" + $stub.ToString()) -Command $command -Answer 'N'

            $result.Calls | Should -BeNullOrEmpty
            $output = $result.Output -replace '\s+', ' '
            $warningAt = $output.IndexOf('this deploy moves it')
            $declinedAt = $output.IndexOf('because the scaffold stage was declined')
            $warningAt | Should -BeGreaterThan -1
            $declinedAt | Should -BeGreaterThan $warningAt
        }
    }

    Context 'project path replacement' {
        BeforeEach {
            $script:projectPath = [System.IO.Path]::Combine($TestDrive, [System.IO.Path]::GetRandomFileName())
            $null = New-Item -ItemType Directory -Path $script:projectPath
            $script:splat = @{
                RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'us-east-1'; RepositoryName = 'rt'
                ProjectPath = $script:projectPath
            }
        }

        It 'refuses a non-empty directory that is not a scaffolded project, before any AWS call' {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'keep.txt')) -Value 'user file'

            { New-LambdaPSRuntimeDeployment @script:splat } | Should -Throw -ErrorId 'TargetNotEmpty,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Test-Path -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'keep.txt')) | Should -BeTrue
        }

        It 'builds a scaffolded runtime project as is, with the pwsh its Dockerfile installs' {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) -Value '{"Target":"CMR","PowerShellVersion":"7.4"}'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'Dockerfile')) -Value "FROM x`nARG PWSH_VERSION=7.4.2`n"
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'custom.txt')) -Value 'user file'

            $result = New-LambdaPSRuntimeDeployment @script:splat

            $result.PwshVersion | Should -Be '7.4.2'
            $result.RuntimeVersion | Should -Be '7.4.2-1'
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 2 -Exactly -ParameterFilter { $Path -eq $script:projectPath }
            Test-Path -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'custom.txt')) | Should -BeTrue
        }

        It 'leaves the arm64 hash check of a scaffolded runtime project to its Dockerfile' {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) -Value '{"Target":"CMR"}'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'Dockerfile')) -Value "FROM x`nARG PWSH_VERSION=7.4.13`n"

            $null = New-LambdaPSRuntimeDeployment @script:splat -Architecture arm64

            Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 2 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
        }

        It 'refuses a scaffolded runtime project with ProjectParameterConflict when <Case>, before any AWS call' -ForEach @(
            @{ Case = 'its PowerShell version differs'; Marker = '{"Target":"CMR","PowerShellVersion":"7.6"}'; Extra = @{}; Expected = '*a PowerShell 7.6 project, not 7.4*' }
            @{ Case = 'its Dockerfile installs another line''s pwsh'; Marker = '{"Target":"CMR"}'; Extra = @{}; Dockerfile = '7.6.0'; Expected = '*installs pwsh 7.6.0, not a PowerShell 7.4 release*' }
            @{ Case = '-PwshVersion differs from its Dockerfile'; Marker = '{"Target":"CMR"}'; Extra = @{ PwshVersion = '7.4.6' }; Expected = '*installs pwsh 7.4.2, not 7.4.6*' }
            @{ Case = '-IncludeModule is passed'; Marker = '{"Target":"CMR"}'; Extra = @{ IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' } }; Expected = '*-IncludeModule*Save-LambdaPSModule*' }
        ) {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) -Value $Marker
            $pwsh = if ($Dockerfile) { $Dockerfile } else { '7.4.2' }
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'Dockerfile')) -Value "FROM x`nARG PWSH_VERSION=$pwsh`n"

            { New-LambdaPSRuntimeDeployment @script:splat @Extra } |
                Should -Throw -ErrorId 'ProjectParameterConflict,New-LambdaPSRuntimeDeployment' -ExpectedMessage $Expected
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'refuses a directory whose marker is a <Target> project, before any AWS call' -ForEach @(
            @{ Target = 'Container' }, @{ Target = 'Layer' }
        ) {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) -Value "{`"Target`":`"$Target`"}"
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'handler.ps1')) -Value 'user code'

            $err = { New-LambdaPSRuntimeDeployment @script:splat } | Should -Throw -ErrorId 'TargetNotEmpty,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "*'$Target' project*"
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
            Test-Path -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'handler.ps1')) | Should -BeTrue
        }

        It 'refuses a directory whose marker names no target, without naming an empty one' {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) -Value '{"PowerShellVersion":"7.4"}'

            $err = { New-LambdaPSRuntimeDeployment @script:splat -WhatIf } | Should -Throw -ErrorId 'TargetNotEmpty,New-LambdaPSRuntimeDeployment' -PassThru
            $err.Exception.Message | Should -Not -BeLike "*holds a '' project*"
            $err.Exception.Message | Should -BeLike '*is not a runtime project scaffolded by this module*'
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'scaffolds in a new temp folder and removes it, leaving ./my-ps-runtime untouched, without -ProjectPath' {
            $existing = [System.IO.Path]::Combine($TestDrive, 'my-ps-runtime')
            $null = New-Item -ItemType Directory -Path $existing -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($existing, 'lambdaps-runtime.json')) -Value '{"Target":"CMR"}'
            Mock 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -MockWith {
                $script:scaffoldedPath = $Path
                $null = New-Item -ItemType Directory -Path $Path -Force
            }
            $splat = $script:splat.Clone()
            $splat.Remove('ProjectPath')

            $result = New-LambdaPSRuntimeDeployment @splat

            $script:scaffoldedPath | Should -BeLike "$([System.IO.Path]::GetTempPath())my-ps-runtime-*"
            Test-Path -LiteralPath $script:scaffoldedPath | Should -BeFalse
            $result.ProjectPath | Should -BeNullOrEmpty
            Test-Path -LiteralPath ([System.IO.Path]::Combine($existing, 'lambdaps-runtime.json')) | Should -BeTrue
        }

        It 'scaffolds two deploys of one runtime without -ProjectPath in separate folders' {
            $script:scaffolded = [System.Collections.Generic.List[string]]::new()
            Mock 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -MockWith { $script:scaffolded.Add($Path) }
            $splat = $script:splat.Clone()
            $splat.Remove('ProjectPath')

            $null = New-LambdaPSRuntimeDeployment @splat
            $null = New-LambdaPSRuntimeDeployment @splat

            $script:scaffolded | Should -HaveCount 2
            $script:scaffolded[0] | Should -Not -Be $script:scaffolded[1]
        }

        It 'keeps -ProjectPath when the deploy fails before scaffolding, whatever the caller''s variables' {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) -Value '{"Target":"CMR"}'
            $global:temporaryProject = $true
            Mock 'Resolve-LambdaPSPwshVersion' -ModuleName $script:moduleName -MockWith { throw 'stop before the scaffold' }
            try {
                { New-LambdaPSRuntimeDeployment @script:splat } | Should -Throw '*stop before the scaffold*'
            }
            finally { Remove-Variable -Name temporaryProject -Scope Global }

            Test-Path -LiteralPath ([System.IO.Path]::Combine($script:projectPath, 'lambdaps-runtime.json')) | Should -BeTrue
        }

        It 'refuses a -ProjectPath that is a file, under -WhatIf too' {
            $file = [System.IO.Path]::Combine($script:projectPath, 'notadir.txt')
            Set-Content -LiteralPath $file -Value 'x'

            { New-LambdaPSRuntimeDeployment @script:splat -ProjectPath $file -WhatIf } |
                Should -Throw -ErrorId 'TargetIsFile,New-LambdaPSRuntimeDeployment'
            Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # BeforeEach runs each test from TestDrive, so a relative path resolves under it.
        It 'reports a relative -ProjectPath as a full path' {
            $result = New-LambdaPSRuntimeDeployment -RuntimeName my-ps -PowerShellVersion 7.4 -RepositoryName rt -Region us-east-1 -ProjectPath ./rel-rt
            $result.ProjectPath | Should -Be ([System.IO.Path]::Combine($TestDrive, 'rel-rt'))
        }

        # A folder that cannot be listed is not an empty one: refuse it before any AWS call, as the scaffolder would.
        It 'refuses a -ProjectPath that cannot be listed with TargetNotReadable, before any AWS call' -Skip:($IsWindows -or ((id -u) -eq '0')) {
            $locked = [System.IO.Path]::Combine($script:projectPath, 'locked')
            $null = New-Item -ItemType Directory -Path $locked
            chmod 000 $locked
            try {
                $err = { New-LambdaPSRuntimeDeployment @script:splat -ProjectPath $locked -WhatIf } |
                    Should -Throw -ErrorId 'TargetNotReadable,New-LambdaPSRuntimeDeployment' -PassThru
                $err.Exception.Message | Should -BeLike "Target '$locked' cannot be listed, so it cannot be checked for files the scaffold would write over:*Choose a directory you can read."
                Should -Invoke 'Get-STSCallerIdentity' -ModuleName $script:moduleName -Times 0 -Exactly
                Should -Invoke 'Get-LMCRuntime' -ModuleName $script:moduleName -Times 0 -Exactly
            }
            finally { chmod 755 $locked }
        }

        It 'scaffolds into an existing empty directory' {
            $null = New-LambdaPSRuntimeDeployment @script:splat
            Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 1 -Exactly
        }
    }
}
