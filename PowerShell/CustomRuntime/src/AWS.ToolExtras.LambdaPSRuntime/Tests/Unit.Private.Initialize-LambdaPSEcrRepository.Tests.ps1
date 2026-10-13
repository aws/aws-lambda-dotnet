BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'DockerStub.ps1'))
    Add-LambdaPSDockerStub
    # The seam reads docker's config to decide whether to log in. An empty one keeps the host's own
    # docker config out of every test; a test that needs a config writes its own.
    $script:hostDockerConfig = $env:DOCKER_CONFIG
    $env:DOCKER_CONFIG = (New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($TestDrive, 'docker-empty'))).FullName
}

AfterAll {
    $env:DOCKER_CONFIG = $script:hostDockerConfig
    Remove-LambdaPSTestModule
}

Describe 'Initialize-LambdaPSEcrRepository' -Tag 'Unit' {
    # AWS.Tools surfaces ECR service errors as an outer InvalidOperationException wrapping the SDK
    # exception, as Get-ECRRepository does. The tests reproduce that shape with the Fakes in
    # Helpers/FakeTypes.ps1, so they exercise the InnerException walk rather than a flat exception.

    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'creates a missing repository, sets a Lambda-pull policy without reading one, and logs in' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig) }
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped',
                    [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('The repository with name rt does not exist'))
            }
            Mock 'New-ECRRepository' -MockWith { [PSCustomObject]@{ RepositoryName = $RepositoryName } }
            Mock 'Get-ECRRepositoryPolicy' -MockWith { }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = 'https://111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' | Should -BeNullOrEmpty
            Should -Invoke 'New-ECRRepository' -Times 1 -Exactly -ParameterFilter { $RepositoryName -eq 'rt' -and $ImageScanningConfiguration_ScanOnPush -eq $true }
            # A repository created just now has no policy, so there is nothing to read.
            Should -Invoke 'Get-ECRRepositoryPolicy' -Times 0 -Exactly
            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 1 -Exactly -ParameterFilter {
                $p = $PolicyText | ConvertFrom-Json
                $RepositoryName -eq 'rt' -and @($p.Statement).Count -eq 1 -and
                $p.Statement[0].Principal.Service -eq 'lambda.amazonaws.com'
            }
            # docker login must use the BARE host (scheme stripped), or buildx --push is denied.
            Should -Invoke 'docker' -Times 1 -Exactly -ParameterFilter {
                $args -contains '111122223333.dkr.ecr.us-east-1.amazonaws.com' -and
                ($args -join ' ') -notmatch 'https://'
            }
            # The token goes in on stdin, never on the command line where other users can see it.
            Should -Invoke 'docker' -Times 1 -Exactly -ParameterFilter { $args -contains '--password-stdin' -and $args -notcontains 'tok' }
        }
    }

    Context 'docker login' {
        BeforeEach {
            InModuleScope $script:moduleName {
                function script:Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
                function script:New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
                function script:Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
                function script:Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ErrorAction) }
                function script:Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            }
            Mock 'New-ECRRepository' -ModuleName $script:moduleName -MockWith { }
            Mock 'Set-ECRRepositoryPolicy' -ModuleName $script:moduleName -MockWith { }
            Mock 'Get-ECRRepository' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ RepositoryName = 'rt' } }
            # The repository already grants this pull, scoped to the account, so no policy is written.
            Mock 'Get-ECRRepositoryPolicy' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ PolicyText = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*","Condition":{"StringEquals":{"aws:SourceAccount":"111122223333"}}}]}' }
            }
            Mock 'Get-ECRLoginCommand' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = 'https://111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
        }

        It 'throws EcrDockerLoginFailed when docker login fails' {
            Mock 'docker' -ModuleName $script:moduleName -MockWith { 'Error: denied'; $global:LASTEXITCODE = 1 }
            InModuleScope $script:moduleName {
                { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                    Should -Throw -ErrorId 'EcrDockerLoginFailed' -ExpectedMessage '*Error: denied*'
                Should -Invoke 'Set-ECRRepositoryPolicy' -Times 0 -Exactly
            }
        }

        # A native command, not a mock: only a real exit code trips the preference.
        It 'throws EcrDockerLoginFailed even when PSNativeCommandUseErrorActionPreference is on' {
            $pwshPath = [System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }))
            InModuleScope $script:moduleName -Parameters @{ PwshPath = $pwshPath } {
                param($PwshPath)
                # pwsh rejects 'login' as an argument and exits non-zero, standing in for a failed docker login.
                Set-Alias -Name 'docker' -Value $PwshPath -Scope Script
                try {
                    $PSNativeCommandUseErrorActionPreference = $true
                    $err = $null
                    try { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -ErrorAction Stop }
                    catch { $err = $_ }
                    $err.FullyQualifiedErrorId | Should -Be 'EcrDockerLoginFailed'
                }
                finally { Remove-Item -Path 'alias:docker' -ErrorAction SilentlyContinue }
            }
        }

        # The deploy cmdlet calls this inside try and rethrows with its own $PSCmdlet; the error it surfaces,
        # and every record -ErrorVariable collects, must name the cmdlet, not this helper.
        It 'surfaces EcrDockerLoginFailed from the calling cmdlet, and names no internal helper in -ErrorVariable' {
            Mock 'docker' -ModuleName $script:moduleName -MockWith { 'Error: denied'; $global:LASTEXITCODE = 1 }
            InModuleScope $script:moduleName {
                function Invoke-LambdaPSTestCaller {
                    [CmdletBinding()]
                    param([scriptblock]$Call)
                    try { $null = & $Call }
                    catch { $PSCmdlet.ThrowTerminatingError($_) }
                }
                $collected = $null
                $err = $null
                try {
                    Invoke-LambdaPSTestCaller -ErrorVariable collected -Call {
                        Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1'
                    }
                }
                catch { $err = $_ }

                $err.FullyQualifiedErrorId | Should -Be 'EcrDockerLoginFailed,Invoke-LambdaPSTestCaller'
                $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
                $ids | Should -Contain 'EcrDockerLoginFailed,Invoke-LambdaPSTestCaller'
                @($ids | Where-Object { $_ -like '*,Initialize-LambdaPSEcrRepository' }) | Should -BeNullOrEmpty
            }
        }

        It 'skips docker login when the docker config hands the registry to ecr-login (<Case>)' -ForEach @(
            @{ Case = 'credHelpers'; Config = '{"credHelpers":{"111122223333.dkr.ecr.us-east-1.amazonaws.com":"ecr-login"}}' }
            @{ Case = 'credsStore'; Config = '{"credsStore":"ecr-login"}' }
            @{ Case = 'credsStore, with credHelpers for another registry'; Config = '{"credsStore":"ecr-login","credHelpers":{"444455556666.dkr.ecr.us-east-1.amazonaws.com":"desktop"}}' }
        ) {
            $directory = (New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($TestDrive, "docker-$([guid]::NewGuid())"))).FullName
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($directory, 'config.json'), $Config)
            Mock 'docker' -ModuleName $script:moduleName -MockWith { 'Error saving credentials: not implemented'; $global:LASTEXITCODE = 1 }
            $previous = $env:DOCKER_CONFIG
            $env:DOCKER_CONFIG = $directory
            try {
                InModuleScope $script:moduleName {
                    { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                        Should -Not -Throw
                }
            }
            finally { $env:DOCKER_CONFIG = $previous }
            Should -Invoke 'docker' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # docker takes the registry's credHelpers entry over credsStore, and a helper other than
        # ecr-login stores the login, so docker login runs.
        It 'logs in when the registry''s own helper is not ecr-login (<Case>)' -ForEach @(
            @{ Case = 'credHelpers over an ecr-login credsStore'; Config = '{"credsStore":"ecr-login","credHelpers":{"111122223333.dkr.ecr.us-east-1.amazonaws.com":"desktop"}}' }
            @{ Case = 'ecr-login for another registry only'; Config = '{"credHelpers":{"444455556666.dkr.ecr.us-east-1.amazonaws.com":"ecr-login"}}' }
            @{ Case = 'another credsStore'; Config = '{"credsStore":"osxkeychain"}' }
            @{ Case = 'a config that is not JSON'; Config = 'not json' }
        ) {
            $directory = (New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($TestDrive, "docker-$([guid]::NewGuid())"))).FullName
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($directory, 'config.json'), $Config)
            Mock 'docker' -ModuleName $script:moduleName -MockWith { 'Login Succeeded'; $global:LASTEXITCODE = 0 }
            $previous = $env:DOCKER_CONFIG
            $env:DOCKER_CONFIG = $directory
            try {
                InModuleScope $script:moduleName {
                    Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1'
                }
            }
            finally { $env:DOCKER_CONFIG = $previous }
            Should -Invoke 'docker' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'names the file docker reports in the unencrypted-token warning' {
            Mock 'docker' -ModuleName $script:moduleName -MockWith {
                'WARNING! Your password will be stored unencrypted in /ci/docker/config.json.'
                'Configure a credential helper to remove this warning.'
                'Login Succeeded'
                $global:LASTEXITCODE = 0
            }
            InModuleScope $script:moduleName {
                Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -WarningVariable warnings -WarningAction SilentlyContinue
                "$warnings" | Should -BeLike "*unencrypted in '/ci/docker/config.json'.*osxkeychain, wincred, pass or secretservice*"
            }
        }

        It 'names the docker config file when docker does not say where it stored the token' {
            Mock 'docker' -ModuleName $script:moduleName -MockWith { 'Your password will be stored unencrypted.'; $global:LASTEXITCODE = 0 }
            InModuleScope $script:moduleName {
                Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -WarningVariable warnings -WarningAction SilentlyContinue
                "$warnings" | Should -BeLike "*unencrypted in '$([System.IO.Path]::Combine($env:DOCKER_CONFIG, 'config.json'))'.*"
            }
        }

        It 'warns when docker stores the token unencrypted' {
            Mock 'docker' -ModuleName $script:moduleName -MockWith {
                'WARNING! Your password will be stored unencrypted in /home/u/.docker/config.json.'
                'Login Succeeded'
                $global:LASTEXITCODE = 0
            }
            InModuleScope $script:moduleName {
                Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -WarningVariable warnings -WarningAction SilentlyContinue
                ($warnings -join ' ') | Should -BeLike '*unencrypted*credential store*'
            }
        }
    }

    It 'does not create the repository when it already exists' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig) }
            Mock 'Get-ECRRepository' -MockWith { [PSCustomObject]@{ RepositoryName = 'rt' } }
            Mock 'New-ECRRepository' -MockWith { }
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped',
                    [LambdaPSTests.Fakes.RepositoryPolicyNotFoundException]::new('no policy'))
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' | Should -BeNullOrEmpty
            Should -Invoke 'New-ECRRepository' -Times 0 -Exactly
            # An existing repository with no policy at all gets the Lambda grant.
            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 1 -Exactly
        }
    }

    # Set-ECRRepositoryPolicy is a full replace, so writing a FIXED policy would silently delete every
    # other grant on an existing, shared repository. The seam merges into the existing policy.
    It 'keeps existing statements when adding the Lambda grant to an existing policy' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig) }
            Mock 'Get-ECRRepository' -MockWith { [PSCustomObject]@{ RepositoryName = 'shared' } }
            Mock 'New-ECRRepository' -MockWith { }
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                [PSCustomObject]@{ PolicyText = '{"Version":"2012-10-17","Statement":[{"Sid":"CrossAccountPull","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"]}]}' }
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            $null = Initialize-LambdaPSEcrRepository -RepositoryName 'shared' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1'

            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 1 -Exactly -ParameterFilter {
                $sids = @(($PolicyText | ConvertFrom-Json).Statement.Sid)
                $sids -contains 'CrossAccountPull' -and $sids -contains 'LambdaPSRuntimeImagePull'
            }
        }
    }

    It 'writes nothing when the existing policy already grants Lambda image pull' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig) }
            Mock 'Get-ECRRepository' -MockWith { [PSCustomObject]@{ RepositoryName = 'rt' } }
            Mock 'New-ECRRepository' -MockWith { }
            # A realistic repository policy shape: a Lambda grant with an extra action and another Sid.
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                [PSCustomObject]@{ PolicyText = '{"Version":"2008-10-17","Statement":[{"Sid":"LambdaCMRRead","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":["ecr:GetDownloadUrlForLayer","ecr:BatchGetImage","ecr:GetRepositoryPolicy"]}]}' }
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            $null = Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -WarningVariable warnings -WarningAction SilentlyContinue
            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 0 -Exactly
            # The grant has no Condition, so it is open to any account: left as it is, with a warning.
            "$warnings" | Should -BeLike "*'LambdaCMRRead' lets any account's Lambda functions pull its images*"
        }
    }

    It 'rethrows a non-not-found repository error instead of creating the repository' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            # Message mentions "does not exist" on purpose: a message-text match would misread this as
            # not-found and go on to create the repository.
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped',
                    [LambdaPSTests.Fakes.AccessDeniedException]::new('KMS key does not exist or is not accessible'))
            }
            Mock 'New-ECRRepository' -MockWith { }

            { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                Should -Throw -ExceptionType ([System.InvalidOperationException]) -ExpectedMessage 'wrapped'
            Should -Invoke 'New-ECRRepository' -Times 0 -Exactly
        }
    }

    It 'rethrows a non-not-found policy read error instead of overwriting the policy' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-ECRRepository' -MockWith { [PSCustomObject]@{ RepositoryName = 'rt' } }
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.AccessDeniedException]::new('denied'))
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            # docker login runs before the policy step.
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                Should -Throw -ExceptionType ([System.InvalidOperationException]) -ExpectedMessage 'wrapped'
            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 0 -Exactly
        }
    }

    It 'tolerates a repository created concurrently between the probe and the create' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig) }
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('nf'))
            }
            Mock 'New-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryAlreadyExistsException]::new('exists'))
            }
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryPolicyNotFoundException]::new('none'))
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' | Should -BeNullOrEmpty
            # Not created by this call, so the policy is READ before writing, not assumed empty.
            Should -Invoke 'Get-ECRRepositoryPolicy' -Times 1 -Exactly
        }
    }

    It 'passes the region, client config and credentials to every ECR call' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ProfileName, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ProfileName, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ProfileName, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ProfileName, $ErrorAction) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ProfileName, $ErrorAction) }
            # Not found, then created concurrently: the one path that makes all five calls.
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('nf'))
            }
            Mock 'New-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryAlreadyExistsException]::new('exists'))
            }
            Mock 'Get-ECRRepositoryPolicy' -MockWith { }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.eu-west-1.amazonaws.com' }
            }
            Mock 'docker' -MockWith { $global:LASTEXITCODE = 0 }

            Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:eu-west-1:111122223333:runtime/rt' -Region 'eu-west-1' -CredentialParameter @{ ProfileName = 'p' }
            foreach ($command in 'Get-ECRRepository', 'New-ECRRepository', 'Get-ECRRepositoryPolicy', 'Set-ECRRepositoryPolicy', 'Get-ECRLoginCommand') {
                Should -Invoke $command -Times 1 -Exactly -ParameterFilter {
                    $Region -eq 'eu-west-1' -and $ProfileName -eq 'p' -and $ClientConfig.ClientAppId -eq 'AWS.ToolExtras.LambdaPSRuntime#test'
                }
            }
        }
    }

    It 'skips the grant and docker login when -WhatIf skips the create' {
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('nf'))
            }
            Mock 'New-ECRRepository' -MockWith { }
            # ECR answers a policy read on a missing repository with RepositoryNotFoundException.
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('nf'))
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith { }

            $output = Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -WhatIf -Verbose 4>&1
            "$output" | Should -BeLike "*'rt' was not created; the pull grant and docker login are skipped*"
            foreach ($command in 'New-ECRRepository', 'Get-ECRRepositoryPolicy', 'Set-ECRRepositoryPolicy', 'Get-ECRLoginCommand') {
                Should -Invoke $command -Times 0 -Exactly
            }
        }
    }

    It 'creates no repository and writes no policy when docker login fails' {
        Mock 'docker' -ModuleName $script:moduleName -MockWith { 'Error: denied'; $global:LASTEXITCODE = 1 }
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('nf'))
            }
            Mock 'New-ECRRepository' -MockWith { }
            Mock 'Get-ECRRepositoryPolicy' -MockWith { }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }

            { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'EcrDockerLoginFailed'
            Should -Invoke 'New-ECRRepository' -Times 0 -Exactly
            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 0 -Exactly
        }
    }

    It 'reads the policy of an existing repository under -WhatIf, but writes nothing and does not log in' {
        Mock 'docker' -ModuleName $script:moduleName -MockWith { $global:LASTEXITCODE = 0 }
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-ECRRepository' -MockWith { [PSCustomObject]@{ RepositoryName = 'rt' } }
            Mock 'New-ECRRepository' -MockWith { }
            Mock 'Get-ECRRepositoryPolicy' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryPolicyNotFoundException]::new('none'))
            }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith { }

            $null = Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' -WhatIf
            Should -Invoke 'Get-ECRRepositoryPolicy' -Times 1 -Exactly
            foreach ($command in 'New-ECRRepository', 'Set-ECRRepositoryPolicy', 'Get-ECRLoginCommand') {
                Should -Invoke $command -Times 0 -Exactly
            }
        }
        Should -Invoke 'docker' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'rethrows a create error other than already-exists, and writes no policy' {
        Mock 'docker' -ModuleName $script:moduleName -MockWith { $global:LASTEXITCODE = 0 }
        InModuleScope $script:moduleName {
            function Get-ECRRepository { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function New-ECRRepository { param($RepositoryName, $ImageScanningConfiguration_ScanOnPush, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRRepositoryPolicy { param($RepositoryName, $Region, $ClientConfig, $ErrorAction) }
            function Set-ECRRepositoryPolicy { param($RepositoryName, $PolicyText, $Region, $ClientConfig, $ErrorAction) }
            function Get-ECRLoginCommand { param($Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.RepositoryNotFoundException]::new('nf'))
            }
            Mock 'New-ECRRepository' -MockWith {
                throw [System.InvalidOperationException]::new('wrapped', [LambdaPSTests.Fakes.AccessDeniedException]::new('denied'))
            }
            Mock 'Get-ECRRepositoryPolicy' -MockWith { }
            Mock 'Set-ECRRepositoryPolicy' -MockWith { }
            Mock 'Get-ECRLoginCommand' -MockWith {
                [PSCustomObject]@{ Username = 'AWS'; Password = 'tok'; Endpoint = '111122223333.dkr.ecr.us-east-1.amazonaws.com' }
            }

            { Initialize-LambdaPSEcrRepository -RepositoryName 'rt' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                Should -Throw -ExceptionType ([System.InvalidOperationException]) -ExpectedMessage 'wrapped'
            Should -Invoke 'Get-ECRRepositoryPolicy' -Times 0 -Exactly
            Should -Invoke 'Set-ECRRepositoryPolicy' -Times 0 -Exactly
        }
    }

    It 'rejects a whitespace repository at parameter binding' {
        InModuleScope $script:moduleName {
            { Initialize-LambdaPSEcrRepository -RepositoryName '   ' -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Initialize-LambdaPSEcrRepository'
        }
    }
}
