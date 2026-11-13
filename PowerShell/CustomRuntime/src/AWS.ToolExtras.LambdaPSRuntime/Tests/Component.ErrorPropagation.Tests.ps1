# A failing stage stops the cmdlet when the caller has no enclosing try: nothing after it runs and no
# partial result is emitted. Each case runs in a fresh runspace, as a script calling the cmdlet would.

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Build-LambdaPSRuntimeImage stops on a failed docker build (no enclosing try)' -Tag 'Component' {
    It 'emits no image object when docker exits non-zero' {
        # The real Invoke-LambdaPSExternalTool runs a stand-in docker program that logs its call and
        # fails, as a broken build would. It is both the Path the container-runtime check returns and
        # the first docker on PATH, so the build reaches it whether it runs that Path or looks docker up
        # by name. The cmdlet must stop there, not go on to report the image as built.
        $dockerDirectory = [System.IO.Path]::Combine($TestDrive, 'failing-docker')
        $null = New-Item -ItemType Directory -Path $dockerDirectory
        $callLog = [System.IO.Path]::Combine($dockerDirectory, 'docker-calls.log')
        if ($IsWindows) {
            $docker = [System.IO.Path]::Combine($dockerDirectory, 'docker.cmd')
            Set-Content -LiteralPath $docker -Value "@echo %*>>`"$callLog`"`r`n@exit /b 1"
        }
        else {
            $docker = [System.IO.Path]::Combine($dockerDirectory, 'docker')
            Set-Content -LiteralPath $docker -Value "#!/bin/sh`nprintf '%s\n' `"`$*`" >> '$callLog'`nexit 1"
            [System.IO.File]::SetUnixFileMode($docker, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        }
        $stub = @"
`$env:PATH = '$dockerDirectory' + [System.IO.Path]::PathSeparator + `$env:PATH
function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '$docker'; BuildxVersion = 'v0' } }
"@
        $projectPath = [System.IO.Path]::Combine($TestDrive, 'img')
        $null = New-Item -ItemType Directory -Path $projectPath
        Set-Content -LiteralPath ([System.IO.Path]::Combine($projectPath, 'Dockerfile')) -Value 'FROM scratch'
        $result = Invoke-InFreshRunspace -Stub $stub -Command "Build-LambdaPSRuntimeImage -Path '$projectPath' -Tag rt:v1"

        @(Get-Content -LiteralPath $callLog -ErrorAction SilentlyContinue) | Should -BeLike '*build*'
        $result.Output | Should -BeNullOrEmpty
        $result.Errors.FullyQualifiedErrorId | Should -Contain 'ExternalToolFailed,Build-LambdaPSRuntimeImage'
    }
}

