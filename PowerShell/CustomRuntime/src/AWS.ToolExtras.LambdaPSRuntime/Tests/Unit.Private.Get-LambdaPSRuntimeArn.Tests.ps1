BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSRuntimeArn' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    # The stub signature below MUST mirror the real cmdlet: Get-LMCRuntime identifies the runtime with
    # -RuntimeName, not -Name (New-LMCRuntime is the odd one out in taking -Name). A stub that declares
    # the wrong parameter makes the mock validate itself instead of the AWS contract, so a wrong
    # '-Name' call site would pass here.
    # Contract.AwsCmdlet.Tests.ps1 guards the signatures whenever the real module is present.
    It 'returns the runtime ARN from Get-LMCRuntime -RuntimeName' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith {
                [PSCustomObject]@{ Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' }
            }

            Get-LambdaPSRuntimeArn -RuntimeName 'my-ps' -Region 'us-east-1' |
                Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
            Should -Invoke 'Get-LMCRuntime' -Times 1 -Exactly -ParameterFilter { $RuntimeName -eq 'my-ps' -and $Region -eq 'us-east-1' }
        }
    }

    It 'throws RuntimeArnNotFound when the runtime has no ARN' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { [PSCustomObject]@{ Arn = $null } }

            { Get-LambdaPSRuntimeArn -RuntimeName 'my-ps' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'RuntimeArnNotFound'
        }
    }

    It 'maps a not-found runtime to RuntimeArnNotFound instead of the raw service error' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Runtime does not exist') }

            { Get-LambdaPSRuntimeArn -RuntimeName 'nope' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'RuntimeArnNotFound'
        }
    }

    It 'maps AccessDeniedException to RuntimeServiceAccessDenied, naming the region, as the runtime deploy does' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntime' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new('Unable to determine service/operation name to be authorized') }

            $err = $null
            try { Get-LambdaPSRuntimeArn -RuntimeName 'my-ps' -Region 'us-west-2' } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'RuntimeServiceAccessDenied'
            $err.Exception.Message | Should -BeLike '*may not be available in us-west-2*'
        }
    }

    It 'rejects a whitespace runtime name at parameter binding' {
        InModuleScope $script:moduleName {
            { Get-LambdaPSRuntimeArn -RuntimeName '   ' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Get-LambdaPSRuntimeArn'
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'RuntimeArnNotFound'; Fault = 'ResourceNotFoundException' }
        @{ ErrorId = 'RuntimeServiceAccessDenied'; Fault = 'AccessDeniedException' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Fault = $Fault } {
            param($ErrorId, $Fault)
            function Get-LMCRuntime { param($RuntimeName, $Region, $ClientConfig, $ErrorAction) }
            $script:runtimeFault = $Fault
            Mock 'Get-LMCRuntime' -MockWith { throw (New-Object "LambdaPSTests.Fakes.$script:runtimeFault" 'refused') }
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call { Get-LambdaPSRuntimeArn -RuntimeName 'my-ps' -Region 'us-east-1' } -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSRuntimeArn' }) | Should -BeNullOrEmpty
        }
    }
}
