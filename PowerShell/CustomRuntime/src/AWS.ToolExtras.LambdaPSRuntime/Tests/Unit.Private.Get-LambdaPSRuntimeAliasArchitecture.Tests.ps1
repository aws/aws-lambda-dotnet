BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSRuntimeAliasArchitecture' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSClientConfig' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ ClientAppId = 'AWS.ToolExtras.LambdaPSRuntime#test' }
        }
    }

    It 'reads the alias with -RuntimeName/-Name and returns its reported architectures' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith {
                [PSCustomObject]@{ Name = 'default'; Compatibility = [PSCustomObject]@{ Architectures = @('arm64') } }
            }

            [string[]]$result = Get-LambdaPSRuntimeAliasArchitecture -RuntimeName 'my-ps' -RuntimeAlias 'default' -Region 'us-east-1'

            $result | Should -Be @('arm64')
            Should -Invoke 'Get-LMCRuntimeAlias' -Times 1 -Exactly -ParameterFilter {
                $RuntimeName -eq 'my-ps' -and $Name -eq 'default' -and $Region -eq 'us-east-1' -and
                $ClientConfig.ClientAppId -eq 'AWS.ToolExtras.LambdaPSRuntime#test'
            }
        }
    }

    It 'returns an empty array (not $null) when the service reports no architecture' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith { [PSCustomObject]@{ Name = 'default'; Compatibility = $null } }

            $result = Get-LambdaPSRuntimeAliasArchitecture -RuntimeName 'my-ps' -RuntimeAlias 'default' -Region 'us-east-1'

            , $result | Should -BeOfType ([string[]])
            $result.Count | Should -Be 0
        }
    }

    It 'throws RuntimeAliasNotFound for a missing alias' {
        InModuleScope $script:moduleName {
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            Mock 'Get-LMCRuntimeAlias' -MockWith {
                throw [LambdaPSTests.Fakes.ResourceNotFoundException]::new('Alias not found')
            }

            { Get-LambdaPSRuntimeAliasArchitecture -RuntimeName 'my-ps' -RuntimeAlias 'nope' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'RuntimeAliasNotFound'
        }
    }

    It 'throws RuntimeAliasCheckFailed, wrapping any other error whose text <Case>, instead of masking it as not-found' -ForEach @(
        @{ Case = 'has no full stop'; Text = 'not authorized to perform lambda:GetRuntimeAlias'; Detail = ': not authorized to perform lambda:GetRuntimeAlias.' }
        @{ Case = 'ends with one'; Text = 'Rate exceeded.'; Detail = ': Rate exceeded.' }
        @{ Case = 'ends with a quoted one'; Text = " Denied by 'policy.' "; Detail = ": Denied by 'policy.'" }
        @{ Case = 'is blank'; Text = '  '; Detail = '.' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Text = $Text; Detail = $Detail } {
            param($Text, $Detail)
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            $script:aliasErrorText = $Text
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw [LambdaPSTests.Fakes.AccessDeniedException]::new($script:aliasErrorText) }

            $err = { Get-LambdaPSRuntimeAliasArchitecture -RuntimeName 'my-ps' -RuntimeAlias 'default' -Region 'us-east-1' } |
                Should -Throw -ErrorId 'RuntimeAliasCheckFailed' -PassThru
            $err.Exception.Message | Should -BeExactly ("Could not read alias 'default' of runtime 'my-ps' in region 'us-east-1' to check " +
                "its architecture$Detail Nothing was packaged or created.")
            $err.Exception.InnerException | Should -BeOfType ([LambdaPSTests.Fakes.AccessDeniedException])
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'RuntimeAliasNotFound'; Fault = 'ResourceNotFoundException' }
        @{ ErrorId = 'RuntimeAliasCheckFailed'; Fault = 'AccessDeniedException' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Fault = $Fault } {
            param($ErrorId, $Fault)
            function Get-LMCRuntimeAlias { param($RuntimeName, $Name, $Region, $ClientConfig, $ErrorAction) }
            $script:aliasFault = $Fault
            Mock 'Get-LMCRuntimeAlias' -MockWith { throw (New-Object "LambdaPSTests.Fakes.$script:aliasFault" 'refused') }
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call { Get-LambdaPSRuntimeAliasArchitecture -RuntimeName 'my-ps' -RuntimeAlias 'default' -Region 'us-east-1' } -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSRuntimeAliasArchitecture' }) | Should -BeNullOrEmpty
        }
    }
}