Describe 'Deploy cmdlets stop at the failing stage (no enclosing try)' -Tag 'Component' {
    It 'New-LambdaPSRuntimeDeployment does not point the alias at a version that did not become ACTIVE' {
        # The real Publish-LambdaPSRuntimeVersion and Set-LambdaPSRuntimeAlias run; only the
        # local tooling and the AWS cmdlets are stubbed.
        $stub = {
            # Path is set to a docker that does not exist, so the builder-platform probe reports none and Auto keeps Docker.
            function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/nonexistent/docker' } }
            function script:Get-STSCallerIdentity { [CmdletBinding()] param($Region, $ClientConfig) [PSCustomObject]@{ Account = '111122223333' } }
            # Advanced functions, so a parameter the caller passes but the stub does not declare fails the test.
            function script:New-LambdaPSRuntimeProject { [CmdletBinding(SupportsShouldProcess)] param($Path, $PowerShellVersion, $PwshVersion, $IncludeModule, $MissingModuleAction, $ModuleRepository, [switch]$Force) }
            function script:Build-LambdaPSRuntimeImage { [CmdletBinding(SupportsShouldProcess)] param($Path, $Tag, $Architecture, $ImageUri) }
            function script:Initialize-LambdaPSEcrRepository { [CmdletBinding(SupportsShouldProcess)] param($RepositoryName, $AccountId, $RuntimeArn, $Region, $CredentialParameter) }
            function script:Get-LMCRuntime { [CmdletBinding()] param($RuntimeName, $Region, $ClientConfig) [PSCustomObject]@{} }
            # The version does not exist until it is created (so the preflight passes), then settles to FAILED.
            function script:New-LMCRuntimeVersion { [CmdletBinding()] param($RuntimeName, $Name, $ImageUri, $Compatibility_Architecture, $ReleaseNote, $Region, $ClientConfig)
                $script:LambdaPSCalls.Add('New-LMCRuntimeVersion')
                [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' } }
            function script:Get-LMCRuntimeVersion { [CmdletBinding()] param($RuntimeName, $Name, $Region, $ClientConfig)
                if ($script:LambdaPSCalls -notcontains 'New-LMCRuntimeVersion') {
                    throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime version does not exist')
                }
                [PSCustomObject]@{ State = 'FAILED' } }
            function script:Get-LMCRuntimeAlias { [CmdletBinding()] param($RuntimeName, $Name, $Region, $ClientConfig) [PSCustomObject]@{} }
            function script:Get-ECRImageMetadata { [CmdletBinding()] param($RepositoryName, $ImageId, $Region, $ClientConfig) }
            function script:New-LMCRuntimeAlias { [CmdletBinding()] param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig)
                $script:LambdaPSCalls.Add('New-LMCRuntimeAlias') }
            function script:Update-LMCRuntimeAlias { [CmdletBinding()] param($RuntimeName, $Name, $RuntimeVersion, $Region, $ClientConfig)
                $script:LambdaPSCalls.Add('Update-LMCRuntimeAlias') }
        }
        $projectPath = [System.IO.Path]::Combine($TestDrive, 'rt')
        $command = "New-LambdaPSRuntimeDeployment -RuntimeName rt -PowerShellVersion 7.4 -Region us-east-1 -ProjectPath '$projectPath' -Confirm:`$false"

        $result = Invoke-InFreshRunspace -Stub $stub.ToString() -Command $command

        $result.Calls | Should -Not -Contain 'New-LMCRuntimeAlias'
        $result.Calls | Should -Not -Contain 'Update-LMCRuntimeAlias'
        $result.Output | Should -BeNullOrEmpty
        $result.Errors.FullyQualifiedErrorId | Should -Contain 'RuntimeVersionNotActive,New-LambdaPSRuntimeDeployment'
    }

    It 'New-LambdaPSFunctionDeployment does not publish the function when the <Missing> is missing' -ForEach @(
        @{ Missing = 'runtime'; RuntimeBody = "throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime not found')"
            AliasBody = "throw 'the alias must not be read'"; ErrorId = 'RuntimeArnNotFound' }
        @{ Missing = 'runtime alias'; RuntimeBody = "[PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' }"
            AliasBody = "throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Alias not found')"; ErrorId = 'RuntimeAliasNotFound' }
    ) {
        # The real Get-LambdaPSRuntimeArn and Get-LambdaPSRuntimeAliasArchitecture run, so the error
        # comes from the seam exactly as it would against a missing runtime or alias.
        $stub = {
            function script:Get-LMCRuntime { [CmdletBinding()] param($RuntimeName, $Region, $ClientConfig)
                __RUNTIME_BODY__ }
            function script:Get-LMCRuntimeAlias { [CmdletBinding()] param($RuntimeName, $Name, $Region, $ClientConfig)
                __ALIAS_BODY__ }
            # A new function: there is no existing one to keep the alias of.
            function script:Get-LMFunctionConfiguration { [CmdletBinding()] param($FunctionName, $Region, $ClientConfig)
                throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Function not found') }
            function script:Compress-LambdaPSFunctionPackage { param($Path, $OutFile, $HandlerScript, $IncludeModule, $MissingModuleAction, [switch]$Force)
                $script:LambdaPSCalls.Add('Compress-LambdaPSFunctionPackage') }
            function script:Publish-LambdaPSFunction { param($FunctionName)
                $script:LambdaPSCalls.Add('Publish-LambdaPSFunction') }
        }
        $source = [System.IO.Path]::Combine($TestDrive, 'fn.ps1')
        Set-Content -LiteralPath $source -Value 'param($LambdaInput, $LambdaContext) "ok"'
        $package = "$source.zip"
        $command = ("New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath '$source' -RuntimeName rt " +
            "-Role 'arn:aws:iam::111122223333:role/r' -Region us-east-1 -PackagePath '$package' -Confirm:`$false")

        $stubText = $stub.ToString().Replace('__RUNTIME_BODY__', $RuntimeBody).Replace('__ALIAS_BODY__', $AliasBody)
        $result = Invoke-InFreshRunspace -Stub $stubText -Command $command

        $result.Calls | Should -Not -Contain 'Compress-LambdaPSFunctionPackage'
        $result.Calls | Should -Not -Contain 'Publish-LambdaPSFunction'
        $result.Output | Should -BeNullOrEmpty
        $result.Errors.FullyQualifiedErrorId | Should -Contain "$ErrorId,New-LambdaPSFunctionDeployment"
    }

    It 'Compress-LambdaPSFunctionPackage emits no package and keeps no stale .zip when staging fails' {
        $source = [System.IO.Path]::Combine($TestDrive, 'pkg')
        $null = New-Item -ItemType Directory -Path $source
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext) "ok"'
        $package = "$source.zip"
        Set-Content -LiteralPath $package -Value 'an older package'
        $command = ("Compress-LambdaPSFunctionPackage -Path '$source' -OutFile '$package' -Force " +
            "-IncludeModule @{ ModuleName = 'LambdaPSNoSuchModule'; RequiredVersion = '1.0.0' } -MissingModuleAction Stop")
        # A temp folder of its own, so another build's staging folders in the shared one don't count.
        $privateTemp = [System.IO.Path]::Combine($TestDrive, 'stage-temp')
        $null = New-Item -ItemType Directory -Path $privateTemp
        $savedTemp = $env:TMPDIR, $env:TMP, $env:TEMP
        $env:TMPDIR = $env:TMP = $env:TEMP = $privateTemp
        try {
            [System.IO.Path]::GetTempPath().TrimEnd([System.IO.Path]::DirectorySeparatorChar) | Should -Be $privateTemp
            $result = Invoke-InFreshRunspace -Command $command
            $stagesAfter = @(Get-ChildItem -LiteralPath $privateTemp -Directory -Filter 'lambdaps-fn-*').Count
        }
        finally { $env:TMPDIR, $env:TMP, $env:TEMP = $savedTemp }

        $result.Output | Should -BeNullOrEmpty
        $result.Errors.FullyQualifiedErrorId | Should -Contain 'ModuleNotFoundLocally,Compress-LambdaPSFunctionPackage'
        # The older package is left as it was rather than reported as this build's output.
        Get-Content -LiteralPath $package -Raw | Should -Match 'an older package'
        @(Get-ChildItem -LiteralPath ([System.IO.Path]::GetDirectoryName($package)) -Filter "$([System.IO.Path]::GetFileName($package)).*.partial") |
            Should -BeNullOrEmpty
        # The staging folder it made in the temp folder is removed too.
        $stagesAfter | Should -Be 0
    }
}
