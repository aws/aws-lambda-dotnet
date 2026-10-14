BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Invoke-LambdaPSExternalTool' -Tag 'Unit' {
    BeforeAll {
        $script:pwshPath = [System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }))
    }

    It 'throws ExternalToolFailed naming the exit code when the tool fails' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            $err = $null
            try { Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', 'exit 3' } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'ExternalToolFailed'
            $err.Exception.Message | Should -BeLike '*exited with code 3.'
        }
    }

    It 'returns quietly when the tool succeeds' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            { Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', 'exit 0' } | Should -Not -Throw
        }
    }

    It 'sends the tool''s stdout to the host and returns nothing' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            Mock Out-Host -MockWith { }
            $output = Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', "'tool output'"
            $output | Should -BeNullOrEmpty
            Should -Invoke Out-Host -Times 1 -Exactly -ParameterFilter { $InputObject -eq 'tool output' }
        }
    }

    It 'sends the tool''s stderr lines to the host as well' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            Mock Out-Host -MockWith { }
            Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', "[Console]::Error.WriteLine('err line')"
            Should -Invoke Out-Host -Times 1 -Exactly -ParameterFilter { $InputObject -eq 'err line' }
        }
    }

    It 'attaches the tool''s output to the ExternalToolFailed exception as Data[''Output'']' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            Mock Out-Host -MockWith { }
            $err = $null
            try { Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', "'out line'; [Console]::Error.WriteLine('exec format error'); exit 1" } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'ExternalToolFailed'
            $output = $err.Exception.Data['Output']
            , $output | Should -BeOfType [string[]]
            $output | Should -Be @('out line', 'exec format error')
        }
    }

    It 'keeps only the last 50 lines in Data[''Output'']' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            Mock Out-Host -MockWith { }
            $err = $null
            try { Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', "1..60 | ForEach-Object { 'line ' + `$_ }; exit 1" } catch { $err = $_ }
            $output = $err.Exception.Data['Output']
            $output | Should -HaveCount 50
            $output[0] | Should -Be 'line 11'
            $output[-1] | Should -Be 'line 60'
            Should -Invoke Out-Host -Times 60 -Exactly
        }
    }

    # One Out-Host pipeline, not one per line, which is slow for a verbose tool.
    It 'writes the whole stream through one Out-Host' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            $script:outHostStarts = 0
            $script:outHostLines = [System.Collections.Generic.List[string]]::new()
            function script:Out-Host { [CmdletBinding()] param([Parameter(ValueFromPipeline)] $InputObject) begin { $script:outHostStarts++ } process { $script:outHostLines.Add([string]$InputObject) } }
            try {
                Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', "1..5 | ForEach-Object { 'line ' + `$_ }"
            }
            finally {
                Remove-Item -LiteralPath 'function:script:Out-Host'
            }
            $script:outHostStarts | Should -Be 1
            @($script:outHostLines) | Should -Be @('line 1', 'line 2', 'line 3', 'line 4', 'line 5')
        }
    }

    It 'throws ExternalToolFailed even when PSNativeCommandUseErrorActionPreference is on' {
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath } {
            param($pwshPath)
            $PSNativeCommandUseErrorActionPreference = $true
            $err = $null
            try { Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-Command', 'exit 3' -ErrorAction Stop } catch { $err = $_ }
            $err.FullyQualifiedErrorId | Should -Be 'ExternalToolFailed'
        }
    }

    It 'passes an argument with quotes and spaces as one argument, under a caller''s Legacy argument passing' {
        $echo = [System.IO.Path]::Combine($TestDrive, 'echo-first-argument.ps1')
        Set-Content -LiteralPath $echo -Value 'param([string]$First) "ARG:[$First] COUNT:$($args.Count)"'
        InModuleScope $script:moduleName -Parameters @{ pwshPath = $script:pwshPath; echo = $echo } {
            param($pwshPath, $echo)
            Mock Out-Host -MockWith { }
            $PSNativeCommandArgumentPassing = 'Legacy'
            Invoke-LambdaPSExternalTool -Executable $pwshPath -ArgumentList '-NoProfile', '-File', $echo, 'say "hi" now'
            Should -Invoke Out-Host -Times 1 -Exactly -ParameterFilter { $InputObject -eq 'ARG:[say "hi" now] COUNT:0' }
        }
    }

    Context 'when a function or alias has the executable''s name' {
        BeforeAll {
            # A stand-in executable first on PATH that records that it ran.
            $script:toolDirectory = [System.IO.Path]::Combine($TestDrive, 'tool-bin')
            $null = New-Item -ItemType Directory -Path $script:toolDirectory -Force
            $script:toolLog = [System.IO.Path]::Combine($script:toolDirectory, 'ran.log')
            if ($IsWindows) {
                Set-Content -LiteralPath ([System.IO.Path]::Combine($script:toolDirectory, 'Invoke-LambdaPSTestTool.cmd')) -Value "@echo %*>>`"$script:toolLog`""
            }
            else {
                $tool = [System.IO.Path]::Combine($script:toolDirectory, 'Invoke-LambdaPSTestTool')
                Set-Content -LiteralPath $tool -Value "#!/bin/sh`nprintf '%s\n' `"`$*`" >> '$script:toolLog'"
                [System.IO.File]::SetUnixFileMode($tool, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
            }
            $script:savedPath = $env:PATH
            $env:PATH = $script:toolDirectory + [System.IO.Path]::PathSeparator + $env:PATH
        }

        AfterAll {
            $env:PATH = $script:savedPath
        }

        BeforeEach {
            Remove-Item -LiteralPath $script:toolLog -ErrorAction SilentlyContinue
        }

        It 'runs the executable, not a function of the same name' {
            InModuleScope $script:moduleName -Parameters @{ toolLog = $script:toolLog } {
                param($toolLog)
                function script:Invoke-LambdaPSTestTool { 'FUNCTION RAN' }
                try {
                    Mock Out-Host -MockWith { }
                    Invoke-LambdaPSExternalTool -Executable 'Invoke-LambdaPSTestTool' -ArgumentList 'build', '--tag', 'x'
                    Should -Invoke Out-Host -Times 0 -Exactly -ParameterFilter { $InputObject -eq 'FUNCTION RAN' }
                }
                finally {
                    Remove-Item -LiteralPath 'function:script:Invoke-LambdaPSTestTool'
                }
                Get-Content -LiteralPath $toolLog | Should -Be 'build --tag x'
            }
        }

        It 'runs the executable, not an alias of the same name' {
            InModuleScope $script:moduleName -Parameters @{ toolLog = $script:toolLog; pwshPath = $script:pwshPath } {
                param($toolLog, $pwshPath)
                Set-Alias -Name 'Invoke-LambdaPSTestTool' -Value $pwshPath -Scope Script
                try {
                    Invoke-LambdaPSExternalTool -Executable 'Invoke-LambdaPSTestTool' -ArgumentList 'version'
                }
                finally {
                    Remove-Item -LiteralPath 'alias:Invoke-LambdaPSTestTool'
                }
                Get-Content -LiteralPath $toolLog | Should -Be 'version'
            }
        }

        It 'refuses a name that is only a function, as for a missing executable' {
            { Invoke-InFreshRunspace -Command "function lambdaps-test-function-only { 'FUNCTION RAN' }; & (Get-Module $script:moduleName) { Invoke-LambdaPSExternalTool -Executable 'lambdaps-test-function-only' -ArgumentList 'x'; 'CONTINUED' }" } |
                Should -Throw "*'lambdaps-test-function-only' is not recognized*"
        }
    }

    # Pester runs every It inside a try, which would stop the caller in-process either way.
    It 'stops its caller when the executable does not exist' {
        # Script-terminating: the whole script stops, so Invoke on the runspace throws.
        { Invoke-InFreshRunspace -Command "& (Get-Module $script:moduleName) { Invoke-LambdaPSExternalTool -Executable 'lambdaps-no-such-tool' -ArgumentList 'x'; 'CONTINUED' }" } |
            Should -Throw "*'lambdaps-no-such-tool' is not recognized*"
    }
}
