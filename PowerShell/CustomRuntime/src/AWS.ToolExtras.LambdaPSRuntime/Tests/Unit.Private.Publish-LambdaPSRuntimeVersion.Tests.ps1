BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Publish-LambdaPSRuntimeVersion' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'creates the runtime (New-LMCRuntime -Name) then publishes a version (-RuntimeName/-Name) and waits for ACTIVE' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v1' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }

            $result = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1'

            $result.Version | Should -Be 'v1'
            $result.State | Should -Be 'ACTIVE'
            $result.Arn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v1'
            Should -Invoke 'New-LMCRuntime' -Times 1 -Exactly -ParameterFilter { $Name -eq 'my-ps' }
            Should -Invoke 'New-LMCRuntimeVersion' -Times 1 -Exactly `
                -ParameterFilter { $RuntimeName -eq 'my-ps' -and $Name -eq 'v1' -and $ImageUri -eq 'uri:v1' }
        }
    }

    It 'sends -ReleaseNote only when it is given' -ForEach @(@{ Note = 'pwsh 7.6.9' }, @{ Note = $null }) {
        InModuleScope $script:moduleName -Parameters @{ Note = $Note } {
            param($Note)
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $ReleaseNote, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps/version/v1' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }
            $splat = if ($Note) { @{ ReleaseNote = $Note } } else { @{} }

            $null = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' @splat

            Should -Invoke 'New-LMCRuntimeVersion' -Times 1 -Exactly -ParameterFilter { $ReleaseNote -eq $Note }
        }
    }

    # The orchestrator discards the seam's result with '$null =', so a version that settled to FAILED
    # must THROW: returning it would let the alias be pointed at it and the deploy reported as a
    # success, and every invoke of the resulting function would fail. FAILED is
    # easy to hit: Lambda pulls by digest and rejects an OCI manifest, which recent BuildKit produces
    # by default.
    It 'throws RuntimeVersionNotActive when the version settles to FAILED instead of returning it' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'FAILED' } }

            { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' } |
                Should -Throw -ErrorId 'RuntimeVersionNotActive'
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' }
            catch { $_.TargetObject | Should -Be 'v1' }
        }
    }

    It 'names the image and the Schema 2 cause in the not-active error' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'FAILED' } }

            $threw = $false
            try {
                Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'acct.dkr.ecr.us-east-1.amazonaws.com/rt:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1'
            }
            catch {
                $threw = $true
                $_.Exception.Message | Should -Match 'FAILED'
                $_.Exception.Message | Should -Match 'acct\.dkr\.ecr\.us-east-1\.amazonaws\.com/rt:v1'
                $_.Exception.Message | Should -Match 'Schema 2'
                # The version now exists, so a rerun needs another name; and the cause is something to check,
                # not a rebuild with the tool that just built it.
                $_.Exception.Message | Should -BeLike "*Version 'v1' already exists and is immutable, so publish under a new -RuntimeVersion, or remove it with Remove-LMCRuntimeVersion -RuntimeName my-ps -Name v1 -Region us-east-1.*"
                $_.Exception.Message | Should -BeLike '*docker buildx imagetools inspect acct.dkr.ecr.us-east-1.amazonaws.com/rt:v1*'
                $_.Exception.Message | Should -BeLike '*or Lambda could not pull it (check that the repository policy grants lambda.amazonaws.com pull for this runtime''s ARN)*'
                $_.Exception.Message | Should -Not -BeLike '*Rebuild with*'
            }
            $threw | Should -BeTrue
        }
    }

    It 'gives the image cause only for FAILED, not for a version that is DELETING' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'DELETING' } }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' }
            catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionNotActive'
            $err.Exception.Message | Should -BeLike "*settled in state 'DELETING'*"
            $err.Exception.Message | Should -Not -Match 'Schema 2'
        }
    }

    It 'throws RuntimeVersionTimeout when the version never leaves CREATING' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'CREATING' } }
            # Keep the test fast: the seam sleeps between polls, so stub the sleep out.
            Mock 'Start-Sleep' -MockWith { }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' }
            catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionTimeout'
            # The wait budget is data (Config/wait.psd1), and the message reports the time it allows.
            $wait = $script:Config.wait.RuntimeVersion
            $err.Exception.Message | Should -BeLike "*still being created after $($wait.Attempts * $wait.IntervalSeconds) seconds*"
            Should -Invoke 'Get-LMCRuntimeVersion' -Times $wait.Attempts -Exactly
            Should -Invoke 'Start-Sleep' -Times $wait.Attempts -Exactly -ParameterFilter { $Seconds -eq $wait.IntervalSeconds }
        }
    }

    It 'names the caller''s profile in the commands the timeout error suggests' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'CREATING' } }
            Mock 'Start-Sleep' -MockWith { }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' -CredentialParameter @{ ProfileName = 'prod' } }
            catch { $err = $_ }
            $err.Exception.Message | Should -BeLike "*Get-LMCRuntimeVersion -RuntimeName my-ps -Name v1 -Region us-east-1 -ProfileName 'prod'*"
            $err.Exception.Message | Should -BeLike "*Update-LMCRuntimeAlias -RuntimeName my-ps -Name <alias> -RuntimeVersion v1 -Region us-east-1 -ProfileName 'prod'*"
        }
    }

    It 'polls until CREATING settles to ACTIVE' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Start-Sleep' -MockWith { }
            $script:pollCount = 0
            Mock 'Get-LMCRuntimeVersion' -MockWith {
                $script:pollCount++
                if ($script:pollCount -lt 3) { [PSCustomObject]@{ State = 'CREATING' } }
                else { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }
            }

            $result = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1'

            $result.State | Should -Be 'ACTIVE'
            Should -Invoke 'Get-LMCRuntimeVersion' -Times 3 -Exactly
        }
    }

    It 'keeps waiting while the new version is not readable yet' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Start-Sleep' -MockWith { }
            $script:pollCount = 0
            Mock 'Get-LMCRuntimeVersion' -MockWith {
                $script:pollCount++
                if ($script:pollCount -lt 2) { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Version not found') }
                [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }

            $result = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1'

            $result.State | Should -Be 'ACTIVE'
            Should -Invoke 'Get-LMCRuntimeVersion' -Times 2 -Exactly
        }
    }

    It 'stops with the real cause when polling fails, rather than reporting a settled state' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntimeVersion') }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' }
            catch { $err = $_ }

            $err | Should -Not -BeNullOrEmpty
            $err.Exception.Message | Should -Match 'lambda:GetRuntimeVersion'
            $err.FullyQualifiedErrorId | Should -Not -Match 'RuntimeVersionNotActive'
        }
    }

    # The runtime and the version exist by then, so the error says so and how to finish, rather than
    # surfacing the service's error alone (a rerun publishes another version instead).
    It 'wraps a failed read during the wait as RuntimeVersionWaitFailed, naming the created version' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntimeVersion') }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' -RuntimeAlias 'default' }
            catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionWaitFailed'
            $err.Exception.InnerException.GetType().Name | Should -Be 'AccessDeniedException'
            $err.Exception.Message | Should -BeLike "Runtime 'my-ps' version 'v1' was created, but its state could not be read: not authorized to perform lambda:GetRuntimeVersion.*"
            $err.Exception.Message | Should -BeLike '*Get-LMCRuntimeVersion -RuntimeName my-ps -Name v1 -Region us-east-1*'
            $err.Exception.Message | Should -BeLike '*Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v1 -Region us-east-1*'
        }
    }

    It 'names the caller''s alias in the timeout error''s alias command when it is given' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'CREATING' } }
            Mock 'Start-Sleep' -MockWith { }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' -RuntimeAlias 'default' }
            catch { $err = $_ }

            $err.Exception.Message | Should -BeLike '*Update-LMCRuntimeAlias -RuntimeName my-ps -Name default -RuntimeVersion v1*'
            $err.Exception.Message | Should -Not -BeLike '*<alias>*'
        }
    }

    It 'names the alias commands, not a deploy re-run, in the timeout error' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'CREATING' } }
            Mock 'Start-Sleep' -MockWith { }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' }
            catch { $err = $_ }

            $err.Exception.Message | Should -Match 'Update-LMCRuntimeAlias -RuntimeName my-ps -Name <alias> -RuntimeVersion v1'
            $err.Exception.Message | Should -Not -Match 'New-LambdaPSRuntimeDeployment'
            # The architecture check never ran, so the alias advice asks for it.
            $err.Exception.Message | Should -BeLike "*ACTIVE and its Compatibility.Architectures is 'x86_64'*"
        }
    }

    It 'throws RuntimeVersionExists, without waiting, when a concurrent deploy published the version name first' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntimeVersion' -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Could not create version! Version already exists.') }
            Mock 'Get-LMCRuntimeVersion' -MockWith { }

            $err = $null
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:7.6.9-9' -RuntimeVersion '7.6.9-9' -Architecture x86_64 -Region 'us-east-1' }
            catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be 'RuntimeVersionExists'
            $err.Exception.InnerException.GetType().Name | Should -Be 'ResourceConflictException'
            $err.Exception.Message | Should -BeLike "Runtime 'my-ps' already has version '7.6.9-9': another deploy published it*"
            Should -Invoke 'Get-LMCRuntimeVersion' -Times 0 -Exactly
        }
    }

    It 'tolerates an already-existing runtime (New-LMCRuntime throws already-exists)' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith {
                throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Runtime already exists')
            }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }

            $result = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v2' -RuntimeVersion 'v2' -Architecture x86_64 -Region 'us-east-1'

            $result.Version | Should -Be 'v2'
            Should -Invoke 'New-LMCRuntimeVersion' -Times 1 -Exactly
        }
    }

    # The runtime is looked up first and New-LMCRuntime is not called when it exists, so a re-deploy
    # makes no failed create call (a create of an existing name would be refused).
    It 'reuses an existing runtime found by Get-LMCRuntime without calling New-LMCRuntime' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' } }
            Mock 'New-LMCRuntime' -MockWith { throw [System.InvalidOperationException]::new('Runtime already exists !') }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }

            $result = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v2' -RuntimeVersion 'v2' -Architecture x86_64 -Region 'us-east-1'

            $result.Version | Should -Be 'v2'
            Should -Invoke 'New-LMCRuntime' -Times 0 -Exactly
            Should -Invoke 'New-LMCRuntimeVersion' -Times 1 -Exactly
        }
    }

    It 'rethrows a Get-LMCRuntime error other than not-found instead of creating' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('not authorized to perform lambda:GetRuntime') }
            Mock 'New-LMCRuntime' -MockWith { }

            { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' } |
                Should -Throw '*not authorized*'
            Should -Invoke 'New-LMCRuntime' -Times 0 -Exactly
        }
    }

    It 'rethrows a non-already-exists New-LMCRuntime error instead of masking it' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            # The message deliberately contains the word 'exists': a permission failure must surface
            # even when its wording looks like an already-exists condition.
            Mock 'New-LMCRuntime' -MockWith {
                throw [LambdaPSTests.Fakes.AccessDeniedException]::new(
                    'not authorized to perform lambda:CreateRuntime because no identity-based policy exists')
            }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }

            { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' } |
                Should -Throw -ExceptionType ([LambdaPSTests.Fakes.AccessDeniedException])
            Should -Invoke 'New-LMCRuntimeVersion' -Times 0 -Exactly
        }
    }

    It 'passes the caller''s credential parameters to every runtime call' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction, $ProfileName) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } } }

            $null = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' -CredentialParameter @{ ProfileName = 'p' }
            foreach ($command in 'Get-LMCRuntime', 'New-LMCRuntime', 'New-LMCRuntimeVersion', 'Get-LMCRuntimeVersion') {
                Should -Invoke $command -Times 1 -Exactly -ParameterFilter { $ProfileName -eq 'p' }
            }
        }
    }

    It 'calls no runtime cmdlet under -WhatIf' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { }

            Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' -WhatIf |
                Should -BeNullOrEmpty
            Should -Invoke 'Get-LMCRuntime' -Times 0 -Exactly
            Should -Invoke 'New-LMCRuntime' -Times 0 -Exactly
            Should -Invoke 'New-LMCRuntimeVersion' -Times 0 -Exactly
        }
    }

    It 'rejects a whitespace runtime name at parameter binding' {
        InModuleScope $script:moduleName {
            { Publish-LambdaPSRuntimeVersion -RuntimeName '   ' -ImageUri 'uri' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Publish-LambdaPSRuntimeVersion'
        }
    }
}

# The runtime version must DECLARE its architecture, or the function deploy has nothing to check a
# mismatch against before Lambda rejects the function at creation.
Describe 'Publish-LambdaPSRuntimeVersion architecture declaration' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'passes -Architecture to New-LMCRuntimeVersion as Compatibility_Architecture' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith {
                [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }

            $result = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture arm64 -Region 'us-east-1'

            $result.Architecture | Should -Be 'arm64'
            Should -Invoke 'New-LMCRuntimeVersion' -Times 1 -Exactly -ParameterFilter {
                @($Compatibility_Architecture).Count -eq 1 -and $Compatibility_Architecture[0] -eq 'arm64'
            }
        }
    }

    It 'does not warn when the service echoes the declared architecture' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith {
                [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }

            $warnings = @()
            $null = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1' -WarningVariable warnings
            $warnings.Count | Should -Be 0
        }
    }

    # The service reports the architecture it reads from the image MANIFEST and ignores
    # the declared value. So a mismatch means the image was built for the wrong platform, and aliasing
    # it would put every adopting function on the wrong architecture.
    It 'throws RuntimeImageArchitectureMismatch when the image is not the requested architecture' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith {
                [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }

            { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture arm64 -Region 'us-east-1' } |
                Should -Throw -ErrorId 'RuntimeImageArchitectureMismatch'
        }
    }

    It 'names the actual image architecture and the builder cause in the mismatch error' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith {
                [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = [PSCustomObject]@{ Architectures = @('x86_64') } }
            }

            $threw = $false
            try { Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture arm64 -Region 'us-east-1' }
            catch {
                $threw = $true
                $_.Exception.Message | Should -Match '\[x86_64\]'
                $_.Exception.Message | Should -Match 'buildx'
                $_.Exception.Message | Should -Match 'NOT pointed'
                $_.Exception.Message | Should -BeLike '*Remove-LMCRuntimeVersion -RuntimeName my-ps -Name v1 -Region us-east-1.'
            }
            $threw | Should -BeTrue
        }
    }

    It 'warns, but does not fail, when the service reports no architecture at all' {
        InModuleScope $script:moduleName {
            function New-LMCRuntime { param($Name, $Region, $ClientConfig, $ErrorAction) }
            # The runtime is looked up before it is created; by default it does not exist yet.
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'New-LMCRuntime' -MockWith { }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = 'ACTIVE'; Compatibility = $null } }

            $warnings = @()
            $null = Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture arm64 -Region 'us-east-1' -WarningVariable warnings -WarningAction SilentlyContinue
            ($warnings -join ' ') | Should -Match "could not be confirmed as 'arm64'"
        }
    }
}

# New-LambdaPSRuntimeDeployment calls this inside try and rethrows with its own $PSCmdlet; the error it
# surfaces, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Publish-LambdaPSRuntimeVersion errors through a calling cmdlet' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'RuntimeVersionNotActive'; State = 'FAILED'; Architectures = @('x86_64') }
        @{ ErrorId = 'RuntimeVersionTimeout'; State = 'CREATING'; Architectures = @('x86_64') }
        @{ ErrorId = 'RuntimeImageArchitectureMismatch'; State = 'ACTIVE'; Architectures = @('arm64') }
    ) {
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; State = $State; Architectures = $Architectures } {
            param($ErrorId, $State, $Architectures)
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            function New-LMCRuntimeVersion { param($RuntimeName, $ImageUri, $Name, $Compatibility_Architecture, $Region, $ClientConfig, $ErrorAction) }
            function Get-LMCRuntimeVersion { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{} }
            Mock 'New-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ Arn = 'arn:x' } }
            Mock 'Get-LMCRuntimeVersion' -MockWith { [PSCustomObject]@{ State = $State; Compatibility = [PSCustomObject]@{ Architectures = $Architectures } } }
            Mock 'Start-Sleep' -MockWith { }
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
                    Publish-LambdaPSRuntimeVersion -RuntimeName 'my-ps' -ImageUri 'uri:v1' -RuntimeVersion 'v1' -Architecture x86_64 -Region 'us-east-1'
                }
            }
            catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Publish-LambdaPSRuntimeVersion' }) | Should -BeNullOrEmpty
        }
    }
}
