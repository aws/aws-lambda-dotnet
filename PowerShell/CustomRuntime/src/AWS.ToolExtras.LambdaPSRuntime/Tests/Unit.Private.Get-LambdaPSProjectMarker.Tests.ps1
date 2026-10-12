BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSProjectMarker' -Tag 'Unit' {
    It 'returns nothing when there is no marker' {
        InModuleScope $script:moduleName -Parameters @{ Dir = $TestDrive } {
            param($Dir)
            Get-LambdaPSProjectMarker -Path $Dir | Should -BeNullOrEmpty
        }
    }

    It 'reads the target and PowerShell version' {
        $dir = Join-Path $TestDrive 'marker-ok'
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value '{"Target":"Layer","PowerShellVersion":"7.4"}'
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            $marker = Get-LambdaPSProjectMarker -Path $Dir
            $marker.Target | Should -Be 'Layer'
            $marker.PowerShellVersion | Should -Be '7.4'
        }
    }

    It 'throws InvalidProjectMarker for <Case>' -ForEach @(
        @{ Case = 'invalid JSON'; Content = '{"Target":' }
        @{ Case = 'an unknown target'; Content = '{"Target":"layer2"}' }
    ) {
        $dir = Join-Path $TestDrive "marker-$($Case -replace ' ', '-')"
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value $Content
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            { Get-LambdaPSProjectMarker -Path $Dir } | Should -Throw -ErrorId 'InvalidProjectMarker'
        }
    }

    # Callers print the Target and return it (Build-LambdaPSRuntimePackage), so a hand-edited 'layer' is 'Layer'.
    It 'returns the canonical spelling of a target written in another case: <Written>' -ForEach @(
        @{ Written = 'layer'; Canonical = 'Layer' }, @{ Written = 'cmr'; Canonical = 'CMR' }
        @{ Written = 'SELFCONTAINEDZIP'; Canonical = 'SelfContainedZip' }, @{ Written = 'container'; Canonical = 'Container' }
    ) {
        $dir = Join-Path $TestDrive "marker-case-$Written"
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value "{`"Target`":`"$Written`",`"PowerShellVersion`":`"7.4`"}"
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir; Canonical = $Canonical } {
            param($Dir, $Canonical)
            (Get-LambdaPSProjectMarker -Path $Dir).Target | Should -BeExactly $Canonical
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Get-LambdaPSProjectMarker raised through a calling cmdlet' -Tag 'Unit' {
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'InvalidProjectMarker'; Case = 'invalid-json'; Content = '{"Target":' }
        @{ ErrorId = 'InvalidProjectMarker'; Case = 'unknown-target'; Content = '{"Target":"layer2"}' }
    ) {
        $dir = [System.IO.Path]::Combine($TestDrive, "caller-marker-$Case")
        $null = New-Item -ItemType Directory -Path $dir -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'lambdaps-runtime.json')) -Value $Content
        $Call = { Get-LambdaPSProjectMarker -Path $Dir }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString(); Dir = $dir } {
            param($ErrorId, $Call, $Dir)
            # Rebuilt here, so the call runs in the module and reaches its private helper.
            $Call = [scriptblock]::Create($Call)
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
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSProjectMarker' }) | Should -BeNullOrEmpty
        }
    }
}
