# Parameter-contract tests: assert that every AWS cmdlet parameter this module passes actually EXISTS
# on the real cmdlet.
#
# Why this file exists. The rest of the suite mocks the AWS cmdlets, and each test first declares a
# stub with the parameters it expects, which shadows the real cmdlet:
#
#     function Get-LMCRuntime { param($Name, $Region, $ClientConfig) }   # <-- WRONG, but self-consistent
#     Mock 'Get-LMCRuntime' ...
#     Should -Invoke 'Get-LMCRuntime' -ParameterFilter { $Name -eq 'my-ps' }
#
# That pattern keeps every test away from AWS, but it makes the mock validate ITSELF: the stub declares
# the contract, the call site matches the stub, and the assertion checks the stub. A call site can be
# wrong about the real AWS cmdlet and the whole suite still passes -- for example 'Get-LMCRuntime -Name'
# when the real cmdlet only accepts -RuntimeName, which fails with a ParameterBindingException on the
# first real call.
#
# These tests close that gap by checking the call sites against the REAL cmdlet metadata. They need an
# AWS.Tools.LambdaCore that exports the Lambda customer-managed runtime cmdlets, plus AWS.Tools.Lambda,
# .ECR and .SecurityToken. build.ps1 checks for them up front; to use modules that are not on PSModulePath:
#
#     $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = '/path/to/aws-tools-modules'   # dir containing AWS.Tools.*/<ver>/
#     pwsh ./build.ps1
#
# The module itself requires them, so the suite does not import without them.

