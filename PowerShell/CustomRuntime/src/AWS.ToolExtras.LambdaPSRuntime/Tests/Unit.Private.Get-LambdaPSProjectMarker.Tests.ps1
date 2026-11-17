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

    # An edited version is reported against the marker, not as a parameter error further on.
    It 'throws InvalidProjectMarker naming the file and field for <Field> <Value>' -ForEach @(
        @{ Field = 'PwshVersion'; Value = '7.4.x/../../evil' }, @{ Field = 'PwshVersion'; Value = '7.4' }
        @{ Field = 'PowerShellVersion'; Value = '7' }, @{ Field = 'PowerShellVersion'; Value = '7.4/..' }
    ) {
        $dir = Join-Path $TestDrive "marker-bad-$Field-$($Value -replace '\W', '-')"
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value (@{ Target = 'Layer'; $Field = $Value } | ConvertTo-Json)
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir; Field = $Field; Value = $Value } {
            param($Dir, $Field, $Value)
            { Get-LambdaPSProjectMarker -Path $Dir } | Should -Throw -ErrorId 'InvalidProjectMarker' -ExpectedMessage "*lambdaps-runtime.json' has $Field '$([System.Management.Automation.WildcardPattern]::Escape($Value))'*"
        }
    }

    It 'throws InvalidProjectMarker for a PwshVersion on another line than its PowerShellVersion' {
        # Arrange
        $dir = Join-Path $TestDrive 'marker-two-lines'
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value (@{ Target = 'Layer'; PowerShellVersion = '7.4'; PwshVersion = '7.40.1' } | ConvertTo-Json)
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            # Act / Assert
            { Get-LambdaPSProjectMarker -Path $Dir } | Should -Throw -ErrorId 'InvalidProjectMarker' -ExpectedMessage "*PwshVersion '7.40.1', which is not a PowerShell 7.4 release*"
        }
    }

    It 'returns well-formed versions as written' {
        $dir = Join-Path $TestDrive 'marker-versions-ok'
        $null = New-Item -ItemType Directory -Path $dir
        Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value '{"Target":"Layer","PowerShellVersion":"7.5","PwshVersion":"7.5.11"}'
        InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
            param($Dir)
            $marker = Get-LambdaPSProjectMarker -Path $Dir
            $marker.PowerShellVersion | Should -Be '7.5'
            $marker.PwshVersion | Should -Be '7.5.11'
        }
    }

    It 'writes no error for a folder it cannot read' -Skip:($IsWindows -or ((id -u) -eq '0')) {
        $dir = Join-Path $TestDrive 'marker-unreadable'
        $null = New-Item -ItemType Directory -Path $dir
        [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'UserWrite')
        try {
            InModuleScope $script:moduleName -Parameters @{ Dir = $dir } {
                param($Dir)
                $errors = @()
                Get-LambdaPSProjectMarker -Path $Dir -ErrorVariable errors | Should -BeNullOrEmpty
                $errors | Should -BeNullOrEmpty
            }
        }
        finally { [System.IO.File]::SetUnixFileMode($dir, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute') }
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
