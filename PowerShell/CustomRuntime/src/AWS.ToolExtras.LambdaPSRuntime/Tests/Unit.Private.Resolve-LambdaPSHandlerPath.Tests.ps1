BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # A function source folder, and a folder beside it that the package would not contain.
    $script:case = [System.IO.Path]::Combine($TestDrive, 'handler-path')
    $script:source = [System.IO.Path]::Combine($script:case, 'fn')
    $script:outside = [System.IO.Path]::Combine($script:case, 'outside')
    $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:source, 'lib'))
    $null = New-Item -ItemType Directory -Path $script:outside
    foreach ($file in 'handler.ps1', 'lib/inner.ps1') {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, $file)) -Value 'param($LambdaInput, $LambdaContext)'
    }
    Set-Content -LiteralPath ([System.IO.Path]::Combine($script:outside, 'outside.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
    # The real location of the test folder (on macOS /tmp is a link to /private/tmp), for the expected paths.
    $script:realSource = InModuleScope $script:moduleName -Parameters @{ Path = $script:source } { param($Path) Get-LambdaPSRealPath -Path $Path }

    function Resolve-InModule {
        param([string]$Path, [string]$HandlerScript)
        InModuleScope $script:moduleName -Parameters @{ Path = $Path; HandlerScript = $HandlerScript } {
            param($Path, $HandlerScript)
            Resolve-LambdaPSHandlerPath -Path $Path -HandlerScript $HandlerScript
        }
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

# Compress-LambdaPSFunctionPackage and New-LambdaPSFunctionDeployment both ask this whether the handler
# script is in the source, so the packager and the deployer agree on it.
Describe 'Resolve-LambdaPSHandlerPath' -Tag 'Unit' {
    It 'returns the file itself for a single-file source with no -HandlerScript' {
        $file = [System.IO.Path]::Combine($script:source, 'handler.ps1')
        Resolve-InModule -Path $file | Should -Be ([System.IO.Path]::Combine($script:realSource, 'handler.ps1'))
    }

    It 'returns a single-file source named by -HandlerScript, and $null for another name' {
        $file = [System.IO.Path]::Combine($script:source, 'handler.ps1')
        Resolve-InModule -Path $file -HandlerScript 'handler.ps1' | Should -Be ([System.IO.Path]::Combine($script:realSource, 'handler.ps1'))
        Resolve-InModule -Path $file -HandlerScript 'other.ps1' | Should -BeNullOrEmpty
    }

    It 'returns the script in a folder, at the root or below it' {
        Resolve-InModule -Path $script:source -HandlerScript 'handler.ps1' | Should -Be ([System.IO.Path]::Combine($script:realSource, 'handler.ps1'))
        Resolve-InModule -Path $script:source -HandlerScript 'lib/inner.ps1' | Should -Be ([System.IO.Path]::Combine($script:realSource, 'lib', 'inner.ps1'))
    }

    It 'returns $null for a folder with no -HandlerScript, or a script it does not have' {
        Resolve-InModule -Path $script:source | Should -BeNullOrEmpty
        Resolve-InModule -Path $script:source -HandlerScript 'missing.ps1' | Should -BeNullOrEmpty
        Resolve-InModule -Path ([System.IO.Path]::Combine($TestDrive, 'no-such-source')) -HandlerScript 'handler.ps1' | Should -BeNullOrEmpty
    }

    # Lambda's file system is case-sensitive, so 'Handler.ps1' does not find 'handler.ps1' there.
    It 'matches the file name in a folder case-sensitively' {
        Resolve-InModule -Path $script:source -HandlerScript 'Handler.ps1' | Should -BeNullOrEmpty
    }

    It 'refuses \ in -HandlerScript off Windows, naming / as the separator to use (<Handler>)' -Skip:$IsWindows -ForEach @(
        @{ Handler = 'lib\inner.ps1' }
        @{ Handler = 'handler.ps1\' }
    ) {
        { Resolve-InModule -Path $script:source -HandlerScript $Handler } |
            Should -Throw -ErrorId 'InvalidHandlerPath' -ExpectedMessage "*contains '\'*use '/' instead*"
        # Callers name the handler -Handler or -HandlerScript, so the message names no parameter.
        try { Resolve-InModule -Path $script:source -HandlerScript $Handler } catch { $_.Exception.Message | Should -Not -BeLike '*(Parameter *' }
    }

    It 'still returns a script below the folder written with /' {
        Resolve-InModule -Path $script:source -HandlerScript 'lib/inner.ps1' | Should -Be ([System.IO.Path]::Combine($script:realSource, 'lib', 'inner.ps1'))
    }

    It 'returns $null for a script outside the folder: <Case>' -ForEach @(
        @{ Case = 'a relative path'; Script = '../outside/outside.ps1' }
        @{ Case = 'an absolute path'; Script = 'ABSOLUTE' }
    ) {
        $name = if ($Script -eq 'ABSOLUTE') { [System.IO.Path]::Combine($script:outside, 'outside.ps1') } else { $Script }
        Resolve-InModule -Path $script:source -HandlerScript $name | Should -BeNullOrEmpty
    }

    Context 'links' -Skip:$IsWindows {
        BeforeAll {
            $script:linked = [System.IO.Path]::Combine($script:case, 'linked')
            $null = New-Item -ItemType Directory -Path $script:linked
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:linked, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:linked, 'out')) -Target $script:outside
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:linked, 'escape.ps1')) -Target ([System.IO.Path]::Combine($script:outside, 'outside.ps1'))
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:linked, 'alias.ps1')) -Target 'handler.ps1'
            $script:linkToSource = [System.IO.Path]::Combine($script:case, 'link-to-linked')
            $null = New-Item -ItemType SymbolicLink -Path $script:linkToSource -Target $script:linked
            $script:realLinked = [System.IO.Path]::Combine((Split-Path -Parent $script:realSource), 'linked')
        }

        It 'returns $null for a script reached through a <Case> that leaves the folder' -ForEach @(
            @{ Case = 'folder link'; Script = 'out/outside.ps1' }
            @{ Case = 'file link'; Script = 'escape.ps1' }
        ) {
            Resolve-InModule -Path $script:linked -HandlerScript $Script | Should -BeNullOrEmpty
        }

        It 'returns the real path of a file link that stays in the folder' {
            Resolve-InModule -Path $script:linked -HandlerScript 'alias.ps1' | Should -Be ([System.IO.Path]::Combine($script:realLinked, 'handler.ps1'))
        }

        It 'returns the real path when the source itself is reached through a link' {
            Resolve-InModule -Path $script:linkToSource -HandlerScript 'handler.ps1' | Should -Be ([System.IO.Path]::Combine($script:realLinked, 'handler.ps1'))
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Resolve-LambdaPSHandlerPath raised through a calling cmdlet' -Tag 'Unit' -Skip:$IsWindows {
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'InvalidHandlerPath' }
    ) {
        $Call = { Resolve-LambdaPSHandlerPath -Path $Source -HandlerScript 'lib\inner.ps1' }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString(); Source = $script:source } {
            param($ErrorId, $Call, $Source)
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
            @($ids | Where-Object { $_ -like '*,Resolve-LambdaPSHandlerPath' }) | Should -BeNullOrEmpty
        }
    }
}
