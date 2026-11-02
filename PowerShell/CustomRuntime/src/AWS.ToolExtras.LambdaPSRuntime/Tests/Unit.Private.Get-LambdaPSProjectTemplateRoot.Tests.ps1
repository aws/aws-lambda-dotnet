BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSProjectTemplateRoot' -Tag 'Unit' {
    It 'returns the trees for <Target> in merge order' -ForEach @(
        @{ Target = 'CMR'; Trees = @('Shared', 'RuntimeImage') }
        @{ Target = 'Layer'; Trees = @('Shared', 'LayerRuntime') }
        @{ Target = 'SelfContainedZip'; Trees = @('Shared', 'SelfContainedZip') }
        @{ Target = 'Container'; Trees = @('Shared', 'RuntimeImage', 'ContainerFunction') }
        @{ Target = 'RuntimeBound'; Trees = @('Shared', 'FunctionZip') }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Trees = $Trees } {
            param($Target, $Trees)
            @(Get-LambdaPSProjectTemplateRoot -Target $Target | ForEach-Object { Split-Path -Leaf $_ }) | Should -Be $Trees
        }
    }

    It 'throws TemplateNotFound, naming the missing tree, when the install is incomplete' {
        InModuleScope $script:moduleName {
            Mock 'Test-Path' -MockWith { $LiteralPath -notlike '*ContainerFunction' }
            { Get-LambdaPSProjectTemplateRoot -Target Container } |
                Should -Throw -ErrorId 'TemplateNotFound' -ExpectedMessage '*ContainerFunction*'
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Get-LambdaPSProjectTemplateRoot raised through a calling cmdlet' -Tag 'Unit' {
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'TemplateNotFound' }
    ) {
        $Call = { Get-LambdaPSProjectTemplateRoot -Target Container }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString() } {
            param($ErrorId, $Call)
            # Rebuilt here, so the call runs in the module and reaches its private helper.
            $Call = [scriptblock]::Create($Call)
            Mock 'Test-Path' -MockWith { $LiteralPath -notlike '*ContainerFunction' }
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call $Call -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSProjectTemplateRoot' }) | Should -BeNullOrEmpty
        }
    }
}
