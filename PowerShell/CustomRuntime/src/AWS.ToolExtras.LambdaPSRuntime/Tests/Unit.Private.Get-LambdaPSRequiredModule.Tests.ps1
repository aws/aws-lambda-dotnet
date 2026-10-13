BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSRequiredModule' -Tag 'Unit' {
    It 'extracts a pinned module specification from a handler #Requires line' {
        $handler = @'
#Requires -Modules @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }
param($LambdaInput, $LambdaContext)
'@
        $path = Join-Path $TestDrive 'handler-pinned.ps1'
        Set-Content -LiteralPath $path -Value $handler

        InModuleScope $script:moduleName -Parameters @{ p = $path } {
            param($p)
            $result = @(Get-LambdaPSRequiredModule -Path $p)
            $result.Count | Should -Be 1
            $result[0].Name | Should -Be 'AWS.Tools.S3'
            $result[0].RequiredVersion | Should -Be '5.0.274'
        }
    }

    It 'extracts every requirement verbatim, including an unpinned one (rejection is the caller''s job)' {
        $handler = @'
#Requires -Modules @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }
#Requires -Modules @{ ModuleName = 'AWS.Tools.DynamoDBv2'; ModuleVersion = '5.0.0' }
param($LambdaInput, $LambdaContext)
'@
        $path = Join-Path $TestDrive 'handler-mixed.ps1'
        Set-Content -LiteralPath $path -Value $handler

        InModuleScope $script:moduleName -Parameters @{ p = $path } {
            param($p)
            $result = @(Get-LambdaPSRequiredModule -Path $p)
            $result.Count | Should -Be 2
            ($result | Where-Object { $_.Name -eq 'AWS.Tools.DynamoDBv2' }).Version | Should -Be '5.0.0'
        }
    }

    It 'refuses <Requirement>, giving its own reason, pointing at AWS.Tools.*' -ForEach @(
        @{ Requirement = 'AWSPowerShell.NetCore'; Line = '#Requires -Modules AWSPowerShell.NetCore'; Reason = "*AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's 250 MB limit for a .zip package and its layers, and in a container image, importing it takes over 10 seconds and over 1 GB of memory on every cold start*"; Other = '*AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda*' }
        @{ Requirement = 'AWSPowerShell'; Line = "#Requires -Modules @{ ModuleName = 'AWSPowerShell'; ModuleVersion = '4.1.0' }"; Reason = '*AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda*'; Other = "*AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's 250 MB limit for a .zip package and its layers, and in a container image, importing it takes over 10 seconds and over 1 GB of memory on every cold start*" }
    ) {
        $path = Join-Path $TestDrive "handler-monolithic-$Requirement.ps1"
        Set-Content -LiteralPath $path -Value ($Line, 'param($LambdaInput, $LambdaContext)')

        InModuleScope $script:moduleName -Parameters @{ p = $path; name = $Requirement; reason = $Reason; other = $Other } {
            param($p, $name, $reason, $other)
            $err = { Get-LambdaPSRequiredModule -Path $p } |
                Should -Throw -ErrorId 'AWSPowerShellModuleNotSupported' -PassThru
            $err.Exception.Message | Should -BeLike "*'$name' (#Requires -Modules), which cannot be packaged for Lambda: *Require the AWS.Tools.* modules for the services you use instead*"
            $err.Exception.Message | Should -BeLike $reason
            $err.Exception.Message | Should -Not -BeLike $other
        }
    }

    It 'gives each module its own reason when a handler requires both' {
        $path = Join-Path $TestDrive 'handler-monolithic-both.ps1'
        Set-Content -LiteralPath $path -Value ('#Requires -Modules AWSPowerShell, AWSPowerShell.NetCore', 'param($LambdaInput, $LambdaContext)')

        InModuleScope $script:moduleName -Parameters @{ p = $path } {
            param($p)
            $err = { Get-LambdaPSRequiredModule -Path $p } | Should -Throw -ErrorId 'AWSPowerShellModuleNotSupported' -PassThru
            $err.Exception.Message | Should -BeLike "*'AWSPowerShell' and 'AWSPowerShell.NetCore'*"
            $err.Exception.Message | Should -BeLike '*AWSPowerShell is the Windows PowerShell (Desktop edition) module and does not run in PowerShell 7 on Lambda*'
            $err.Exception.Message | Should -BeLike "*AWSPowerShell.NetCore holds every AWS service (over 600 MB installed): more than Lambda's 250 MB limit for a .zip package and its layers, and in a container image, importing it takes over 10 seconds and over 1 GB of memory on every cold start*"
        }
    }

    It 'still returns AWS.Tools.* requirements next to each other' {
        $handler = @'
#Requires -Modules @{ ModuleName = 'AWS.Tools.Common'; RequiredVersion = '5.0.274' }
#Requires -Modules @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' }
param($LambdaInput, $LambdaContext)
'@
        $path = Join-Path $TestDrive 'handler-aws-tools.ps1'
        Set-Content -LiteralPath $path -Value $handler

        InModuleScope $script:moduleName -Parameters @{ p = $path } {
            param($p)
            @(Get-LambdaPSRequiredModule -Path $p).Name | Should -Be @('AWS.Tools.Common', 'AWS.Tools.S3')
        }
    }

    It 'returns an empty array for a handler that declares no module requirements' {
        $path = Join-Path $TestDrive 'handler-none.ps1'
        Set-Content -LiteralPath $path -Value 'param($LambdaInput, $LambdaContext)'

        InModuleScope $script:moduleName -Parameters @{ p = $path } {
            param($p)
            @(Get-LambdaPSRequiredModule -Path $p).Count | Should -Be 0
        }
    }

    It 'throws HandlerScriptNotFound when the script does not exist' {
        $missing = Join-Path $TestDrive 'does-not-exist.ps1'
        InModuleScope $script:moduleName -Parameters @{ p = $missing } {
            param($p)
            { Get-LambdaPSRequiredModule -Path $p } |
                Should -Throw -ErrorId 'HandlerScriptNotFound'
        }
    }

    # The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
    # surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
    It 'surfaces <ErrorId> (<Case>) from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'HandlerScriptNotFound'; Case = 'no script'; Call = { Get-LambdaPSRequiredModule -Path (Join-Path $Arg 'does-not-exist.ps1') } }
        @{
            ErrorId = 'AWSPowerShellModuleNotSupported'; Case = 'AWSPowerShell'
            Prepare = { $p = Join-Path $TestDrive 'handler-caller.ps1'; Set-Content -LiteralPath $p -Value '#Requires -Modules AWSPowerShell'; $p }
            Call = { Get-LambdaPSRequiredModule -Path $Arg }
        }
    ) {
        $arg = if ($Prepare) { & $Prepare } else { $TestDrive }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Setup = "$Setup"; Call = "$Call"; Arg = $arg } {
            param($ErrorId, $Setup, $Call, $Arg)
            . ([scriptblock]::Create($Setup))
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call ([scriptblock]::Create($Call)) -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Get-LambdaPSRequiredModule' }) | Should -BeNullOrEmpty
        }
    }
}
