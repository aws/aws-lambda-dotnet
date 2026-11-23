BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Read-LambdaPSBootstrap -Bytes' -Tag 'Unit' {
    It 'reads a known-form bootstrap: the pwsh statement, its environment and the writes' {
        InModuleScope $script:moduleName {
            $text = "#!/bin/sh`n# a comment`nexport A='one two' B=2 # trailing`nC=3; export C`nexec 2>&1`necho started | tee /tmp/log`nDOTNET_X=1 exec env D=4 /opt/powershell/pwsh -NoProfile -File '/opt/runtime.ps1'`nexport AFTER=1`n"
            $bootstrap = Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($text))

            $bootstrap.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.Bootstrap'
            $bootstrap.StartsWithShebang | Should -BeTrue
            $bootstrap.Shebang | Should -Be '#!/bin/sh'
            @($bootstrap.Unrecognised) | Should -BeNullOrEmpty
            $bootstrap.Pwsh.Program | Should -Be '/opt/powershell/pwsh'
            $bootstrap.Pwsh.IsExec | Should -BeTrue
            $bootstrap.Pwsh.Arguments | Should -Be @('-NoProfile', '-File', '/opt/runtime.ps1')
            $bootstrap.Environment['A'] | Should -Be 'one two'
            $bootstrap.Environment['C'] | Should -Be '3'
            $bootstrap.Environment['DOTNET_X'] | Should -Be '1'
            $bootstrap.Environment['D'] | Should -Be '4'
            $bootstrap.Environment.ContainsKey('AFTER') | Should -BeFalse -Because 'nothing after an exec of pwsh runs'
            @($bootstrap.Writes) | Should -Contain '/tmp/log'
        }
    }

    It 'gives the pwsh command''s own assignment over an exported one' {
        InModuleScope $script:moduleName {
            $bootstrap = Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`nexport X=1`nX=0 exec pwsh"))
            $bootstrap.Environment['X'] | Should -Be '0'
        }
    }

    It 'keeps variable names case-sensitive' {
        InModuleScope $script:moduleName {
            $bootstrap = Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes("#!/bin/sh`nexport path=/x`nexec pwsh"))
            $bootstrap.Environment.ContainsKey('path') | Should -BeTrue
            $bootstrap.Environment.ContainsKey('PATH') | Should -BeFalse
        }
    }

    It 'names <Construct> in Unrecognised' -ForEach @(
        @{ Construct = 'a function'; Line = 'start() { exec pwsh; }' }
        @{ Construct = 'an if, case or loop block'; Line = 'if [ -n "$X" ]; then exec pwsh; fi' }
        @{ Construct = 'a here-doc'; Line = 'cat <<EOF >/tmp/x' }
        @{ Construct = 'a command substitution'; Line = 'V=$(date)' }
        @{ Construct = 'a variable as the program'; Line = 'exec "$PWSH"' }
        @{ Construct = "a '\' line continuation"; Line = "export A=1 \" }
        @{ Construct = "an '&&' or '||' list"; Line = 'cd /tmp && exec pwsh' }
        @{ Construct = "a background '&'"; Line = 'sleep 1 &' }
        @{ Construct = 'a sourced file'; Line = '. /opt/env.sh' }
        @{ Construct = 'an exec of another program'; Line = 'exec /bin/sh' }
        @{ Construct = 'more than one pwsh invocation'; Line = "pwsh -Command warmup`nexec pwsh" }
        @{ Construct = 'a quote left open at the end of a line'; Line = "echo 'open" }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Text = "#!/bin/sh`n$Line`n"; Construct = $Construct } {
            param($Text, $Construct)
            @((Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Text))).Unrecognised) | Should -Contain $Construct
        }
    }

    It 'recognises <Case>' -ForEach @(
        @{ Case = 'an exec that only redirects'; Line = "exec >/tmp/boot.log 2>&1`nexec pwsh" }
        @{ Case = '<< inside quotes'; Line = "echo 'usage: cat <<EOF'`nexec pwsh" }
        @{ Case = 'a here-string'; Line = "cat <<< x`nexec pwsh" }
        @{ Case = 'a pwsh statement without exec'; Line = 'DOTNET_X=1 /opt/powershell/pwsh -File /opt/runtime.ps1' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Text = "#!/bin/sh`n$Line`n" } {
            param($Text)
            $bootstrap = Read-LambdaPSBootstrap -Bytes ([System.Text.Encoding]::UTF8.GetBytes($Text))
            @($bootstrap.Unrecognised) | Should -BeNullOrEmpty
            $bootstrap.Pwsh | Should -Not -BeNullOrEmpty
        }
    }

    It 'names the byte order mark and decodes the text without it' {
        InModuleScope $script:moduleName {
            $encoding = [System.Text.UnicodeEncoding]::new($false, $true)
            $bootstrap = Read-LambdaPSBootstrap -Bytes ([byte[]]($encoding.GetPreamble() + $encoding.GetBytes("#!/bin/sh`nexec pwsh")))
            $bootstrap.ByteOrderMark | Should -Be 'UTF-16 LE (FF FE)'
            $bootstrap.StartsWithShebang | Should -BeFalse
            $bootstrap.Shebang | Should -Be '#!/bin/sh'
        }
    }
}