BeforeDiscovery {
    # The contract table: cmdlet -> the parameters this module actually passes to it, including keys
    # passed in a splatted hashtable. The 'AWS call sites' tests below fail when a call site in Public/
    # or Private/ names a cmdlet, or a parameter (literal or a splatted hashtable key), not listed here.
    $script:AwsCmdletContract = @(
        # --- Lambda CMR control plane (Private/Publish-LambdaPSRuntimeVersion.ps1) ---
        @{ Cmdlet = 'New-LMCRuntime'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('Name', 'Region', 'ClientConfig') }
        # -Name is the version label; the runtime is -RuntimeName.
        @{ Cmdlet = 'New-LMCRuntimeVersion'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Name', 'ImageUri', 'Compatibility_Architecture', 'ReleaseNote', 'Region', 'ClientConfig') }
        # Also the version preflight (Public/New-LambdaPSRuntimeDeployment.ps1).
        @{ Cmdlet = 'Get-LMCRuntimeVersion'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Name', 'Region', 'ClientConfig') }
        # Naming the next default version (Public/New-LambdaPSRuntimeDeployment.ps1).
        @{ Cmdlet = 'Get-LMCRuntimeVersionList'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Region', 'ClientConfig') }

        # --- Runtime alias (Private/Set-LambdaPSRuntimeAlias.ps1) ---
        @{ Cmdlet = 'New-LMCRuntimeAlias'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Name', 'RuntimeVersion', 'Region', 'ClientConfig') }
        @{ Cmdlet = 'Update-LMCRuntimeAlias'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Name', 'RuntimeVersion', 'Region', 'ClientConfig') }

        # --- Runtime alias architecture read (Private/Get-LambdaPSRuntimeAliasArchitecture.ps1) ---
        @{ Cmdlet = 'Get-LMCRuntimeAlias'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Name', 'Region', 'ClientConfig') }

        # --- Runtime ARN resolution (Private/Get-LambdaPSRuntimeArn.ps1) ---
        # -RuntimeName, NOT -Name. New-LMCRuntime is the odd one out in the family.
        @{ Cmdlet = 'Get-LMCRuntime'; Module = 'AWS.Tools.LambdaCore'
           Parameters = @('RuntimeName', 'Region', 'ClientConfig') }

        # --- Function create / update (Private/Publish-LambdaPSFunction.ps1) ---
        # -Code_ZipFile / -ZipFile (byte[]), NOT the [Obsolete] path-taking -ZipFilename.
        @{ Cmdlet = 'Publish-LMFunction'; Module = 'AWS.Tools.Lambda'
           Parameters = @('FunctionName', 'Handler', 'Runtime', 'Role', 'Architecture', 'Code_ZipFile',
                          'Timeout', 'MemorySize', 'Environment_Variable', 'Environment_IsVariablesSet',
                          'RuntimeConfig_RuntimeArn', 'RuntimeConfig_RuntimeAlias', 'Region', 'ClientConfig')
           Types = @{ Code_ZipFile = 'System.Byte[]' } }
        @{ Cmdlet = 'Update-LMFunctionCode'; Module = 'AWS.Tools.Lambda'
           Parameters = @('FunctionName', 'ZipFile', 'Architecture', 'Region', 'ClientConfig')
           Types = @{ ZipFile = 'System.Byte[]' } }
        @{ Cmdlet = 'Get-LMFunctionConfiguration'; Module = 'AWS.Tools.Lambda'
           Parameters = @('FunctionName', 'Region', 'ClientConfig') }

        # --- Invoke (Public/Invoke-LambdaPSFunction.ps1) ---
        # -Payload is sent as the JSON text the cmdlet serialized.
        @{ Cmdlet = 'Invoke-LMFunction'; Module = 'AWS.Tools.Lambda'
           Parameters = @('FunctionName', 'Payload', 'LogType', 'Qualifier', 'Region', 'ClientConfig')
           Types = @{ Payload = 'System.String' } }

        # --- Configuration update of an existing function (Private/Publish-LambdaPSFunction.ps1) ---
        @{ Cmdlet = 'Update-LMFunctionConfiguration'; Module = 'AWS.Tools.Lambda'
           Parameters = @('FunctionName', 'Handler', 'Role', 'Timeout', 'MemorySize', 'RuntimeConfig_RuntimeArn',
                          'RuntimeConfig_RuntimeAlias', 'Environment_Variable', 'Environment_IsVariablesSet', 'Region', 'ClientConfig') }

        # --- ECR (Private/Initialize-LambdaPSEcrRepository.ps1) ---
        @{ Cmdlet = 'Get-ECRRepository'; Module = 'AWS.Tools.ECR'
           Parameters = @('RepositoryName', 'Region', 'ClientConfig') }
        @{ Cmdlet = 'New-ECRRepository'; Module = 'AWS.Tools.ECR'
           Parameters = @('RepositoryName', 'ImageScanningConfiguration_ScanOnPush', 'ImageTagMutability', 'Region', 'ClientConfig') }
        @{ Cmdlet = 'Get-ECRRepositoryPolicy'; Module = 'AWS.Tools.ECR'
           Parameters = @('RepositoryName', 'Region', 'ClientConfig') }
        @{ Cmdlet = 'Set-ECRRepositoryPolicy'; Module = 'AWS.Tools.ECR'
           Parameters = @('RepositoryName', 'PolicyText', 'Region', 'ClientConfig') }
        @{ Cmdlet = 'Get-ECRLoginCommand'; Module = 'AWS.Tools.ECR'
           Parameters = @('Region', 'ClientConfig') }

        # --- ECR image tag preflight (Public/New-LambdaPSRuntimeDeployment.ps1) ---
        @{ Cmdlet = 'Get-ECRImageMetadata'; Module = 'AWS.Tools.ECR'
           Parameters = @('RepositoryName', 'ImageId', 'Region', 'ClientConfig') }
        # --- ECR token for the no-Docker push (Private/Get-LambdaPSEcrAuthorizationToken.ps1) ---
        @{ Cmdlet = 'Get-ECRAuthorizationToken'; Module = 'AWS.Tools.ECR'
           Parameters = @('Region', 'ClientConfig') }

        # --- STS (Public/New-LambdaPSRuntimeDeployment.ps1) ---
        @{ Cmdlet = 'Get-STSCallerIdentity'; Module = 'AWS.Tools.SecurityToken'
           Parameters = @('Region', 'ClientConfig') }
    )

    # Try to make the real AWS.Tools modules importable. An explicit path wins; otherwise rely on
    # whatever is already on PSModulePath.
    $script:ContractModulePath = $env:LAMBDAPS_AWSTOOLS_MODULE_PATH
    if (-not [string]::IsNullOrWhiteSpace($script:ContractModulePath) -and
        (Test-Path -LiteralPath $script:ContractModulePath)) {
        $resolved = (Resolve-Path -LiteralPath $script:ContractModulePath).Path
        # Moved to the front even when it is already on PSModulePath, as build.ps1 does: a pwsh started
        # from another pwsh lists its own module folders before the inherited entries.
        $otherPaths = @($env:PSModulePath -split [System.IO.Path]::PathSeparator) -ne $resolved
        $env:PSModulePath = (@($resolved) + $otherPaths) -join [System.IO.Path]::PathSeparator
    }

    # A build is usable for this contract only if it carries the generated CMR cmdlets. An
    # AWS.Tools.LambdaCore without them does not, and skipping is more honest than passing vacuously.
    $script:ContractAvailable = $false
    $script:ContractSkipReason = ''
    $script:ContractLoadError = $null
    try {
        Import-Module AWS.Tools.Common -ErrorAction Stop
        Import-Module AWS.Tools.Lambda, AWS.Tools.LambdaCore, AWS.Tools.ECR, AWS.Tools.SecurityToken -ErrorAction Stop
        if (Get-Command 'New-LMCRuntime' -ErrorAction SilentlyContinue) {
            $script:ContractAvailable = $true
        }
        else {
            $lambdaCoreVersion = (Get-Module AWS.Tools.LambdaCore | Select-Object -First 1).Version
            $script:ContractSkipReason = "AWS.Tools.LambdaCore $lambdaCoreVersion is installed but does not export the " +
                'generated Lambda CMR cmdlets (New-LMCRuntime). Set $env:LAMBDAPS_AWSTOOLS_MODULE_PATH to a ' +
                'build that includes them to verify the parameter contract.'
        }
    }
    catch {
        # A FileLoadException means another AWS.Tools build's AWSSDK.Core is already loaded in this
        # process. The build is there but cannot be checked, so that fails the contract instead of skipping it.
        for ($exception = $_.Exception; $null -ne $exception; $exception = $exception.InnerException) {
            if ($exception -is [System.IO.FileLoadException]) { $script:ContractLoadError = $_.Exception.Message; break }
        }
        $script:ContractSkipReason = 'AWS.Tools.Lambda/.LambdaCore/.ECR/.SecurityToken could not be imported: ' +
            "$($_.Exception.Message). Set `$env:LAMBDAPS_AWSTOOLS_MODULE_PATH to a build that includes the " +
            'generated Lambda CMR cmdlets to verify the parameter contract.'
    }

    if (-not $script:ContractAvailable) {
        Write-Warning "Parameter-contract tests SKIPPED: $script:ContractSkipReason"
    }
}

Describe 'AWS cmdlet parameter contract' -Tag 'Contract' {
    # The import runs in BeforeDiscovery, so its outcome reaches the test as data.
    It 'imports the AWS.Tools build without an assembly conflict' -ForEach @(@{ LoadError = $script:ContractLoadError }) {
        $LoadError | Should -BeNullOrEmpty -Because (
            'another AWS.Tools build was loaded first in this process, so the contract cannot be checked. ' +
            'Run the suite with build.ps1, which puts LAMBDAPS_AWSTOOLS_MODULE_PATH first on PSModulePath')
    }

    Context 'against the real AWS.Tools cmdlets' -Skip:(-not $script:ContractAvailable) {
        It '<Cmdlet> exists' -ForEach $script:AwsCmdletContract {
            Get-Command $Cmdlet -ErrorAction SilentlyContinue |
                Should -Not -BeNullOrEmpty -Because "$Module must export $Cmdlet; this module calls it"
        }

        It '<Cmdlet> accepts every parameter this module passes' -ForEach $script:AwsCmdletContract {
            $command = Get-Command $Cmdlet -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty

            $actual = $command.Parameters.Keys
            $missing = @($Parameters | Where-Object { $actual -notcontains $_ })

            $missing -join ', ' | Should -BeNullOrEmpty -Because (
                "$Cmdlet is called with these parameters by this module, but the real cmdlet does not " +
                'declare them. Either the call site is wrong or the cmdlet changed shape. Note that ' +
                'a mocked test CANNOT catch this: the stub declares whatever the call site uses.')
        }

        # A parameter can exist and take another type than the value this module passes: the package
        # bytes and the JSON payload.
        It '<Cmdlet> takes the parameter types this module passes' -ForEach @($script:AwsCmdletContract | Where-Object { $_.Types }) {
            $command = Get-Command $Cmdlet -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty

            foreach ($name in $Types.Keys) {
                $command.Parameters[$name].ParameterType.FullName | Should -Be $Types[$name] -Because "this module passes $Cmdlet -$name a $($Types[$name])"
            }
        }

        # Every AWS call site splats the caller's -ProfileName / -ProfileLocation / -Credential (see the
        # call-site checks below), so every cmdlet the module calls must accept them.
        It '<Cmdlet> accepts the credential parameters this module forwards' -ForEach $script:AwsCmdletContract {
            $command = Get-Command $Cmdlet -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty

            $missing = @('ProfileName', 'ProfileLocation', 'Credential') | Where-Object { -not $command.Parameters.ContainsKey($_) }
            $missing -join ', ' | Should -BeNullOrEmpty
        }

        # A parameter can exist and still be the wrong one to use. AWS.Tools marks superseded generated
        # parameters with [Obsolete], which emits a warning on every call, so the module calls none of
        # them. Mocked tests cannot
        # see this either: a stub has no attributes.
        It '<Cmdlet> is called with no [Obsolete] parameter' -ForEach $script:AwsCmdletContract {
            $command = Get-Command $Cmdlet -ErrorAction SilentlyContinue
            $command | Should -Not -BeNullOrEmpty

            $obsolete = @(
                $Parameters |
                    Where-Object { $command.Parameters.ContainsKey($_) } |
                    Where-Object {
                        $command.Parameters[$_].Attributes |
                            Where-Object { $_ -is [System.ObsoleteAttribute] }
                    }
            )

            $obsolete -join ', ' | Should -BeNullOrEmpty -Because (
                "these parameters of $Cmdlet are marked [Obsolete] in $Module, so calling them warns on " +
                'every invocation. Switch the call site to the replacement the obsolete message names.')
        }
    }
}

# The download seams (Private/Save-LambdaPSModuleFromRepository.ps1, Private/Save-LambdaPSAWSToolsModule.ps1) are
# mocked everywhere else, for the same reason and with the same gap. PSResourceGet ships with
# PowerShell 7.4, so its contract is always checked; AWS.Tools.Installer is checked when 2.x is
# installed and skipped otherwise.
BeforeDiscovery {
    $script:AcquisitionContract = @(
        @{ Cmdlet = 'Save-PSResource'; Module = 'Microsoft.PowerShell.PSResourceGet'; MinimumVersion = '1.0'
           Parameters = @('Name', 'Version', 'Path', 'SkipDependencyCheck', 'IncludeXml', 'PassThru', 'Confirm', 'Repository', 'AuthenticodeCheck') }
        @{ Cmdlet = 'Install-AWSToolsModule'; Module = 'AWS.Tools.Installer'; MinimumVersion = '2.0'
           Parameters = @('Name', 'Version', 'Path', 'Force', 'Confirm') }
    )
    foreach ($entry in $script:AcquisitionContract) {
        $entry.Available = $null -ne (Get-Module -ListAvailable -Name $entry.Module |
                Where-Object { $_.Version -ge [version]$entry.MinimumVersion -and $_.Version.Major -eq ([version]$entry.MinimumVersion).Major } |
                Select-Object -First 1)
    }
}

Describe 'Module download parameter contract' -Tag 'Contract' {
    It '<Cmdlet> (<Module> <MinimumVersion>.x) accepts every parameter this module passes' -ForEach $script:AcquisitionContract {
        if (-not $Available) {
            Set-ItResult -Skipped -Because "$Module $MinimumVersion.x is not installed"
            return
        }
        $installed = Get-Module -ListAvailable -Name $Module |
            Where-Object { $_.Version -ge [version]$MinimumVersion -and $_.Version.Major -eq ([version]$MinimumVersion).Major } |
            Sort-Object Version -Descending | Select-Object -First 1
        $imported = Import-Module $installed.Path -PassThru -Force | Where-Object Name -eq $Module
        $command = $imported.ExportedCommands[$Cmdlet]
        $command | Should -Not -BeNullOrEmpty -Because "$Module must export $Cmdlet"

        $missing = @($Parameters | Where-Object { $command.Parameters.Keys -notcontains $_ })
        $missing -join ', ' | Should -BeNullOrEmpty
    }
}

# The AWS.Tools cmdlets and SDK types the module finds by NAME rather than by a typed reference, so a
# rename would make it fall back silently instead of failing.
Describe 'Named SDK surface' -Tag 'Contract' {
    Context 'against the real AWS.Tools modules' -Skip:(-not $script:ContractAvailable) {
        It 'AWS.Tools.Common exports Get-DefaultAWSRegion, which the region fallback reads' {
            Get-Command -Name 'Get-DefaultAWSRegion' -Module 'AWS.Tools.Common' -ErrorAction SilentlyContinue |
                Should -Not -BeNullOrEmpty
        }

        It '<Type> exists, with <Member>' -ForEach @(
            @{ Type = 'Amazon.Runtime.FallbackRegionFactory'; Member = 'GetRegionEndpoint' }
            @{ Type = 'Amazon.Runtime.CredentialManagement.CredentialProfileStoreChain'; Member = 'TryGetProfile' }
            @{ Type = 'Amazon.RegionEndpoint'; Member = 'GetBySystemName' }
            @{ Type = 'Amazon.PowerShell.Common.StandaloneRegionArguments'; Member = 'ProfileLocation' }
            @{ Type = 'Amazon.PowerShell.Common.IAWSRegionArgumentsMethods'; Member = 'TryGetRegion' }
            @{ Type = 'Amazon.PowerShell.Common.RegionSource'; Member = 'Unknown' }
        ) {
            $resolved = $Type -as [type]
            $resolved | Should -Not -BeNullOrEmpty -Because (
                'Resolve-LambdaPSRegion, Get-LambdaPSAWSToolsRegion, Get-LambdaPSSdkFallbackRegion and ' +
                'Get-LambdaPSPartition find it by name, and a missing type falls through to the next region source')
            @($resolved.GetMember($Member)).Count | Should -BeGreaterThan 0
        }

        # Get-LambdaPSAWSToolsRegion calls this overload with two [ref] outputs. A changed signature
        # would throw MethodException on every deploy run without -Region instead of falling through.
        It 'IAWSRegionArgumentsMethods.TryGetRegion has the 5-parameter overload Get-LambdaPSAWSToolsRegion calls' {
            $expected = 'Amazon.PowerShell.Common.IAWSRegionArguments', 'System.Boolean', 'Amazon.RegionEndpoint&',
                'Amazon.PowerShell.Common.RegionSource&', 'System.Management.Automation.SessionState'
            $overloads = @(([type]'Amazon.PowerShell.Common.IAWSRegionArgumentsMethods').GetMethods() | Where-Object Name -eq 'TryGetRegion' |
                    ForEach-Object { ($_.GetParameters() | ForEach-Object { $_.ParameterType.FullName }) -join ',' })
            $overloads | Should -Contain ($expected -join ',')
        }

        It 'a RegionEndpoint carries the partition name and DNS suffix the ECR host and ARNs are built from' {
            $endpoint = [Amazon.RegionEndpoint]::GetBySystemName('us-east-1')
            $endpoint.PartitionName | Should -Be 'aws'
            $endpoint.PartitionDnsSuffix | Should -Be 'amazonaws.com'
        }
    }
}

# The call sites themselves, read from the source with the PowerShell parser. These need no AWS.Tools
# build, so they always run.
BeforeDiscovery {
    $moduleRoot = Split-Path -Parent $PSScriptRoot
    $script:AwsCallSites = @(
        foreach ($file in Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($moduleRoot, 'Public')), ([System.IO.Path]::Combine($moduleRoot, 'Private')) -Filter '*.ps1') {
            $ast = [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$null, [ref]$null)
            $ast.FindAll({
                    $args[0] -is [System.Management.Automation.Language.CommandAst] -and
                    $args[0].GetCommandName() -match '^[A-Za-z]+-(LM|ECR|STS)[A-Za-z]+$'
                }, $true) | ForEach-Object {
                $call = $_
                $name = $call.GetCommandName()
                $contract = $script:AwsCmdletContract | Where-Object { $_.Cmdlet -eq $name } | Select-Object -First 1
                $splats = @($call.CommandElements | Where-Object {
                        $_ -is [System.Management.Automation.Language.VariableExpressionAst] -and $_.Splatted
                    } | ForEach-Object { $_.VariablePath.UserPath })
                # The keys of each splatted hashtable, read from its assignments in the same function:
                # '$x = @{ Key = ... }', '$x.Key = ...', '$x[''Key''] = ...' and '$x.Add(''Key'', ...)'.
                $function = $call.Parent
                while ($null -ne $function -and $function -isnot [System.Management.Automation.Language.FunctionDefinitionAst]) { $function = $function.Parent }
                $splatKeys = foreach ($splat in $splats | Where-Object { $_ -ne 'CredentialParameter' -and $_ -ne 'credentialParameter' }) {
                    foreach ($assignment in $function.FindAll({ $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst] }, $true)) {
                        $left = $assignment.Left
                        if ($left -is [System.Management.Automation.Language.VariableExpressionAst] -and $left.VariablePath.UserPath -eq $splat) {
                            $table = $assignment.Right.Find({ $args[0] -is [System.Management.Automation.Language.HashtableAst] }, $true)
                            if ($table) { $table.KeyValuePairs | ForEach-Object { $_.Item1.Extent.Text.Trim("'", '"') } }
                        }
                        elseif ($left -is [System.Management.Automation.Language.MemberExpressionAst] -and
                            $left.Expression -is [System.Management.Automation.Language.VariableExpressionAst] -and
                            $left.Expression.VariablePath.UserPath -eq $splat) {
                            $left.Member.Extent.Text.Trim("'", '"')
                        }
                        elseif ($left -is [System.Management.Automation.Language.IndexExpressionAst] -and
                            $left.Target -is [System.Management.Automation.Language.VariableExpressionAst] -and
                            $left.Target.VariablePath.UserPath -eq $splat) {
                            $left.Index.Extent.Text.Trim("'", '"')
                        }
                    }
                    foreach ($add in $function.FindAll({
                                $args[0] -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and
                                $args[0].Member.Extent.Text -eq 'Add' -and
                                $args[0].Expression -is [System.Management.Automation.Language.VariableExpressionAst] -and
                                $args[0].Arguments.Count -ge 1
                            }, $true)) {
                        if ($add.Expression.VariablePath.UserPath -eq $splat) { $add.Arguments[0].Extent.Text.Trim("'", '"') }
                    }
                }
                @{
                    Cmdlet     = $name
                    Location   = "$($file.Name):$($_.Extent.StartLineNumber)"
                    Splats     = $splats
                    # Parameters named at the call site or in a splatted hashtable, less the common ones.
                    Named      = @(@($call.CommandElements | Where-Object {
                                $_ -is [System.Management.Automation.Language.CommandParameterAst]
                            } | ForEach-Object { $_.ParameterName }) + @($splatKeys) |
                            Where-Object { [System.Management.Automation.Cmdlet]::CommonParameters -notcontains $_ } |
                            Select-Object -Unique)
                    InContract = $null -ne $contract
                    Contracted = if ($contract) { @($contract.Parameters) } else { @() }
                }
            }
        }
    )
}

Describe 'AWS call sites' -Tag 'Contract' {
    # Discovery-time data reaches the run phase only through -ForEach.
    It 'finds the AWS call sites to check' -ForEach @(@{ Count = $script:AwsCallSites.Count }) {
        $Count | Should -BeGreaterThan 20
    }

    It 'reads the keys of a splatted hashtable as parameters' -ForEach @(@{ Sites = $script:AwsCallSites }) {
        $publish = $Sites | Where-Object { $_.Cmdlet -eq 'Publish-LMFunction' } | Select-Object -First 1
        $publish.Named | Should -Contain 'RuntimeConfig_RuntimeArn'
        $publish.Named | Should -Contain 'Code_ZipFile'
    }

    It '<Cmdlet> at <Location> is in the contract table' -ForEach $script:AwsCallSites {
        $InContract | Should -BeTrue -Because "add $Cmdlet and the parameters this call passes to the contract table"
    }

    It '<Cmdlet> at <Location> names only parameters listed in the contract table' -ForEach $script:AwsCallSites {
        $unlisted = @($Named | Where-Object { $Contracted -notcontains $_ })
        $unlisted -join ', ' | Should -BeNullOrEmpty -Because 'the table is what is checked against the real cmdlet'
    }

    It '<Cmdlet> at <Location> passes the caller''s credential parameters' -ForEach $script:AwsCallSites {
        $Splats -contains 'CredentialParameter' | Should -BeTrue -Because (
            'every AWS call must splat @CredentialParameter (in a public cmdlet, @credentialParameter), ' +
            'or a caller''s -ProfileName / -Credential is silently ignored for that call')
    }
}

# The integration suite (Tests/Integration.EndToEnd.Tests.ps1) only runs with -Task Integration, so its setup is checked
# here from source: where it deploys comes from the environment, never from the file.
Describe 'Integration suite configuration' -Tag 'Contract' {
    BeforeAll {
        $script:integrationPath = [System.IO.Path]::Combine($PSScriptRoot, 'Integration.EndToEnd.Tests.ps1')
        $script:integrationAst = [System.Management.Automation.Language.Parser]::ParseFile($script:integrationPath, [ref]$null, [ref]$null)
        $script:integrationText = Get-Content -LiteralPath $script:integrationPath -Raw
    }

    It 'names no account, region or role of its own' {
        $script:integrationText | Should -Not -Match '\b[0-9]{12}\b'
        $script:integrationText | Should -Not -Match '\b(us|eu|ap|sa|ca|me|af|il|mx|cn)-[a-z]+-[0-9]\b'
        $script:integrationText | Should -Not -Match 'arn:aws[a-z-]*:iam::'
    }

    It 'reads <Name> from the environment' -ForEach @(
        @{ Name = 'LAMBDAPS_INTEGRATION_REGION' }, @{ Name = 'LAMBDAPS_INTEGRATION_ACCOUNT' }, @{ Name = 'LAMBDAPS_INTEGRATION_ROLE_ARN' }
    ) {
        $uses = $script:integrationAst.FindAll({
                $args[0] -is [System.Management.Automation.Language.VariableExpressionAst] -and
                $args[0].VariablePath.UserPath -eq "env:$Name"
            }, $true)
        @($uses).Count | Should -BeGreaterThan 0
    }

    # The removal pass runs offline here, defined from the suite's own source, with resources whose
    # Remove blocks write output the way AWS.Tools cmdlets do.
    It 'returns only the resources it could not remove, whatever the Remove blocks write' {
        $definitions = @('Test-IntegrationNotFound', 'Remove-IntegrationResourceSet' | ForEach-Object {
                $name = $_
                $function = $script:integrationAst.Find({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $args[0].Name -eq $name }, $true)
                $function | Should -Not -BeNullOrEmpty -Because "the integration suite defines $name"
                $function.Extent.Text
            })
        . ([scriptblock]::Create($definitions -join [Environment]::NewLine))
        $resources = @(
            [PSCustomObject]@{ Kind = 'function'; Name = 'run-a'; Remove = { 'output a removal writes'; [PSCustomObject]@{ HttpStatusCode = 204 } } }
            [PSCustomObject]@{ Kind = 'function'; Name = 'run-b'; Remove = { throw [System.InvalidOperationException]::new('still in use') } }
            $null
            [PSCustomObject]@{ Kind = 'bucket'; Name = 'other-c'; Remove = { 'never called' } }
        )

        $failed = @(Remove-IntegrationResourceSet -Resource $resources -Prefix 'run-')

        $failed.Count | Should -Be 2
        $failed.Resource.Name | Should -Be @('run-b', 'other-c')
        $failed[0].Message | Should -Be 'still in use'
    }

    It 'removes the log group of each function it creates' {
        $removals = $script:integrationAst.FindAll({
                $args[0] -is [System.Management.Automation.Language.CommandAst] -and $args[0].GetCommandName() -eq 'Remove-CWLLogGroup'
            }, $true)
        @($removals).Count | Should -BeGreaterThan 0
        $script:integrationText | Should -Match ([regex]::Escape('"/aws/lambda/$Name"'))
    }
}
