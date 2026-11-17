# Integration tests: each deployment target is scaffolded, built, deployed to AWS and invoked once (the
# RuntimeImage function is then redeployed and invoked again), and everything the run created is removed
# afterwards. They are tagged 'Integration' and run only with
#
#     pwsh ./build.ps1 -Task Integration
#
# which sets LAMBDAPS_INTEGRATION_TESTS; without it this file skips itself before any AWS call, and
# every other build task leaves the 'Integration' tag out. It runs where these environment variables
# say, all of which it requires:
#   LAMBDAPS_INTEGRATION_REGION    the region to deploy into;
#   LAMBDAPS_INTEGRATION_ACCOUNT   the account the credentials must be for (the suite refuses any other);
#   LAMBDAPS_INTEGRATION_ROLE_ARN  an existing Lambda execution role in that account.
# It needs credentials for that account (as for any AWS.Tools cmdlet: Set-AWSCredential,
# AWS_PROFILE, ...), docker with the buildx plugin, and AWS.Tools.S3 and AWS.Tools.CloudWatchLogs
# besides the AWS.Tools modules the module requires.
#
# What it creates. Every name starts with lambdaps-integration-<random>-, so one run cannot meet another's:
#   - an S3 bucket (<prefix>artifacts) holding the layer and the self-contained .zip, which are over
#     Lambda's direct-upload limit;
#   - LayerRuntime: a layer (<prefix>layer) and a provided.al2023 function (<prefix>layer) that uses it;
#   - SelfContainedZip: a provided.al2023 function (<prefix>zip);
#   - ContainerFunction: an ECR repository with one image, and an image function (<prefix>container);
#   - RuntimeImage (CMR): an ECR repository with one image, a customer-managed runtime with a version named
#     after its pwsh release (for example 7.6.9-1) and alias default (<prefix>cmr), and a function bound to it (<prefix>cmr-fn), which is
#     deployed twice;
#   - the CloudWatch log group each function writes (/aws/lambda/<function name>).
# AfterAll removes all of it, removes the log groups again after 30 seconds in case a late log delivery
# created one again, and fails listing anything it could not remove. Lambda deletes a runtime's alias and
# versions days later, so the runtime is left, with a warning naming it. Nothing whose name does not start
# with this run's prefix is changed.
#
# What it costs: five invocations at 512 MB, and a few hundred MB of ECR and S3 storage held only until
# AfterAll, with the requests to upload and remove it. At on-demand prices that is under
# US$0.01 a run; data transfer into AWS (the image pushes and the .zip uploads) is not charged. The
# image and package builds run on the build host.

BeforeDiscovery {
    $script:IntegrationEnabled = $env:LAMBDAPS_INTEGRATION_TESTS -eq 'true'
}

