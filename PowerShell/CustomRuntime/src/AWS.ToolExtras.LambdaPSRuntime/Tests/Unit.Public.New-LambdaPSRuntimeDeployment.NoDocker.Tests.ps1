BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSRuntimeDeployment -BuildMethod' -Tag 'Unit' {
    BeforeEach {
        Push-Location $TestDrive
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new('The docker CLI was not found on PATH.'), 'DockerNotFound',
                [System.Management.Automation.ErrorCategory]::NotInstalled, 'docker')
        }
        Mock 'Import-LambdaPSOciBuilder' -ModuleName $script:moduleName -MockWith { [object] }
        Mock 'Get-STSCallerIdentity' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Account = '111122223333' } }
        Mock 'Get-LMCRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = $RuntimeName } }
        Mock 'Get-LMCRuntimeVersion' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist') }
        Mock 'Get-LMCRuntimeAlias' -ModuleName $script:moduleName -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime alias does not exist') }
        Mock 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -MockWith { }
        Mock 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -MockWith { $RepositoryName }
        Mock 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ Tag = $Tag; ImageUri = $ImageUri; ImageDigest = $(if ($ImageUri) { 'sha256:abc123' }); Architecture = $Architecture; Pushed = [bool]$ImageUri }
        }
        Mock 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ RuntimeName = $RuntimeName; ImageUri = $ImageUri; Version = 'v1'; Arn = "arn:aws:lambda:eu-west-1:111122223333:runtime/$RuntimeName/version/v1" }
        }
        Mock 'Set-LambdaPSRuntimeAlias' -ModuleName $script:moduleName -MockWith { $RuntimeAlias }
        $script:splat = @{ RuntimeName = 'my-ps'; PowerShellVersion = '7.4'; Region = 'eu-west-1'; RepositoryName = 'rt' }
    }

    AfterEach {
        Pop-Location
    }

    It 'deploys without Docker under Auto: no local build, no docker login, a NoDocker push with the credentials' {
        Mock 'Get-LambdaPSCredentialParameter' -ModuleName $script:moduleName -MockWith { @{ ProfileName = 'dev' } }
        $result = New-LambdaPSRuntimeDeployment @script:splat -ProfileName dev

        $result.ImageUri | Should -Be '111122223333.dkr.ecr.eu-west-1.amazonaws.com/rt:v1'
        Should -Invoke 'Import-LambdaPSOciBuilder' -ModuleName $script:moduleName -Times 1 -Exactly
        Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $SkipDockerLogin }
        Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 1 -Exactly
        Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
            $BuildMethod -eq 'NoDocker' -and $ImageUri -and $Region -eq 'eu-west-1' -and $ProfileName -eq 'dev'
        }
        Should -Invoke 'Publish-LambdaPSRuntimeVersion' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
            $ImageUri -eq '111122223333.dkr.ecr.eu-west-1.amazonaws.com/rt@sha256:abc123'
        }
    }

    It 'keeps the Docker flow (local build, login, push) when docker is installed' {
        Mock 'Test-LambdaPSContainerRuntime' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }

        $null = New-LambdaPSRuntimeDeployment @script:splat

        Should -Invoke 'Import-LambdaPSOciBuilder' -ModuleName $script:moduleName -Times 0 -Exactly
        Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { -not $SkipDockerLogin }
        Should -Invoke 'Build-LambdaPSRuntimeImage' -ModuleName $script:moduleName -Times 2 -Exactly -ParameterFilter { $BuildMethod -ne 'NoDocker' }
    }

    It 'fails in preflight under -BuildMethod Docker when docker is missing, before anything is created' {
        { New-LambdaPSRuntimeDeployment @script:splat -BuildMethod Docker } | Should -Throw -ErrorId 'DockerNotFound,New-LambdaPSRuntimeDeployment'
        Should -Invoke 'New-LambdaPSRuntimeProject' -ModuleName $script:moduleName -Times 0 -Exactly
        Should -Invoke 'Initialize-LambdaPSEcrRepository' -ModuleName $script:moduleName -Times 0 -Exactly
    }
}