Describe 'Read-LambdaPSBootstrap -Path' -Tag 'Unit' {
    BeforeAll {
        $script:readDir = [System.IO.Path]::Combine($TestDrive, 'read')
        $null = New-Item -ItemType Directory -Path $script:readDir -Force
    }

    It 'reads a regular file' {
        $path = [System.IO.Path]::Combine($script:readDir, 'regular')
        [System.IO.File]::WriteAllText($path, "#!/bin/sh`nexec pwsh`n")
        InModuleScope $script:moduleName -Parameters @{ Path = $path } {
            param($Path)
            (Read-LambdaPSBootstrap -Path $Path).Pwsh.Program | Should -Be 'pwsh'
        }
    }

    It 'returns <ErrorId> for <Case>' -ForEach @(
        @{ Case = 'a missing file'; ErrorId = 'BootstrapNotFound'; Make = { param($p) } }
        @{ Case = 'a directory'; ErrorId = 'BootstrapNotFound'; Make = { param($p) $null = New-Item -ItemType Directory -Path $p } }
        @{ Case = 'an empty file'; ErrorId = 'BootstrapNotReadable'; Make = { param($p) [System.IO.File]::WriteAllText($p, '') } }
        @{ Case = 'a file over 1 MB'; ErrorId = 'BootstrapNotReadable'; Make = { param($p) [System.IO.File]::WriteAllBytes($p, [byte[]]::new(1048577)) } }
    ) {
        $path = [System.IO.Path]::Combine($script:readDir, ($Case -replace '\W', '-'))
        & $Make $path
        InModuleScope $script:moduleName -Parameters @{ Path = $path; ErrorId = $ErrorId } {
            param($Path, $ErrorId)
            $result = Read-LambdaPSBootstrap -Path $Path
            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.BootstrapReadError'
            $result.ErrorId | Should -Be $ErrorId
        }
    }

    # Links, FIFOs and devices exist only on Unix.
    Context 'special files' -Skip:$IsWindows {
        It 'returns <ErrorId> for <Case>, without blocking' -ForEach @(
            @{ Case = 'a FIFO'; ErrorId = 'BootstrapNotReadable'; Make = { param($p) & mkfifo $p } }
            @{ Case = 'a link to a device'; ErrorId = 'BootstrapNotReadable'; Make = { param($p) $null = New-Item -ItemType SymbolicLink -Path $p -Target '/dev/zero' } }
            @{ Case = 'a dangling link'; ErrorId = 'BootstrapNotFound'; Make = { param($p) $null = New-Item -ItemType SymbolicLink -Path $p -Target ($p + '-missing') } }
        ) {
            $path = [System.IO.Path]::Combine($script:readDir, ($Case -replace '\W', '-'))
            & $Make $path
            InModuleScope $script:moduleName -Parameters @{ Path = $path; ErrorId = $ErrorId } {
                param($Path, $ErrorId)
                (Read-LambdaPSBootstrap -Path $Path).ErrorId | Should -Be $ErrorId
            }
        }

        It 'follows a link to a regular file' {
            $target = [System.IO.Path]::Combine($script:readDir, 'link-target')
            [System.IO.File]::WriteAllText($target, "#!/bin/sh`nexec pwsh`n")
            $link = [System.IO.Path]::Combine($script:readDir, 'link')
            $null = New-Item -ItemType SymbolicLink -Path $link -Target $target
            InModuleScope $script:moduleName -Parameters @{ Path = $link } {
                param($Path)
                (Read-LambdaPSBootstrap -Path $Path).StartsWithShebang | Should -BeTrue
            }
        }
    }
}
