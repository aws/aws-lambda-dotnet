# -Confirm across the deploy stages: each stage asks once, and declining a stage skips the stages
# after it, which need its output. Invoke-LambdaPSFunction asks once per function. Run in a child
# pwsh, whose console answers the prompts.

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Declining a stage under -Confirm' -Tag 'Component' {
    It 'skips the function create when packaging is declined, rather than deploy a stale .zip' {
        $handler = [System.IO.Path]::Combine($TestDrive, 'handler.ps1')
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)'
        $stub = {
            function script:Get-LambdaPSRuntimeArn { 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' }
            function script:Get-LambdaPSRuntimeAliasArchitecture { , [string[]]@('x86_64') }
            function script:Get-LambdaPSFunctionBinding { $null }
            function script:Compress-LambdaPSFunctionPackage { $script:LambdaPSCalls.Add('Compress') }
            function script:Publish-LambdaPSFunction { $script:LambdaPSCalls.Add('Publish') }
        }
        $command = "New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath '$handler' -RuntimeName rt -Role arn:aws:iam::111122223333:role/r -Region us-east-1 -PackagePath '$TestDrive/fn.zip' -Confirm"

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command -Answer 'N'

        $result.Calls | Should -BeNullOrEmpty
        $result.Output | Should -BeLike '*Skipped creating or updating function ''fn'', because packaging was declined*'
    }

    It 'packages but does not create the function when only the create is declined' {
        $handler = [System.IO.Path]::Combine($TestDrive, 'handler-create.ps1')
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)'
        $stub = {
            function script:Get-LambdaPSRuntimeArn { 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' }
            function script:Get-LambdaPSRuntimeAliasArchitecture { , [string[]]@('x86_64') }
            function script:Get-LambdaPSFunctionBinding { $null }
            function script:Compress-LambdaPSFunctionPackage {
                $script:LambdaPSCalls.Add('Compress')
                [PSCustomObject]@{ ModulesStaged = @([PSCustomObject]@{ Name = 'AWS.Tools.S3'; Version = [version]'5.0.1' }) }
            }
            function script:Publish-LambdaPSFunction { $script:LambdaPSCalls.Add('Publish') }
        }
        $command = ("`$r = New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath '$handler' -RuntimeName rt " +
            "-Role arn:aws:iam::111122223333:role/r -Region us-east-1 -PackagePath '$TestDrive/fn-create.zip' -Confirm; " +
            "'RESULT arn=[' + `$r.FunctionArn + '] updated=' + `$r.Updated + ' staged=' + (@(`$r.ModulesStaged.Name) -join ',')")

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command -Answer 'Y', 'N'

        $result.Calls | Should -Be @('Compress')
        # The result still comes back, with what was packaged but no function.
        $result.Output | Should -BeLike '*RESULT arn=[[]] updated=False staged=AWS.Tools.S3*'
    }

    It 'skips every later stage when the scaffold is declined, rather than build a stale project' {
        $project = [System.IO.Path]::Combine($TestDrive, 'rt-runtime-first')
        $stub = {
            function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }
            function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
            function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'rt' } }
            function script:Get-LMCRuntimeVersion { $null }
            function script:Get-LMCRuntimeAlias { $null }
            function script:Get-ECRImageMetadata { $null }
            function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
            function script:Build-LambdaPSRuntimeImage { $script:LambdaPSCalls.Add('Build') }
            function script:Initialize-LambdaPSEcrRepository { $script:LambdaPSCalls.Add('Repository') }
            function script:Publish-LambdaPSRuntimeVersion { $script:LambdaPSCalls.Add('Publish') }
            function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
        }
        $command = "New-LambdaPSRuntimeDeployment -RuntimeName rt -PowerShellVersion 7.4 -Region us-east-1 -ProjectPath '$project' -Confirm"

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command -Answer 'N'

        $result.Calls | Should -BeNullOrEmpty
        $result.Output | Should -BeLike '*Skipped building and pushing the image, because the scaffold stage was declined*'
    }

    It 'skips the publish and the alias when the image build and push is declined' {
        $project = [System.IO.Path]::Combine($TestDrive, 'rt-runtime')
        $stub = {
            function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }
            function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
            function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'rt' } }
            function script:Get-LMCRuntimeVersion { $null }
            function script:Get-LMCRuntimeAlias { $null }
            function script:Get-ECRImageMetadata { $null }
            function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
            function script:Build-LambdaPSRuntimeImage { $script:LambdaPSCalls.Add('Build') }
            function script:Initialize-LambdaPSEcrRepository { $script:LambdaPSCalls.Add('Repository') }
            function script:Publish-LambdaPSRuntimeVersion { $script:LambdaPSCalls.Add('Publish') }
            function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
        }
        $command = "New-LambdaPSRuntimeDeployment -RuntimeName rt -PowerShellVersion 7.4 -Region us-east-1 -ProjectPath '$project' -Confirm"

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command -Answer 'Y', 'N'

        $result.Calls | Should -Be @('Scaffold')
        $result.Output | Should -BeLike '*Skipped publishing the runtime version, because the build and push stage was declined*'
        $result.Output | Should -BeLike '*Skipped pointing alias ''default'' at ''v1''*'
    }

    It 'skips the alias when the runtime version publish is declined' {
        $project = [System.IO.Path]::Combine($TestDrive, 'rt-runtime-publish')
        $stub = {
            function script:Test-LambdaPSContainerRuntime { [PSCustomObject]@{ Name = 'docker'; Path = '/usr/bin/docker'; BuildxVersion = 'v0' } }
            function script:Get-STSCallerIdentity { [PSCustomObject]@{ Account = '111122223333' } }
            function script:Get-LMCRuntime { [PSCustomObject]@{ Name = 'rt' } }
            function script:Get-LMCRuntimeVersion { $null }
            function script:Get-LMCRuntimeAlias { $null }
            function script:Get-ECRImageMetadata { $null }
            function script:New-LambdaPSRuntimeProject { $script:LambdaPSCalls.Add('Scaffold') }
            function script:Build-LambdaPSRuntimeImage { $script:LambdaPSCalls.Add('Build') }
            function script:Initialize-LambdaPSEcrRepository { $script:LambdaPSCalls.Add('Repository') }
            function script:Publish-LambdaPSRuntimeVersion { $script:LambdaPSCalls.Add('Publish') }
            function script:Set-LambdaPSRuntimeAlias { $script:LambdaPSCalls.Add('Alias') }
        }
        $command = "New-LambdaPSRuntimeDeployment -RuntimeName rt -PowerShellVersion 7.4 -Region us-east-1 -ProjectPath '$project' -Confirm"

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command -Answer 'Y', 'Y', 'N'

        $result.Calls | Should -Not -Contain 'Publish'
        $result.Calls | Should -Not -Contain 'Alias'
        $result.Output | Should -BeLike '*Skipped pointing alias ''default'' at ''v1'', because the publish stage was declined*'
    }
}

