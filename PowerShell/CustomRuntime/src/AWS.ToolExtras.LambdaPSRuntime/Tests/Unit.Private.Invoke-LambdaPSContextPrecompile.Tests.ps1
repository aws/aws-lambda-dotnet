BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # A stand-in for the bundled pwsh: an sh script that prints a line, then exits with -ExitCode and,
    # unless -NoAssembly, writes the -OutputAssembly file named in the -Command text it is given.
    $script:newFakeBundledPwsh = {
        param([string]$Name, [int]$ExitCode = 0, [switch]$NoAssembly)
        $path = [System.IO.Path]::Combine($TestDrive, $Name)
        $write = if ($NoAssembly) { '' } else { "out=`$(printf '%s' `"`$4`" | sed -n `"s/.*-OutputAssembly '\([^']*\)'.*/\1/p`")`necho dll > `"`$out`"`n" }
        Set-Content -LiteralPath $path -Value ("#!/bin/sh`necho 'fake bundled pwsh: a test line, not a warning'`n$write" + "exit $ExitCode")
        $path
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Invoke-LambdaPSContextPrecompile' -Tag 'Unit' {
    BeforeEach {
        $script:source = [System.IO.Path]::Combine($TestDrive, 'PowerShellLambdaContext.cs')
        Set-Content -LiteralPath $script:source -Value 'public class C { }'
        $script:dll = [System.IO.Path]::Combine($TestDrive, "ctx-$([guid]::NewGuid().ToString('N')).dll")
    }

    It 'returns $true alone, sending the bundled pwsh''s output to the host, when it writes the DLL' -Skip:$IsWindows {
        $pwsh = & $script:newFakeBundledPwsh -Name 'pwsh-ok'

        $result = @(InModuleScope $script:moduleName -Parameters @{ Pwsh = $pwsh; Source = $script:source; Dll = $script:dll } {
                param($Pwsh, $Source, $Dll)
                Invoke-LambdaPSContextPrecompile -PwshPath $Pwsh -SourcePath $Source -OutputPath $Dll
            })

        $result.Count | Should -Be 1
        $result[0] | Should -BeTrue
        Test-Path -LiteralPath $script:dll | Should -BeTrue
    }

    # A temp folder mounted noexec, or a glibc pwsh on a musl host, cannot start the binary at all.
    It 'returns $false when the bundled pwsh cannot start' -Skip:$IsWindows {
        $pwsh = [System.IO.Path]::Combine($TestDrive, 'pwsh-noexec')
        Set-Content -LiteralPath $pwsh -Value "$([char]0x7f)ELF not a program" -NoNewline

        $result = InModuleScope $script:moduleName -Parameters @{ Pwsh = $pwsh; Source = $script:source; Dll = $script:dll } {
            param($Pwsh, $Source, $Dll)
            Invoke-LambdaPSContextPrecompile -PwshPath $Pwsh -SourcePath $Source -OutputPath $Dll
        }

        $result | Should -BeFalse
    }

    It 'throws ContextCompileFailed when the bundled pwsh <Case>' -Skip:$IsWindows -ForEach @(
        @{ Case = 'exits non-zero'; Name = 'pwsh-fail'; ExitCode = 1; NoAssembly = $true; Reason = '*exited with 1*' }
        @{ Case = 'writes no assembly'; Name = 'pwsh-noasm'; ExitCode = 0; NoAssembly = $true; Reason = '*wrote no assembly*' }
    ) {
        $pwsh = & $script:newFakeBundledPwsh -Name $Name -ExitCode $ExitCode -NoAssembly:$NoAssembly

        $err = { InModuleScope $script:moduleName -Parameters @{ Pwsh = $pwsh; Source = $script:source; Dll = $script:dll } {
                    param($Pwsh, $Source, $Dll)
                    Invoke-LambdaPSContextPrecompile -PwshPath $Pwsh -SourcePath $Source -OutputPath $Dll
                } } | Should -Throw -ErrorId 'ContextCompileFailed' -PassThru
        $err.Exception.Message | Should -BeLike $Reason
    }
}