Describe 'Integration end to end' -Tag 'Integration' -Skip:(-not $script:IntegrationEnabled) {
    BeforeAll {
        # First, so that AfterAll has them whatever fails below.
        $script:Prefix = 'lambdaps-integration-{0}-' -f (-join (Get-Random -Count 8 -InputObject ([char[]]'abcdefghijklmnopqrstuvwxyz0123456789')))
        # What this run created, in order, each with the script block that removes it.
        $script:Created = [System.Collections.Generic.List[object]]::new()
        # The registries docker logged in to, logged out of in AfterAll.
        $script:DockerRegistries = [System.Collections.Generic.List[string]]::new()
        $missing = @('LAMBDAPS_INTEGRATION_REGION', 'LAMBDAPS_INTEGRATION_ACCOUNT', 'LAMBDAPS_INTEGRATION_ROLE_ARN' |
                Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
        if ($missing) { throw "The integration suite needs these environment variables set: $($missing -join ', ')." }
        $script:Region = $env:LAMBDAPS_INTEGRATION_REGION

        . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
        Import-Module AWS.Tools.S3 -ErrorAction Stop
        Import-Module AWS.Tools.CloudWatchLogs -ErrorAction Stop

        $script:Account = $env:LAMBDAPS_INTEGRATION_ACCOUNT
        $script:Role = $env:LAMBDAPS_INTEGRATION_ROLE_ARN
        $script:Work = [System.IO.Path]::Combine($TestDrive, 'integration')
        $null = New-Item -ItemType Directory -Path $script:Work

        $identity = Get-STSCallerIdentity -Region $script:Region -ErrorAction Stop
        if ($identity.Account -ne $script:Account) {
            throw "The integration suite runs in account $script:Account only; these credentials are for account $($identity.Account)."
        }
        Write-Host "Integration suite: prefix '$script:Prefix', region $script:Region."

        # A resource whose name is known in advance is recorded BEFORE the call that creates it, so a call
        # that fails part-way is still cleaned up; removing one that was never created is a not-found,
        # which counts as removed. A layer version, whose number Lambda assigns, is recorded once created.
        # A function also gets the log group Lambda creates for it on its first invoke. That is recorded
        # first, so it is removed after the function, which could otherwise write to it again.
        function Add-IntegrationResource {
            param([string]$Kind, [string]$Name, [scriptblock]$Remove)
            if (-not $Name.StartsWith($script:Prefix)) { throw "Refusing to record '$Name': it is not one of this run's names." }
            if ($Kind -eq 'function') {
                $logGroup = "/aws/lambda/$Name"
                $region = $script:Region
                $script:Created.Add([PSCustomObject]@{ Kind = "log group $logGroup"; Name = $Name; Remove = {
                            Remove-CWLLogGroup -LogGroupName $logGroup -Region $region -Force -ErrorAction Stop
                        }.GetNewClosure() })
            }
            $script:Created.Add([PSCustomObject]@{ Kind = $Kind; Name = $Name; Remove = $Remove })
        }

        function Test-IntegrationNotFound {
            param($ErrorRecord)
            for ($exception = $ErrorRecord.Exception; $exception; $exception = $exception.InnerException) {
                if ($exception.GetType().Name -match 'NotFound|NoSuch' -or [string]$exception.ErrorCode -match 'NotFound|NoSuch') { return $true }
            }
            $false
        }

        # One removal pass over the recorded resources. It returns a failure record for each one it could
        # not remove, and nothing else: a Remove block's own output is discarded, so it cannot be taken
        # for a failure. A missing (not-found) resource counts as removed.
        function Remove-IntegrationResourceSet {
            param([object[]]$Resource, [string]$Prefix)
            foreach ($item in $Resource) {
                if ($null -eq $item) { continue }
                if (-not ([string]$item.Name).StartsWith($Prefix)) {
                    [PSCustomObject]@{ Resource = $item; Message = 'not one of this run''s names, so it was left alone' }
                    continue
                }
                try { $null = & $item.Remove }
                catch {
                    if (-not (Test-IntegrationNotFound $_)) { [PSCustomObject]@{ Resource = $item; Message = $_.Exception.Message } }
                }
            }
        }

        # Lambda refuses an invoke while a new function is Pending.
        function Wait-IntegrationFunction {
            param([string]$FunctionName)
            for ($i = 0; $i -lt 90; $i++) {
                $configuration = Get-LMFunctionConfiguration -FunctionName $FunctionName -Region $script:Region -ErrorAction Stop
                if ([string]$configuration.State -eq 'Active' -and [string]$configuration.LastUpdateStatus -ne 'InProgress') { return }
                if ([string]$configuration.State -eq 'Failed') { throw "Function '$FunctionName' failed: $($configuration.StateReason)" }
                Start-Sleep -Seconds 2
            }
            throw "Function '$FunctionName' did not become Active."
        }

        function New-IntegrationFunctionPackage {
            param([string]$Name)
            $project = [System.IO.Path]::Combine($script:Work, $Name)
            $null = New-LambdaPSFunctionProject -Path $project -Handler handler
            $zip = [System.IO.Path]::Combine($script:Work, "$Name.zip")
            $null = Compress-LambdaPSFunctionPackage -Path $project -OutFile $zip
            $zip
        }

        $script:Bucket = $bucket = $script:Prefix + 'artifacts'
        $region = $script:Region
        Add-IntegrationResource -Kind 'S3 bucket' -Name $bucket -Remove {
            Remove-S3Bucket -BucketName $bucket -DeleteBucketContent -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $null = New-S3Bucket -BucketName $script:Bucket -Region $script:Region -ErrorAction Stop
    }

    AfterAll {
        # Removed newest first: a function before its layer or runtime, an alias before its version.
        # What fails is tried once more, since Lambda can still be releasing a function that was just
        # deleted, then listed.
        # Nothing was recorded when BeforeAll stopped before creating anything, so there is nothing to remove.
        $pending = $script:Created.ToArray()
        [array]::Reverse($pending)
        $failed = @()
        foreach ($attempt in 1, 2) {
            if (-not $pending) { break }
            $failed = @(Remove-IntegrationResourceSet -Resource $pending -Prefix $script:Prefix)
            $pending = @($failed | ForEach-Object Resource)
            if (-not $pending -or $attempt -eq 2) { break }
            Start-Sleep -Seconds 15
        }
        # A late log delivery from Lambda creates a removed log group again. A delivery later than the
        # fixed wait still leaves one behind.
        $logGroups = @($script:Created | Where-Object { $_.Kind -like 'log group *' -and $failed.Resource -notcontains $_ })
        if ($logGroups) {
            Start-Sleep -Seconds 30
            $failed += @(Remove-IntegrationResourceSet -Resource $logGroups -Prefix $script:Prefix)
        }
        foreach ($registry in $script:DockerRegistries) { $null = & docker logout $registry 2>&1 }
        if (Get-Command Remove-LambdaPSTestModule -ErrorAction SilentlyContinue) { Remove-LambdaPSTestModule }
        if ($failed) {
            $list = ($failed | ForEach-Object { "  $($_.Resource.Kind) '$($_.Resource.Name)': $($_.Message)" }) -join "`n"
            throw "The integration suite could not remove these resources in $script:Region; remove them by hand:`n$list"
        }
    }

    It 'LayerRuntime: a provided.al2023 function with the runtime layer answers an invoke' {
        $region = $script:Region
        $layerProject = [System.IO.Path]::Combine($script:Work, 'layer')
        $null = New-LambdaPSRuntimeProject -Path $layerProject -PowerShellVersion '7.4.*' -Target Layer
        $layerPackage = Build-LambdaPSRuntimePackage -Path $layerProject -OutFile ([System.IO.Path]::Combine($script:Work, 'layer.zip'))
        Write-S3Object -BucketName $script:Bucket -Key 'layer.zip' -File $layerPackage.PackagePath -Region $script:Region -ErrorAction Stop

        $layerName = $script:Prefix + 'layer'
        $layer = Publish-LMLayerVersion -LayerName $layerName -CompatibleRuntime 'provided.al2023' -CompatibleArchitecture $layerPackage.Architecture `
            -Content_S3Bucket $script:Bucket -Content_S3Key 'layer.zip' -Region $script:Region -ErrorAction Stop
        $layerVersion = $layer.Version
        Add-IntegrationResource -Kind 'layer version' -Name $layerName -Remove {
            Remove-LMLayerVersion -LayerName $layerName -VersionNumber $layerVersion -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()

        $functionName = $script:Prefix + 'layer'
        $code = New-IntegrationFunctionPackage -Name 'layer-fn'
        Add-IntegrationResource -Kind 'function' -Name $functionName -Remove {
            Remove-LMFunction -FunctionName $functionName -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $null = Publish-LMFunction -FunctionName $functionName -Runtime 'provided.al2023' -Handler 'handler.ps1' -Layer $layer.LayerVersionArn `
            -Architecture $layerPackage.Architecture -Role $script:Role -Timeout 30 -MemorySize 512 `
            -Code_ZipFile ([System.IO.File]::ReadAllBytes($code)) -Region $script:Region -ErrorAction Stop
        Wait-IntegrationFunction -FunctionName $functionName

        $response = Invoke-LambdaPSFunction -FunctionName $functionName -Payload @{ name = 'live' } -Region $script:Region -ErrorAction Stop

        $response.message | Should -BeExactly 'Hello, live'
        $response.function | Should -BeExactly $functionName
    }

    It 'SelfContainedZip: a provided.al2023 function that carries its own runtime answers an invoke' {
        $region = $script:Region
        $project = [System.IO.Path]::Combine($script:Work, 'zip')
        $null = New-LambdaPSFunctionProject -Path $project -Handler handler -Target SelfContainedZip -PowerShellVersion '7.4.*'
        $package = Build-LambdaPSRuntimePackage -Path $project -OutFile ([System.IO.Path]::Combine($script:Work, 'zip.zip'))
        Write-S3Object -BucketName $script:Bucket -Key 'zip.zip' -File $package.PackagePath -Region $script:Region -ErrorAction Stop

        $functionName = $script:Prefix + 'zip'
        Add-IntegrationResource -Kind 'function' -Name $functionName -Remove {
            Remove-LMFunction -FunctionName $functionName -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $null = Publish-LMFunction -FunctionName $functionName -Runtime 'provided.al2023' -Handler 'handler.ps1' `
            -Architecture $package.Architecture -Role $script:Role -Timeout 30 -MemorySize 512 `
            -Code_S3Bucket $script:Bucket -Code_S3Key 'zip.zip' -Region $script:Region -ErrorAction Stop
        Wait-IntegrationFunction -FunctionName $functionName

        $response = Invoke-LambdaPSFunction -FunctionName $functionName -Payload @{ name = 'live' } -Region $script:Region -ErrorAction Stop

        $response.Message | Should -BeExactly 'Hello from PowerShell on Lambda (self-contained zip)'
        $response.Echo.name | Should -BeExactly 'live'
    }

    It 'ContainerFunction: an image function that carries its own runtime answers an invoke' {
        $region = $script:Region
        $project = [System.IO.Path]::Combine($script:Work, 'container')
        $null = New-LambdaPSFunctionProject -Path $project -Handler handler -Target Container -PowerShellVersion '7.4.*'

        $repositoryName = $script:Prefix + 'container'
        Add-IntegrationResource -Kind 'ECR repository' -Name $repositoryName -Remove {
            Remove-ECRRepository -RepositoryName $repositoryName -IgnoreExistingImages $true -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $repository = New-ECRRepository -RepositoryName $repositoryName -Region $script:Region -ErrorAction Stop
        $login = Get-ECRLoginCommand -Region $script:Region -ErrorAction Stop
        $registry = $login.Endpoint -replace '^https?://', ''
        $script:DockerRegistries.Add($registry)
        $loginOutput = $login.Password | & docker login --username $login.Username --password-stdin $registry 2>&1
        if ($LASTEXITCODE -ne 0) { throw "docker login to the ECR registry $registry failed: $(($loginOutput | ForEach-Object { [string]$_ }) -join ' ')" }
        $image = Build-LambdaPSRuntimeImage -Path $project -ImageUri "$($repository.RepositoryUri):v1"

        $functionName = $script:Prefix + 'container'
        Add-IntegrationResource -Kind 'function' -Name $functionName -Remove {
            Remove-LMFunction -FunctionName $functionName -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $imageUri = if ($image.ImageDigest) { "$($repository.RepositoryUri)@$($image.ImageDigest)" } else { "$($repository.RepositoryUri):v1" }
        $null = Publish-LMFunction -FunctionName $functionName -PackageType Image -Code_ImageUri $imageUri `
            -Architecture $image.Architecture -Role $script:Role -Timeout 30 -MemorySize 512 -Region $script:Region -ErrorAction Stop
        Wait-IntegrationFunction -FunctionName $functionName

        $response = Invoke-LambdaPSFunction -FunctionName $functionName -Payload @{ name = 'live' } -Region $script:Region -ErrorAction Stop

        $response.Message | Should -BeExactly 'Hello from PowerShell on Lambda (container-per-function)'
        $response.Echo.name | Should -BeExactly 'live'
    }

    It 'RuntimeImage (CMR): a function bound to a customer-managed runtime answers an invoke, and a redeploy keeps its handler and alias' {
        $region = $script:Region
        # Region names are lowercase. The runtime deploy is given its region in upper case, and every
        # ARN that follows must carry it in lower case.
        $lowerRegion = $script:Region.ToLowerInvariant()
        $inRegion = "^arn:aws[a-z-]*:lambda:$([regex]::Escape($lowerRegion)):\d{12}:"
        $runtimeName = $script:Prefix + 'cmr'
        # New-LambdaPSRuntimeDeployment makes all four, so all four are recorded first.
        Add-IntegrationResource -Kind 'ECR repository' -Name $runtimeName -Remove {
            Remove-ECRRepository -RepositoryName $runtimeName -IgnoreExistingImages $true -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        Add-IntegrationResource -Kind 'runtime' -Name $runtimeName -Remove {
            # Lambda refuses to delete a runtime that still lists a version or alias, and a deleted alias
            # stays listed for days. Wait (up to two minutes) for other deletes to finish.
            for ($wait = 0; $wait -lt 24; $wait++) {
                $left = @(Get-LMCRuntimeAliasList -RuntimeName $runtimeName -Region $region -ErrorAction Stop) +
                    @(Get-LMCRuntimeVersionList -RuntimeName $runtimeName -Region $region -ErrorAction Stop)
                if (-not @($left | Where-Object { [string]$_.State -eq 'DELETING' })) { break }
                Start-Sleep -Seconds 5
            }
            if ($left) {
                Write-Warning "Runtime '$runtimeName' in $region and its versions are left, since its alias is scheduled for deletion. Remove them once Lambda has purged it."
                return
            }
            Remove-LMCRuntime -RuntimeName $runtimeName -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        # The deploy names the version after the pwsh release it installs, so every version the run's
        # own runtime lists is removed, except one already scheduled for deletion or one an alias scheduled
        # for deletion points at: Lambda refuses that delete, and the runtime is left with it either way.
        Add-IntegrationResource -Kind 'runtime versions of' -Name $runtimeName -Remove {
            $pendingAliasVersions = @(Get-LMCRuntimeAliasList -RuntimeName $runtimeName -Region $region -ErrorAction Stop |
                    Where-Object { [string]$_.State -eq 'PENDING_DELETION' } | ForEach-Object { [string]$_.RuntimeVersion })
            foreach ($version in @(Get-LMCRuntimeVersionList -RuntimeName $runtimeName -Region $region -ErrorAction Stop)) {
                if ([string]$version.State -eq 'PENDING_DELETION' -or $pendingAliasVersions -ccontains $version.Name) { continue }
                Remove-LMCRuntimeVersion -RuntimeName $runtimeName -Name $version.Name -Region $region -Force -ErrorAction Stop
            }
        }.GetNewClosure()
        Add-IntegrationResource -Kind 'runtime alias default of' -Name $runtimeName -Remove {
            Remove-LMCRuntimeAlias -RuntimeName $runtimeName -Name 'default' -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $runtime = New-LambdaPSRuntimeDeployment -RuntimeName $runtimeName -PowerShellVersion '7.4.*' -RuntimeAlias default -Region $script:Region.ToUpperInvariant() `
            -ProjectPath ([System.IO.Path]::Combine($script:Work, 'cmr')) -Confirm:$false
        $runtime.Region | Should -BeExactly $lowerRegion
        $runtime.RuntimeArn | Should -MatchExactly $inRegion

        $functionName = $script:Prefix + 'cmr-fn'
        $project = [System.IO.Path]::Combine($script:Work, 'cmr-fn')
        $null = New-LambdaPSFunctionProject -Path $project -Handler handler
        Add-IntegrationResource -Kind 'function' -Name $functionName -Remove {
            Remove-LMFunction -FunctionName $functionName -Region $region -Force -ErrorAction Stop
        }.GetNewClosure()
        $deployment = New-LambdaPSFunctionDeployment -FunctionName $functionName -ProjectPath $project -RuntimeName $runtimeName `
            -Role $script:Role -Region $script:Region -PackagePath ([System.IO.Path]::Combine($script:Work, 'cmr-fn.zip')) -Confirm:$false
        $deployment.RuntimeArn | Should -BeExactly $runtime.RuntimeArn
        $deployment.FunctionArn | Should -MatchExactly $inRegion

        $response = Invoke-LambdaPSFunction -FunctionName $functionName -Payload @{ name = 'live' } -Region $script:Region -ErrorAction Stop

        $response.message | Should -BeExactly 'Hello, live'
        $response.function | Should -BeExactly $functionName

        # The redeploy: new code, and a manifest.json that now records another script and alias. The
        # function keeps the handler and alias it has, with a warning for each, and runs the new code.
        $before = Get-LMFunctionConfiguration -FunctionName $functionName -Region $script:Region -ErrorAction Stop
        $before.Handler | Should -BeExactly 'handler.ps1'
        $before.RuntimeConfig.RuntimeArn | Should -BeExactly $runtime.RuntimeArn
        $before.RuntimeConfig.RuntimeAlias | Should -BeExactly 'default'
        $handlerPath = [System.IO.Path]::Combine($project, 'handler.ps1')
        $code = [System.IO.File]::ReadAllText($handlerPath)
        $newCode = $code.Replace('"Hello, $name"', '"Hello again, $name"')
        $newCode | Should -Not -BeExactly $code
        [System.IO.File]::WriteAllText($handlerPath, $newCode)
        [System.IO.File]::WriteAllText([System.IO.Path]::Combine($project, 'other.ps1'), "param(`$LambdaInput, `$LambdaContext)`n'other'`n")
        $manifestPath = [System.IO.Path]::Combine($project, 'manifest.json')
        $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json
        $manifest.HandlerString = 'other.ps1'
        $manifest.ScriptFile = 'other.ps1'
        $manifest.RuntimeAlias = 'staging'
        [System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json))

        $redeployment = New-LambdaPSFunctionDeployment -FunctionName $functionName -ProjectPath $project -RuntimeName $runtimeName `
            -Role $script:Role -Region $script:Region -PackagePath ([System.IO.Path]::Combine($script:Work, 'cmr-fn-redeploy.zip')) `
            -Confirm:$false -WarningVariable warnings -WarningAction SilentlyContinue

        $redeployment.Updated | Should -BeTrue
        $redeployment.FunctionArn | Should -MatchExactly $inRegion
        $messages = @($warnings | ForEach-Object { [string]$_ })
        @($messages | Where-Object { $_ -clike "manifest.json records ScriptFile 'other.ps1', but function '$functionName' runs handler 'handler.ps1'*" }).Count | Should -Be 1
        @($messages | Where-Object { $_ -clike "manifest.json records RuntimeAlias 'staging', but function '$functionName' is bound to alias 'default'*" }).Count | Should -Be 1
        $after = Get-LMFunctionConfiguration -FunctionName $functionName -Region $script:Region -ErrorAction Stop
        $after.Handler | Should -BeExactly $before.Handler
        $after.RuntimeConfig.RuntimeArn | Should -BeExactly $before.RuntimeConfig.RuntimeArn
        $after.RuntimeConfig.RuntimeArn | Should -MatchExactly $inRegion
        $after.RuntimeConfig.RuntimeAlias | Should -BeExactly $before.RuntimeConfig.RuntimeAlias

        $response = Invoke-LambdaPSFunction -FunctionName $functionName -Payload @{ name = 'live' } -Region $script:Region -ErrorAction Stop

        $response.message | Should -BeExactly 'Hello again, live'
        $response.function | Should -BeExactly $functionName
    }
}