Describe 'Confirming an invoke' -Tag 'Component' {
    BeforeAll {
        # Shaped like Invoke-LMFunction, which also asks under -Confirm (ConfirmImpact Low), so a
        # second prompt per function would show up as a consumed answer.
        $script:invokeStub = {
            function script:Invoke-LMFunction {
                [CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Low')]
                param($FunctionName, $Payload, $LogType, $Qualifier, $Region, $ClientConfig)
                if ($PSCmdlet.ShouldProcess($FunctionName, 'Invoke-LMFunction')) {
                    $script:LambdaPSCalls.Add($FunctionName)
                    [PSCustomObject]@{ Payload = '{}'; LogResult = $null; FunctionError = $null }
                }
            }
        }
    }

    It 'does not ask by default' {
        $result = Invoke-InChildPwsh -Stub $script:invokeStub.ToString() -Command "'fn-a', 'fn-b' | Invoke-LambdaPSFunction"

        $result.Calls | Should -Be @('fn-a', 'fn-b')
    }

    It 'asks once per function under -Confirm, and skips a declined one' {
        $result = Invoke-InChildPwsh -Stub $script:invokeStub.ToString() -Command "'fn-a', 'fn-b' | Invoke-LambdaPSFunction -Confirm" -Answer 'N', 'Y'

        $result.Calls | Should -Be @('fn-b')
    }

    It 'names the function, its qualifier and the payload size under -WhatIf' {
        $result = Invoke-InChildPwsh -Stub $script:invokeStub.ToString() -Command "Invoke-LambdaPSFunction -FunctionName fn-a -Qualifier live -Payload '{""a"":1}' -WhatIf"

        $result.Calls | Should -BeNullOrEmpty
        $result.Output | Should -BeLike '*What if: Performing the operation "Invoke Lambda function with a 7-byte JSON payload" on target "fn-a:live".*'
    }
}

Describe 'Previewing or confirming a scaffold' -Tag 'Component' {
    It 'shows one What if line for the whole template, not one per file' {
        $path = [System.IO.Path]::Combine($TestDrive, 'whatif-rt')
        $result = Invoke-InChildPwsh -Command "New-LambdaPSRuntimeProject -Path '$path' -PowerShellVersion 7.4 -WhatIf"

        $whatIf = @($result.Output -split "`n" | Where-Object { $_ -like 'What if:*' })
        # -BeLike on an array passes when any line matches, so count them: one line, not one per file.
        $whatIf.Count | Should -Be 1
        $whatIf | Should -BeLike 'What if: Performing the operation "Create * files from the CMR template" on target "*whatif-rt".'
        $path | Should -Not -Exist
    }

    It 'previews the -Force delete and the template copy, touching nothing' {
        $path = [System.IO.Path]::Combine($TestDrive, 'whatif-force-rt')
        $null = New-Item -ItemType Directory -Path $path -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($path, 'keep.txt')) -Value 'existing content'

        $result = Invoke-InChildPwsh -Command "New-LambdaPSRuntimeProject -Path '$path' -PowerShellVersion 7.4 -Force -WhatIf"

        $result.Output | Should -BeLike '*What if: Performing the operation "Delete existing contents before scaffolding (-Force)" on target "*whatif-force-rt"*'
        @($result.Output -split "`n" | Where-Object { $_ -like 'What if:*from the CMR template*' }).Count | Should -Be 1
        Get-Content -LiteralPath ([System.IO.Path]::Combine($path, 'keep.txt')) -Raw | Should -Match 'existing content'
        @(Get-ChildItem -LiteralPath $path -Force).Name | Should -Be @('keep.txt')
    }

    # The prompt text does not reach redirected stdout on every platform, so these assert what was written.
    It 'declining the -Confirm prompt writes nothing' {
        $path = [System.IO.Path]::Combine($TestDrive, 'confirm-fn')
        $null = Invoke-InChildPwsh -Command "New-LambdaPSFunctionProject -Path '$path' -Handler handler -Confirm" -Answer 'N'

        $path | Should -Not -Exist
    }

    # A prompt per file would take the 'N' for the second file and leave the project incomplete.
    It 'asks once under -Confirm: one yes writes the whole project' {
        $path = [System.IO.Path]::Combine($TestDrive, 'confirm-fn-yes')
        $reference = [System.IO.Path]::Combine($TestDrive, 'confirm-fn-reference')
        $null = New-LambdaPSFunctionProject -Path $reference -Handler handler

        $null = Invoke-InChildPwsh -Command "New-LambdaPSFunctionProject -Path '$path' -Handler handler -Confirm" -Answer 'Y', 'N'

        $written = @(Get-ChildItem -LiteralPath $path -Recurse -File -Force | ForEach-Object { [System.IO.Path]::GetRelativePath($path, $_.FullName) } | Sort-Object)
        $expected = @(Get-ChildItem -LiteralPath $reference -Recurse -File -Force | ForEach-Object { [System.IO.Path]::GetRelativePath($reference, $_.FullName) } | Sort-Object)
        $written.Count | Should -BeGreaterThan 0
        $written | Should -Be $expected
    }
}
