BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # Materialize each non-compliant fixture project once. File writes (not function
    # declarations) in BeforeAll are permitted; paths are shared via script scope.
    $script:bootstrapFixture = @{
        wrongRuntimePath = @'
#!/bin/sh
export PATH="/usr/bin"
export LD_LIBRARY_PATH="/opt/lib"
export PSModulePath="/var/task/modules:/opt/modules"
exec /opt/pwsh
'@
        missingEnv = @'
#!/bin/sh
cd /var/runtime
export PSModulePath="/var/task/modules:/var/runtime/modules"
exec pwsh
'@
        reserved = @'
#!/bin/sh
cd /var/runtime
export PATH="/usr/bin"
export LD_LIBRARY_PATH="/lib"
echo hi > /var/runtime/state.txt
export PSModulePath="/var/task/modules:/opt/modules:/var/runtime/modules"
exec pwsh
'@
        noinvariant = @'
#!/bin/sh
exec /opt/powershell/pwsh -noprofile -File /opt/runtime.ps1
'@
    }

    $script:fixturePath = @{}
    foreach ($name in $script:bootstrapFixture.Keys) {
        $dir = [System.IO.Path]::Combine($TestDrive, $name)
        $null = New-Item -ItemType Directory -Path $dir -Force
        [System.IO.File]::WriteAllText(
            [System.IO.Path]::Combine($dir, 'bootstrap'),
            $script:bootstrapFixture[$name])
        $script:fixturePath[$name] = $dir
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSRuntimeProject' -Tag 'Unit' {
    Context 'a well-formed scaffolded project' {
        It 'passes every rule' {
            $target = [System.IO.Path]::Combine($TestDrive, 'good')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            $findings = Test-LambdaPSRuntimeProject -Path $target
            $findings | Should -Not -BeNullOrEmpty
            ($findings | Where-Object { -not $_.Passed }) | Should -BeNullOrEmpty
        }
    }

    Context 'a non-compliant bootstrap' {
        It 'flags a bootstrap that never targets /var/runtime (R-RuntimePath)' {
            $findings = Test-LambdaPSRuntimeProject -Path $script:fixturePath['wrongRuntimePath'] -ErrorAction SilentlyContinue
            $runtimePathFinding = $findings | Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeFalse
            $runtimePathFinding.Severity | Should -Be 'Error'
        }

        It 'flags missing PATH and LD_LIBRARY_PATH exports (R-Env-Path, R-Env-LdLibraryPath)' {
            $findings = Test-LambdaPSRuntimeProject -Path $script:fixturePath['missingEnv'] -ErrorAction SilentlyContinue
            foreach ($ruleId in 'R-Env-Path', 'R-Env-LdLibraryPath') {
                $finding = @($findings | Where-Object { $_.RuleId -eq $ruleId })
                $finding.Count | Should -Be 1 -Because "$ruleId must be reported, not missing"
                $finding[0].Passed | Should -BeFalse
            }
        }

        It 'flags a write to a reserved read-only path' {
            $findings = Test-LambdaPSRuntimeProject -Path $script:fixturePath['reserved'] -ErrorAction SilentlyContinue
            $finding = @($findings | Where-Object { $_.RuleId -eq 'R-ReservedWrite' })
            $finding.Count | Should -Be 1
            $finding[0].Passed | Should -BeFalse
        }

        It 'does not judge PSModulePath order (module placement is customer preference)' {
            $findings = Test-LambdaPSRuntimeProject -Path $script:fixturePath['reserved'] -ErrorAction SilentlyContinue
            ($findings | Where-Object { $_.RuleId -like '*ModulePath*' }) | Should -BeNullOrEmpty
        }
    }

    Context 'a container-free bootstrap missing invariant globalization' {
        It 'flags a Layer bootstrap that does not export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT (R-Globalization)' {
            $findings = Test-LambdaPSRuntimeProject -Path $script:fixturePath['noinvariant'] -Target Layer -ErrorAction SilentlyContinue
            $g = $findings | Where-Object { $_.RuleId -eq 'R-Globalization' }
            $g.Passed | Should -BeFalse
            $g.Severity | Should -Be 'Error'
        }

        It 'reports R-Globalization not-applicable (passing) for a CMR project' {
            $target = [System.IO.Path]::Combine($TestDrive, 'cmr-glob')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            $g = Test-LambdaPSRuntimeProject -Path $target |
                Where-Object { $_.RuleId -eq 'R-Globalization' }
            $g.Passed | Should -BeTrue
        }
    }

    Context 'a well-formed scaffolded Layer project' {
        It 'lints with zero failures (target resolved from the marker)' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-good')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer
            $findings = Test-LambdaPSRuntimeProject -Path $target
            $findings | Should -Not -BeNullOrEmpty
            ($findings | Where-Object { -not $_.Passed }) | Should -BeNullOrEmpty
        }

        It 'passes R-RuntimePath for a layer bootstrap referencing /opt' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-runtimepath-ok')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer
            $runtimePathFinding = Test-LambdaPSRuntimeProject -Path $target |
                Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeTrue
        }
    }

    Context 'a Layer project whose bootstrap uses the wrong runtime path' {
        It 'fails R-RuntimePath when a Layer bootstrap references /var/runtime' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'layer-runtimepath-bad')
            $null = New-Item -ItemType Directory -Path $dir -Force
            # A known form, so the wrong path is a clear violation rather than unverified.
            $badBootstrap = @'
#!/bin/sh
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
exec /var/runtime/powershell/pwsh -NoProfile -NonInteractive -File /var/runtime/runtime.ps1
'@
            [System.IO.File]::WriteAllText(
                [System.IO.Path]::Combine($dir, 'bootstrap'), $badBootstrap)
            [System.IO.File]::WriteAllText(
                [System.IO.Path]::Combine($dir, 'lambdaps-runtime.json'),
                '{ "Target": "Layer", "PowerShellVersion": "7.4" }')

            $runtimePathFinding = Test-LambdaPSRuntimeProject -Path $dir -ErrorAction SilentlyContinue |
                Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeFalse
            $runtimePathFinding.Severity | Should -Be 'Error'
        }
    }

    Context 'a well-formed scaffolded SelfContainedZip project' {
        It 'lints with zero failures (target resolved from the marker)' {
            $target = [System.IO.Path]::Combine($TestDrive, 'scz-good')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
            $findings = Test-LambdaPSRuntimeProject -Path $target
            $findings | Should -Not -BeNullOrEmpty
            ($findings | Where-Object { -not $_.Passed }) | Should -BeNullOrEmpty
        }

        It 'passes R-RuntimePath for a bootstrap referencing /var/task' {
            $target = [System.IO.Path]::Combine($TestDrive, 'scz-runtimepath-ok')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
            $runtimePathFinding = Test-LambdaPSRuntimeProject -Path $target |
                Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeTrue
        }
    }

    Context 'a SelfContainedZip project whose bootstrap uses the wrong runtime path' {
        It 'fails R-RuntimePath when a SelfContainedZip bootstrap references /var/runtime' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'scz-runtimepath-bad')
            $null = New-Item -ItemType Directory -Path $dir -Force
            # A known form, so the wrong path is a clear violation rather than unverified.
            $badBootstrap = @'
#!/bin/sh
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
exec /var/runtime/powershell/pwsh -NoProfile -NonInteractive -File /var/runtime/runtime.ps1
'@
            [System.IO.File]::WriteAllText(
                [System.IO.Path]::Combine($dir, 'bootstrap'), $badBootstrap)
            [System.IO.File]::WriteAllText(
                [System.IO.Path]::Combine($dir, 'lambdaps-runtime.json'),
                '{ "Target": "SelfContainedZip", "PowerShellVersion": "7.4" }')

            $runtimePathFinding = Test-LambdaPSRuntimeProject -Path $dir -ErrorAction SilentlyContinue |
                Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeFalse
            $runtimePathFinding.Severity | Should -Be 'Error'
        }
    }

    Context 'a well-formed scaffolded Container project' {
        It 'lints with zero failures (target resolved from the marker)' {
            $target = [System.IO.Path]::Combine($TestDrive, 'container-good')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
            $findings = Test-LambdaPSRuntimeProject -Path $target
            $findings | Should -Not -BeNullOrEmpty
            ($findings | Where-Object { -not $_.Passed }) | Should -BeNullOrEmpty
        }

        It 'passes R-RuntimePath for a bootstrap referencing /var/runtime' {
            $target = [System.IO.Path]::Combine($TestDrive, 'container-runtimepath-ok')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
            $runtimePathFinding = Test-LambdaPSRuntimeProject -Path $target |
                Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeTrue
        }
    }

    Context 'a Container project whose bootstrap uses the wrong runtime path' {
        It 'fails R-RuntimePath when a Container bootstrap references /opt instead of /var/runtime' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'container-runtimepath-bad')
            $null = New-Item -ItemType Directory -Path $dir -Force
            # A known form, so the wrong path is a clear violation rather than unverified.
            $badBootstrap = @'
#!/bin/sh
exec /opt/powershell/pwsh -NoProfile -NonInteractive -File /opt/runtime.ps1
'@
            [System.IO.File]::WriteAllText(
                [System.IO.Path]::Combine($dir, 'bootstrap'), $badBootstrap)
            [System.IO.File]::WriteAllText(
                [System.IO.Path]::Combine($dir, 'lambdaps-runtime.json'),
                '{ "Target": "Container", "PowerShellVersion": "7.4" }')

            $runtimePathFinding = Test-LambdaPSRuntimeProject -Path $dir -ErrorAction SilentlyContinue |
                Where-Object { $_.RuleId -eq 'R-RuntimePath' }
            $runtimePathFinding.Passed | Should -BeFalse
            $runtimePathFinding.Severity | Should -Be 'Error'
        }
    }

    # The cmdlet has to be usable as a GATE, not only as a report. If it only returned findings, a
    # script could run the validator in the documented scaffold -> validate -> build sequence, see no
    # error, and go on to build an image whose bootstrap had failed two rules.
    Context 'failing as a gate' {
        It 'writes each failing rule to the error stream' {
            $errors = @()
            $null = Test-LambdaPSRuntimeProject -Path $script:fixturePath['missingEnv'] `
                -ErrorAction SilentlyContinue -ErrorVariable errors

            $errors.Count | Should -BeGreaterThan 0
            @($errors.FullyQualifiedErrorId) | Should -Contain 'R-Env-Path,Test-LambdaPSRuntimeProject'
        }

        It 'still returns every finding alongside the errors' {
            $findings = Test-LambdaPSRuntimeProject -Path $script:fixturePath['missingEnv'] -ErrorAction SilentlyContinue

            # All seven rules are reported, not just the failures.
            $findings.Count | Should -Be 7
        }

        It 'halts a script under -ErrorAction Stop' {
            { Test-LambdaPSRuntimeProject -Path $script:fixturePath['wrongRuntimePath'] -ErrorAction Stop } |
                Should -Throw -ErrorId 'R-RuntimePath,Test-LambdaPSRuntimeProject'
        }

        It 'writes nothing to the error stream for a compliant project' {
            $target = [System.IO.Path]::Combine($TestDrive, 'gate-clean')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'

            $errors = @()
            $null = Test-LambdaPSRuntimeProject -Path $target -ErrorVariable errors
            $? | Should -BeTrue
            $errors.Count | Should -Be 0
        }

        It 'returns $false from -Quiet for a failing project and emits no findings' {
            $result = Test-LambdaPSRuntimeProject -Path $script:fixturePath['missingEnv'] -Quiet

            $result | Should -BeOfType [bool]
            $result | Should -BeFalse
        }

        It 'returns $true from -Quiet for a compliant project' {
            $target = [System.IO.Path]::Combine($TestDrive, 'gate-quiet-ok')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'

            Test-LambdaPSRuntimeProject -Path $target -Quiet | Should -BeTrue
        }

        It 'reports a hand-edited marker naming an unknown target as InvalidProjectMarker' {
            $target = [System.IO.Path]::Combine($TestDrive, 'gate-bad-marker')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Value '{"Target":"layer2"}'

            { Test-LambdaPSRuntimeProject -Path $target -ErrorAction Stop } | Should -Throw -ErrorId 'InvalidProjectMarker,Test-LambdaPSRuntimeProject'
        }

        It 'takes the project from a piped directory or deployment result' {
            $target = [System.IO.Path]::Combine($TestDrive, 'gate-pipeline')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'

            Get-Item -LiteralPath $target | Test-LambdaPSRuntimeProject -Quiet | Should -BeTrue
            [PSCustomObject]@{ ProjectPath = $target } | Test-LambdaPSRuntimeProject -Quiet | Should -BeTrue
        }

        It 'suppresses the per-rule errors under -Quiet' {
            $errors = @()
            $null = Test-LambdaPSRuntimeProject -Path $script:fixturePath['missingEnv'] -Quiet -ErrorVariable errors
            $errors.Count | Should -Be 0
        }
    }

    Context 'line endings (R-LineEndings)' {
        It 'fails a bootstrap saved with CRLF line endings' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'crlf')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            $path = [System.IO.Path]::Combine($dir, 'bootstrap')
            [System.IO.File]::WriteAllText($path, [System.IO.File]::ReadAllText($path).Replace("`n", "`r`n"))

            $errors = @()
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -ErrorAction SilentlyContinue -ErrorVariable errors)
            $finding = $findings | Where-Object RuleId -eq 'R-LineEndings'
            $finding.Passed | Should -BeFalse
            $finding.Severity | Should -Be 'Error'
            $errors[0].Exception.Message | Should -BeLike '`[R-LineEndings`] *'
            $findings.Count | Should -Be 7
        }

        It 'passes the scaffolded bootstrap' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'lf')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'

            (Test-LambdaPSRuntimeProject -Path $dir | Where-Object RuleId -eq 'R-LineEndings').Passed | Should -BeTrue
        }
    }

    Context 'byte order mark (R-Encoding)' {
        It 'fails a bootstrap saved with a UTF-8 byte order mark, and still runs the other rules on its text' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'bom')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            $path = [System.IO.Path]::Combine($dir, 'bootstrap')
            [System.IO.File]::WriteAllText($path, [System.IO.File]::ReadAllText($path), [System.Text.UTF8Encoding]::new($true))
            @([System.IO.File]::ReadAllBytes($path)[0..2]) | Should -Be @(0xEF, 0xBB, 0xBF)

            $errors = @()
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -ErrorVariable errors -ErrorAction SilentlyContinue)
            $finding = $findings | Where-Object RuleId -eq 'R-Encoding'
            $finding.Passed | Should -BeFalse
            $finding.Severity | Should -Be 'Error'
            @($findings | Where-Object { -not $_.Passed }).RuleId | Should -Be @('R-Encoding')
            $errors[0].FullyQualifiedErrorId | Should -Be 'R-Encoding,Test-LambdaPSRuntimeProject'
            Test-LambdaPSRuntimeProject -Path $dir -Quiet | Should -BeFalse
        }

        It 'fails R-Encoding alone for a UTF-16 bootstrap, whose text the other rules still read' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'utf16')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            $path = [System.IO.Path]::Combine($dir, 'bootstrap')
            # What Windows PowerShell's Out-File and '>' write: UTF-16 LE with a byte order mark.
            [System.IO.File]::WriteAllText($path, [System.IO.File]::ReadAllText($path), [System.Text.UnicodeEncoding]::new($false, $true))

            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -ErrorAction SilentlyContinue)
            @($findings | Where-Object { -not $_.Passed }).RuleId | Should -Be @('R-Encoding')
            ($findings | Where-Object RuleId -eq 'R-Encoding').Message | Should -BeLike '*UTF-16 LE (FF FE)*'
        }

        It 'passes the scaffolded bootstrap' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'nobom')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'

            (Test-LambdaPSRuntimeProject -Path $dir | Where-Object RuleId -eq 'R-Encoding').Passed | Should -BeTrue
        }
    }

    Context 'error handling' {
        It 'reports a missing project directory as an error for that item' {
            $missing = [System.IO.Path]::Combine($TestDrive, 'does-not-exist')
            { Test-LambdaPSRuntimeProject -Path $missing -ErrorAction Stop } |
                Should -Throw -ErrorId 'ProjectNotFound,Test-LambdaPSRuntimeProject'
            Test-LambdaPSRuntimeProject -Path $missing -Quiet -ErrorAction Stop | Should -BeFalse
        }

        It 'reports a lambdaps-runtime.json that is not valid as an error for that item, or $false under -Quiet' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'badmarker')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'lambdaps-runtime.json')) -Value '{ not json'
            $good = [System.IO.Path]::Combine($TestDrive, 'goodmarker')
            $null = New-LambdaPSRuntimeProject -Path $good -PowerShellVersion '7.4'

            { Test-LambdaPSRuntimeProject -Path $dir -ErrorAction Stop } |
                Should -Throw -ErrorId 'InvalidProjectMarker,Test-LambdaPSRuntimeProject'
            Test-LambdaPSRuntimeProject -Path $dir -Quiet -ErrorAction Stop | Should -BeFalse
            @($dir, $good | Test-LambdaPSRuntimeProject -Quiet) | Should -Be @($false)
        }

        It 'reports a project with no bootstrap file as an error for that item' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'nobootstrap')
            $null = New-Item -ItemType Directory -Path $dir -Force
            { Test-LambdaPSRuntimeProject -Path $dir -ErrorAction Stop } |
                Should -Throw -ErrorId 'BootstrapNotFound,Test-LambdaPSRuntimeProject'
            Test-LambdaPSRuntimeProject -Path $dir -Quiet -ErrorAction Stop | Should -BeFalse

            # Non-terminating: a project piped after it is still checked.
            $good = [System.IO.Path]::Combine($TestDrive, 'after-nobootstrap')
            $null = New-LambdaPSRuntimeProject -Path $good -PowerShellVersion '7.4'
            $errors = @()
            $findings = @($dir, $good | Test-LambdaPSRuntimeProject -ErrorVariable errors -ErrorAction SilentlyContinue)
            @($errors.FullyQualifiedErrorId) | Should -Be @('BootstrapNotFound,Test-LambdaPSRuntimeProject')
            $findings.Count | Should -Be 7
            $findings | ForEach-Object { $_.Path | Should -Be $good }
        }
    }

    Context 'error TargetObject' {
        It 'is the project''s full path for <ErrorId>, given a relative path' -ForEach @(
            @{ ErrorId = 'ProjectNotFound'; Name = 'target-missing' }
            @{ ErrorId = 'BootstrapNotFound'; Name = 'target-nobootstrap' }
            @{ ErrorId = 'InvalidProjectMarker'; Name = 'target-badmarker-json' }
            @{ ErrorId = 'R-Env-Path'; Name = 'missingEnv' }
        ) {
            $full = [System.IO.Path]::Combine($TestDrive, $Name)
            if ($ErrorId -eq 'BootstrapNotFound') { $null = New-Item -ItemType Directory -Path $full -Force }
            if ($ErrorId -eq 'InvalidProjectMarker') {
                $null = New-LambdaPSRuntimeProject -Path $full -PowerShellVersion '7.4' -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($full, 'lambdaps-runtime.json')) -Value '{ not json'
            }
            Push-Location -LiteralPath $TestDrive
            try {
                $errors = @()
                $null = Test-LambdaPSRuntimeProject -Path "./$Name" -ErrorVariable errors -ErrorAction SilentlyContinue
            }
            finally { Pop-Location }

            $record = @($errors | Where-Object { $_.FullyQualifiedErrorId -eq "$ErrorId,Test-LambdaPSRuntimeProject" })[0]
            $record | Should -Not -BeNullOrEmpty
            $record.TargetObject | Should -Be $full
        }
    }

    # A file mode of 000 stops the owner reading it, except as root, which reads anything.
    Context 'unreadable bootstrap' -Skip:($IsWindows -or ((id -u) -eq '0')) {
        BeforeAll {
            $script:unreadable = [System.IO.Path]::Combine($TestDrive, 'unreadable')
            $null = New-LambdaPSRuntimeProject -Path $script:unreadable -PowerShellVersion '7.4'
            $script:readable = [System.IO.Path]::Combine($TestDrive, 'readable')
            $null = New-LambdaPSRuntimeProject -Path $script:readable -PowerShellVersion '7.4'
            $script:unreadableBootstrap = [System.IO.Path]::Combine($script:unreadable, 'bootstrap')
            [System.IO.File]::SetUnixFileMode($script:unreadableBootstrap, [System.IO.UnixFileMode]::None)
        }

        AfterAll {
            [System.IO.File]::SetUnixFileMode($script:unreadableBootstrap, [System.IO.UnixFileMode]'UserRead, UserWrite')
        }

        # The error stream (2>&1) holds what the cmdlet writes. -ErrorVariable would also hold the .NET
        # read exception the cmdlet catches, which PowerShell records but does not write.
        It 'reports BootstrapNotReadable for that project, targeting its full path, and checks the next one' {
            $output = @($script:unreadable, $script:readable | Test-LambdaPSRuntimeProject -ErrorAction Continue 2>&1)
            $errors = @($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] })
            $findings = @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] })

            $errors.Count | Should -Be 1
            $errors[0].FullyQualifiedErrorId | Should -Be 'BootstrapNotReadable,Test-LambdaPSRuntimeProject'
            $errors[0].TargetObject | Should -Be $script:unreadable
            @($findings.Path | Select-Object -Unique) | Should -Be @($script:readable)
        }

        It 'returns $false under -Quiet, and writes no error' {
            $output = @($script:unreadable, $script:readable | Test-LambdaPSRuntimeProject -Quiet 2>&1)
            $output | Should -Be @($false)
        }
    }

    Context 'the documented gate' {
        # Run as a top-level script in a child pwsh: inside a try block (as Should -Throw is), an
        # error that stops the call is caught instead of skipping the statement.
        It 'fails closed when the call stops, even after an earlier check passed' {
            $script = [System.IO.Path]::Combine($TestDrive, 'gate.ps1')
            Set-Content -LiteralPath $script -Value @(
                "Import-Module '$($script:modulePath)' -WarningAction SilentlyContinue"
                '$ok = $true'
                '$ok = $null'
                "`$ok = Test-LambdaPSRuntimeProject -Path '$TestDrive' -Target 'NoSuchTarget' -Quiet 2>`$null"
                'if ($ok -ne $true) { "GATE: tripped"; exit 3 }'
                '"GATE: passed"'
            )
            # '2>$null' in the script does not silence a binding error, so capture the child's error
            # stream too: the run stays quiet, and the test shows why the call stopped.
            $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -File $script 2>&1
            $LASTEXITCODE | Should -Be 3
            @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) | Should -Contain 'GATE: tripped'
            ($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | Out-String) -replace '\s+', ' ' |
                Should -BeLike "*Cannot validate argument on parameter 'Target'*"
        }

        It 'says a pass shows the scaffold''s shape, not that a hand-written bootstrap starts' {
            $help = (Get-Help Test-LambdaPSRuntimeProject -Full | Out-String) -replace '\s+', ' '
            $help | Should -BeLike '*A pass means the bootstrap has the shape the scaffolder writes. It does not prove that a hand-written bootstrap starts*Test a hand-written bootstrap by invoking the function.*'
        }

        It 'is the pattern the help shows' {
            $help = (Get-Help Test-LambdaPSRuntimeProject -Full | Out-String) -replace '\s+', ' '
            $help | Should -BeLike '*$ok = $null $ok = Test-LambdaPSRuntimeProject -Path ./rt -Quiet if ($ok -ne $true)*'
        }

        It 'lists the forms it does not evaluate' {
            $help = (Get-Help Test-LambdaPSRuntimeProject -Full | Out-String) -replace '\s+', ' '
            $help | Should -BeLike "*a '\' line continuation, an '&&' or '||' list*"
            $help | Should -BeLike '*makes each shell rule UNVERIFIED*'
        }
    }

    Context 'an unexpected failure under -Quiet' {
        It 'fails that project and keeps checking the next, instead of stopping' {
            $broken = [System.IO.Path]::Combine($TestDrive, 'quiet-broken')
            $null = New-LambdaPSRuntimeProject -Path $broken -PowerShellVersion '7.4'
            Add-Content -LiteralPath ([System.IO.Path]::Combine($broken, 'bootstrap')) -Value '# BROKEN'
            $good = [System.IO.Path]::Combine($TestDrive, 'quiet-good')
            $null = New-LambdaPSRuntimeProject -Path $good -PowerShellVersion '7.4'
            # The first rule runs for both projects; the second throws for the broken one only.
            Mock -ModuleName $script:moduleName Test-LambdaPSBootstrapRuntimePath {
                New-LambdaPSFinding -RuleId 'R-RuntimePath' -Passed $true -Message 'stub'
            }
            Mock -ModuleName $script:moduleName Test-LambdaPSBootstrapEnvironment { throw 'rule failed unexpectedly' } -ParameterFilter {
                $Bootstrap.Text.Contains('# BROKEN')
            }

            $ok = $broken, $good | Test-LambdaPSRuntimeProject -Quiet
            $ok | Should -BeFalse
            Should -Invoke -ModuleName $script:moduleName Test-LambdaPSBootstrapRuntimePath -Times 2 -Exactly
        }

        It 'still stops without -Quiet' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'loud-broken')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            Mock -ModuleName $script:moduleName Test-LambdaPSBootstrapEnvironment { throw 'rule failed unexpectedly' }
            { Test-LambdaPSRuntimeProject -Path $dir -ErrorAction SilentlyContinue } | Should -Throw '*rule failed unexpectedly*'
        }
    }

    # Valid Layer bootstrap forms the validator must accept.
    Context 'valid bootstrap forms' {
        BeforeAll {
            $script:newLayerProject = {
                param([string]$Name, [string]$Content)
                $dir = [System.IO.Path]::Combine($TestDrive, "form-$Name")
                $null = New-Item -ItemType Directory -Path $dir -Force
                [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, 'bootstrap'), $Content)
                $dir
            }
            $script:exec = 'exec /opt/powershell/pwsh -NoProfile -NonInteractive -File /opt/runtime.ps1'
            $script:invariant = 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1'
        }

        It 'passes every rule for <Case>' -ForEach @(
            @{ Case = 'an exec that only redirects stderr (exec 2>&1)'; Lines = @('exec 2>&1', '<invariant>', '<exec>') }
            @{ Case = 'an exec that redirects to a log (exec >/tmp/boot.log 2>&1)'; Lines = @('exec >/tmp/boot.log 2>&1', '<invariant>', '<exec>') }
            @{ Case = 'an exec that opens a descriptor (exec 3>&1)'; Lines = @('exec 3>&1', '<invariant>', '<exec>') }
            @{ Case = 'a bare redirect target (exec > /tmp/boot.log)'; Lines = @('exec > /tmp/boot.log', '<invariant>', '<exec>') }
            @{ Case = 'an exec of another program in an if block'; Lines = @('if [ "$1" = shell ]; then', '  exec /bin/sh', 'fi', '<invariant>', '<exec>') }
            @{ Case = 'an exec inside a here-doc body'; Lines = @("cat > /tmp/x.sh <<'EOF'", 'exec echo hi', 'EOF', '<invariant>', '<exec>') }
            @{ Case = 'an exec inside a tab-stripped here-doc body'; Lines = @('cat > /tmp/x.sh <<-EOF', "`texec echo hi", "`tEOF", '<invariant>', '<exec>') }
            @{ Case = 'the flag on a pwsh command run without exec'; Lines = @('export PATH=/opt/powershell:$PATH', 'export LD_LIBRARY_PATH=/opt/powershell', 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 pwsh -NoProfile -NonInteractive -File /opt/runtime.ps1') }
            @{ Case = 'a tee into /tmp, then a read of /opt'; Lines = @('<invariant>', 'echo "$(date)" | tee /tmp/start.log; ls /opt', '<exec>') }
            @{ Case = 'a tee into /tmp piped on'; Lines = @('<invariant>', 'echo x | tee /tmp/a | cat > /tmp/b', '<exec>') }
        ) {
            $content = "#!/bin/sh`n" + (($Lines | ForEach-Object { $_.Replace('<invariant>', $script:invariant).Replace('<exec>', $script:exec) }) -join "`n") + "`n"
            $dir = & $script:newLayerProject ($Case -replace '\W', '-') $content
            $failed = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -ErrorAction SilentlyContinue -WarningAction SilentlyContinue | Where-Object { -not $_.Passed })
            @($failed.RuleId) | Should -BeNullOrEmpty -Because $content
        }

        It 'still fails <Rule> for <Case>' -ForEach @(
            @{ Case = 'an exec that only redirects, with the export after the pwsh exec'; Rule = 'R-Globalization'; Lines = @('exec 2>&1', '<exec>', '<invariant>') }
            @{ Case = 'the flag set to 0 on a pwsh command run without exec'; Rule = 'R-Globalization'; Lines = @('DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0 /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a tee into /opt before a ;'; Rule = 'R-ReservedWrite'; Lines = @('<invariant>', 'echo x | tee /opt/start.log; ls /tmp', '<exec>') }
            @{ Case = 'the flag set to 0 through /usr/bin/env'; Rule = 'R-Globalization'; Lines = @('exec /usr/bin/env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0 /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'PATH exported empty, for a pwsh found on PATH'; Rule = 'R-Env-Path'; Lines = @('<invariant>', 'export PATH= LD_LIBRARY_PATH=', 'exec pwsh -File /opt/runtime.ps1') }
            @{ Case = 'LD_LIBRARY_PATH exported empty, for a pwsh found on PATH'; Rule = 'R-Env-LdLibraryPath'; Lines = @('<invariant>', 'export PATH=/opt/powershell LD_LIBRARY_PATH=', 'exec pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a mkdir under /opt'; Rule = 'R-ReservedWrite'; Lines = @('<invariant>', 'mkdir -p /opt/cache', '<exec>') }
        ) {
            $content = "#!/bin/sh`n" + (($Lines | ForEach-Object { $_.Replace('<invariant>', $script:invariant).Replace('<exec>', $script:exec) }) -join "`n") + "`n"
            $dir = & $script:newLayerProject ($Case -replace '\W', '-') $content
            $failed = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -ErrorAction SilentlyContinue -WarningAction SilentlyContinue | Where-Object { -not $_.Passed })
            @($failed.RuleId) | Should -Contain $Rule -Because $content
        }
    }

    # Shapes outside the known forms: never a wrong pass or failure, but UNVERIFIED.
    Context 'shapes the validator does not evaluate' {
        BeforeAll {
            $script:newShape = {
                param([string]$Name, [string[]]$Lines)
                $dir = [System.IO.Path]::Combine($TestDrive, "shape-$Name")
                $null = New-Item -ItemType Directory -Path $dir -Force
                [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, 'bootstrap'), "#!/bin/sh`n" + ($Lines -join "`n") + "`n")
                $dir
            }
        }

        It 'reports <Case> as unverified, not passed, for every shell rule' -ForEach @(
            @{ Case = 'a flag only on an earlier pwsh run'; Lines = @('DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /opt/powershell/pwsh -Command warmup', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a flag on an earlier pwsh, 0 on the exec'; Lines = @('DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /opt/powershell/pwsh -Command warmup', 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0 exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a flag inside a command substitution'; Lines = @('V=$( DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 pwsh -Version )', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a variable as the program, with the export after it'; Lines = @('export PATH=/opt/powershell:/usr/bin LD_LIBRARY_PATH=/opt/powershell', 'exec "$PWSH" -File /opt/runtime.ps1', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1') }
            @{ Case = 'a variable as the program, set through env'; Lines = @('PWSH=/opt/powershell/pwsh', 'exec env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 "$PWSH" -File /opt/runtime.ps1') }
            @{ Case = 'a pwsh exec inside a function'; Lines = @('start() {', '  exec /opt/powershell/pwsh -File /opt/runtime.ps1', '}', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'start') }
            @{ Case = 'an arithmetic shift'; Lines = @('BITS=$((1<<2))', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a tee in a subshell'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', '(echo x | tee /opt/x)', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an unknown command before the exec'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', '/opt/tools/prepare.sh', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'exec -a NAME, which only bash takes'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec -a pwsh /opt/powershell/pwsh -File /opt/runtime.ps1') }
            # Each part of a pipeline runs in a subshell, so an export there never reaches pwsh.
            @{ Case = 'an export after a pipe'; Lines = @('echo start | export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an export before a pipe'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 | cat', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a pipeline continued on the next line'; Lines = @('echo start |', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            # sh stops at these with a syntax error, before pwsh runs.
            @{ Case = 'a redirect with no target'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo started >', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an operator after an operator'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo a > > /tmp/x', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an empty pipeline element'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', '| echo hi', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an empty statement'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo a ;; echo b', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a quoted export assignment'; Lines = @('export "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1"', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
        ) {
            $dir = & $script:newShape ($Case -replace '\W', '-') $Lines
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -WarningVariable warnings -WarningAction SilentlyContinue)
            $shell = @($findings | Where-Object { $_.RuleId -in 'R-RuntimePath', 'R-Env-Path', 'R-Env-LdLibraryPath', 'R-Globalization', 'R-ReservedWrite' })
            $shell.Count | Should -Be 5
            $shell | ForEach-Object {
                $_.Unverified | Should -BeTrue -Because $_.RuleId
                $_.Severity | Should -Be 'Warning'
                $_.Message | Should -BeLike 'Not verified: the bootstrap uses *. This check does not evaluate that; check by hand that *'
            }
            @($warnings).Count | Should -Be 5
            Test-LambdaPSRuntimeProject -Path $dir -Target Layer -Quiet -WarningAction SilentlyContinue | Should -BeTrue
            Test-LambdaPSRuntimeProject -Path $dir -Target Layer -Quiet -UnverifiedAsFailure -WarningAction SilentlyContinue | Should -BeFalse
        }

        It 'reports an unknown command in a CMR bootstrap as unverified for its four shell rules, with R-Globalization not applicable' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'shape-cmr-unknown-command')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            $bootstrap = [System.IO.Path]::Combine($dir, 'bootstrap')
            [System.IO.File]::WriteAllText($bootstrap, ([System.IO.File]::ReadAllText($bootstrap) -replace '(?m)^exec ', "/opt/tools/prepare.sh`nexec "))

            $errors = @()
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -WarningVariable warnings -WarningAction SilentlyContinue -ErrorVariable errors)
            $errors | Should -BeNullOrEmpty
            $shellRules = 'R-RuntimePath', 'R-Env-Path', 'R-Env-LdLibraryPath', 'R-ReservedWrite'
            @($findings | Where-Object Unverified | ForEach-Object RuleId) | Should -Be $shellRules
            $findings | Where-Object Unverified | ForEach-Object {
                $_.Passed | Should -BeTrue -Because $_.RuleId
                $_.Severity | Should -Be 'Warning'
                $_.Message | Should -BeLike "Not verified: the bootstrap uses the command '/opt/tools/prepare.sh'. This check does not evaluate that; check by hand that *"
            }
            $globalization = $findings | Where-Object RuleId -eq 'R-Globalization'
            $globalization.Passed | Should -BeTrue
            $globalization.Unverified | Should -BeFalse
            $globalization.Message | Should -BeLike 'Not applicable:*'
            @($warnings).Count | Should -Be 4
            Test-LambdaPSRuntimeProject -Path $dir -Quiet -WarningAction SilentlyContinue | Should -BeTrue
            Test-LambdaPSRuntimeProject -Path $dir -Quiet -UnverifiedAsFailure -WarningAction SilentlyContinue | Should -BeFalse
        }

        It 'reports <Case> as unverified for every shell rule' -ForEach @(
            @{ Case = 'eval of a write into /opt'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', "eval 'echo x >/opt/log'", 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'sh -c with a write into /opt'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', "sh -c 'echo x >/opt/log'", 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a variable in a redirect target'; Lines = @('D=/opt', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo x > $D/log', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a variable in a tee target'; Lines = @('D=/opt', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo x | tee "$D/log"', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a variable as the flag value'; Lines = @('V=1', 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=$V', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'pwsh run through timeout'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'timeout 900 /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'exec env with no program'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec env A=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'unset after the export'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'unset DOTNET_SYSTEM_GLOBALIZATION_INVARIANT', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'exit before the exec'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exit 0', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'export with an option'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'export -n DOTNET_SYSTEM_GLOBALIZATION_INVARIANT', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
        ) {
            $dir = & $script:newShape ($Case -replace '\W', '-') $Lines
            $shell = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -WarningAction SilentlyContinue | Where-Object { $_.RuleId -in 'R-RuntimePath', 'R-Env-Path', 'R-Env-LdLibraryPath', 'R-Globalization', 'R-ReservedWrite' })
            $shell | ForEach-Object { $_.Unverified | Should -BeTrue -Because $_.RuleId }
        }

        It 'passes every rule, verified, for <Case>' -ForEach @(
            @{ Case = 'escaped quotes inside double quotes'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo "say \"hi\"" >/tmp/x', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'plain commands writing to /tmp'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'mkdir -p /tmp/run; cd /tmp/run', 'printf ok | tee -a /tmp/start.log', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a single-quoted $ in a write target'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', "echo x > '/tmp/`$name'", 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'PATH exported without an expansion'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 PATH=/opt/powershell:/usr/bin LD_LIBRARY_PATH=/opt/powershell', 'exec pwsh -File /opt/runtime.ps1') }
            @{ Case = 'the flag set through /usr/bin/env'; Lines = @('exec /usr/bin/env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'the flag set through /bin/env'; Lines = @('exec /bin/env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'exec --'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec -- /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an export, then a pipeline after a ;'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1; echo start | tee /tmp/start.log', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a redirect to a file, and a trailing ;'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo started > /tmp/start.log;', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'two statements on a line'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo a; echo b', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an export with the value quoted'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="1"', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
        ) {
            $dir = & $script:newShape ($Case -replace '\W', '-') $Lines
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -WarningVariable warnings)
            @($findings | Where-Object { -not $_.Passed -or $_.Unverified }).RuleId | Should -BeNullOrEmpty
            @($warnings) | Should -BeNullOrEmpty
        }

        It 'reports <Case> as unverified for every shell rule (narrowing)' -ForEach @(
            @{ Case = 'a $ in an exec env PATH'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec env PATH=$PATH pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a $ in an exec env flag'; Shebang = '#!/bin/sh'; Lines = @('INV=1', 'exec env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=$INV /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'printf -v'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'printf -v DOTNET_SYSTEM_GLOBALIZATION_INVARIANT 0', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a pwsh shebang over a shell body'; Shebang = '#!/opt/powershell/pwsh -NoProfile'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a python shebang'; Shebang = '#!/usr/bin/python3'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a dash shebang, which provided.al2023 does not have'; Shebang = '#!/usr/bin/dash -e'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'an env pwsh shebang'; Shebang = '#!/usr/bin/env pwsh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a no-break space between words'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', "exec$([char]0xA0)/opt/powershell/pwsh -File /opt/runtime.ps1") }
            @{ Case = 'a relative redirect after cd'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'cd /opt', 'exec >log 2>&1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a relative tee target'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo x | tee start.log', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, "narrow-$($Case -replace '\W', '-')")
            $null = New-Item -ItemType Directory -Path $dir -Force
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, 'bootstrap'), "$Shebang`n" + ($Lines -join "`n") + "`n")
            $shell = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -WarningAction SilentlyContinue | Where-Object { $_.RuleId -in 'R-RuntimePath', 'R-Env-Path', 'R-Env-LdLibraryPath', 'R-Globalization', 'R-ReservedWrite' })
            $shell.Count | Should -Be 5
            $shell | ForEach-Object { $_.Unverified | Should -BeTrue -Because $_.RuleId }
        }

        It 'passes every rule, verified, for <Case> (narrowing)' -ForEach @(
            @{ Case = 'a bash shebang'; Shebang = '#!/bin/bash'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'a /usr/bin/bash shebang'; Shebang = '#!/usr/bin/bash'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'tab-separated words'; Shebang = '#!/bin/sh'; Lines = @("export`tDOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1", "exec`t/opt/powershell/pwsh`t-File /opt/runtime.ps1") }
            @{ Case = 'printf without -v'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', "printf '%s\n' started >/tmp/log", 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
            @{ Case = 'exec env with literal values'; Shebang = '#!/bin/sh'; Lines = @('exec env DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 PATH=/opt/powershell:/usr/bin LD_LIBRARY_PATH=/opt/powershell pwsh -File /opt/runtime.ps1') }
            @{ Case = 'absolute redirects to /tmp'; Shebang = '#!/bin/sh'; Lines = @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'exec >/tmp/boot.log 2>&1', 'echo x | tee -a /tmp/start.log', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1') }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, "narrow-ok-$($Case -replace '\W', '-')")
            $null = New-Item -ItemType Directory -Path $dir -Force
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, 'bootstrap'), "$Shebang`n" + ($Lines -join "`n") + "`n")
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -WarningVariable warnings)
            @($findings | Where-Object { -not $_.Passed -or $_.Unverified }).RuleId | Should -BeNullOrEmpty
            @($warnings) | Should -BeNullOrEmpty
        }

        It 'still fails a clear violation in a known form: a tee into /opt' {
            $dir = & $script:newShape 'tee-opt' @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', 'echo x | tee /opt/x', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1')
            (Test-LambdaPSRuntimeProject -Path $dir -Target Layer -ErrorAction SilentlyContinue -WarningAction SilentlyContinue | Where-Object RuleId -eq 'R-ReservedWrite').Passed | Should -BeFalse
        }

        It 'names an unknown command once, and says only once that it is not evaluated' {
            $dir = & $script:newShape 'message-once' @('export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1', '/opt/tools/prepare.sh', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1')
            $finding = Test-LambdaPSRuntimeProject -Path $dir -Target Layer -WarningAction SilentlyContinue | Where-Object RuleId -eq 'R-Globalization'
            $finding.Message | Should -BeLike "Not verified: the bootstrap uses the command '/opt/tools/prepare.sh'. This check does not evaluate that; check by hand that *"
            ([regex]::Matches($finding.Message, 'evaluate')).Count | Should -Be 1
        }

        It 'fails an unverified rule under -UnverifiedAsFailure, on the error stream' {
            $dir = & $script:newShape 'as-failure' @('if true; then :; fi', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1')
            $errors = @()
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -UnverifiedAsFailure -ErrorVariable errors -ErrorAction SilentlyContinue -WarningAction SilentlyContinue)
            @($findings | Where-Object { $_.Unverified -and -not $_.Passed }).Count | Should -Be 5
            @($errors | Where-Object { $_.FullyQualifiedErrorId -like 'R-Globalization,*' }).Count | Should -Be 1
        }

        It 'checks the byte rules even when the shell rules are unverified' {
            $dir = & $script:newShape 'bytes' @('if true; then :; fi', 'exec /opt/powershell/pwsh -File /opt/runtime.ps1')
            $path = [System.IO.Path]::Combine($dir, 'bootstrap')
            [System.IO.File]::WriteAllText($path, [System.IO.File]::ReadAllText($path).Replace("`n", "`r`n"))
            $finding = Test-LambdaPSRuntimeProject -Path $dir -Target Layer -ErrorAction SilentlyContinue -WarningAction SilentlyContinue | Where-Object RuleId -eq 'R-LineEndings'
            $finding.Passed | Should -BeFalse
            $finding.Unverified | Should -BeFalse
        }

        It 'reports every scaffolded bootstrap as verified and passing (<Target>)' -ForEach @(
            @{ Target = 'CMR' }, @{ Target = 'Layer' }, @{ Target = 'SelfContainedZip' }, @{ Target = 'Container' }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, "verified-$Target")
            if ($Target -in 'CMR', 'Layer') { $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4' -Target $Target }
            else { $null = New-LambdaPSFunctionProject -Path $dir -Handler 'handler' -PowerShellVersion '7.4' -Target $Target }
            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -WarningVariable warnings)
            @($findings.RuleId) | Should -Be @('R-RuntimePath', 'R-Env-Path', 'R-Env-LdLibraryPath', 'R-ReservedWrite', 'R-Globalization', 'R-LineEndings', 'R-Encoding')
            @($findings | Where-Object { -not $_.Passed -or $_.Unverified }) | Should -BeNullOrEmpty
            @($warnings) | Should -BeNullOrEmpty
        }
    }

    Context 'what the bootstrap is' {
        It 'reports a file passed as the project as ProjectNotFound, saying it is a file' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'file-as-project')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            $file = [System.IO.Path]::Combine($dir, 'bootstrap')
            { Test-LambdaPSRuntimeProject -Path $file -ErrorAction Stop } |
                Should -Throw -ErrorId 'ProjectNotFound,Test-LambdaPSRuntimeProject' -ExpectedMessage '*is a file, not a project directory.'
        }

        It 'reports a FIFO bootstrap as BootstrapNotReadable without blocking' -Skip:$IsWindows {
            $dir = [System.IO.Path]::Combine($TestDrive, 'fifo-bootstrap')
            $null = New-Item -ItemType Directory -Path $dir -Force
            & mkfifo ([System.IO.Path]::Combine($dir, 'bootstrap'))
            # A separate process, so a read that blocks can be stopped.
            $modulePath = $script:modulePath
            $job = Start-Job -ScriptBlock {
                Import-Module $using:modulePath -WarningAction SilentlyContinue
                $jobErrors = @()
                $quiet = Test-LambdaPSRuntimeProject -Path $using:dir -Quiet
                $null = Test-LambdaPSRuntimeProject -Path $using:dir -ErrorAction SilentlyContinue -ErrorVariable jobErrors
                @{ Quiet = $quiet; ErrorId = $jobErrors[-1].FullyQualifiedErrorId }
            }
            $done = Wait-Job -Job $job -Timeout 60
            $result = if ($done) { Receive-Job -Job $job }
            Remove-Job -Job $job -Force
            $done | Should -Not -BeNullOrEmpty -Because 'reading a FIFO must not wait for a writer'
            $result.Quiet | Should -BeFalse
            $result.ErrorId | Should -Be 'BootstrapNotReadable,Test-LambdaPSRuntimeProject'
        }

        It 'reports a link loop as BootstrapNotReadable for that project, and checks the next one' -Skip:$IsWindows {
            $loop = [System.IO.Path]::Combine($TestDrive, 'loop-bootstrap')
            $null = New-Item -ItemType Directory -Path $loop -Force
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($loop, 'bootstrap')) -Target 'bootstrap'
            $good = [System.IO.Path]::Combine($TestDrive, 'loop-next')
            $null = New-LambdaPSRuntimeProject -Path $good -PowerShellVersion '7.4'

            $output = @($loop, $good | Test-LambdaPSRuntimeProject -ErrorAction Continue 2>&1)
            $errors = @($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] })
            $errors.FullyQualifiedErrorId | Should -Be @('BootstrapNotReadable,Test-LambdaPSRuntimeProject')
            $errors[0].TargetObject | Should -Be $loop
            @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object Path | Select-Object -Unique) | Should -Be @($good)
        }

        It 'reports a link to nothing as BootstrapNotFound' -Skip:$IsWindows {
            $dir = [System.IO.Path]::Combine($TestDrive, 'dangling-bootstrap')
            $null = New-Item -ItemType Directory -Path $dir -Force
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($dir, 'bootstrap')) -Target ([System.IO.Path]::Combine($dir, 'missing'))
            { Test-LambdaPSRuntimeProject -Path $dir -ErrorAction Stop } |
                Should -Throw -ErrorId 'BootstrapNotFound,Test-LambdaPSRuntimeProject'
        }
    }

    Context 'where pwsh runs from' {
        # The scaffolded image removes the pwsh the package installs, so only the target's own copy runs.
        It 'fails R-RuntimePath for a <Target> bootstrap that runs <Program>' -ForEach @(
            @{ Target = 'CMR'; Program = '/usr/bin/pwsh'; Script = '/var/runtime/runtime.ps1' }
            @{ Target = 'CMR'; Program = '/opt/microsoft/powershell/7/pwsh'; Script = '/var/runtime/runtime.ps1' }
            @{ Target = 'Layer'; Program = '/var/task/powershell/pwsh'; Script = '/opt/runtime.ps1' }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, "outside-root-$Target-$($Program -replace '\W', '-')")
            $null = New-Item -ItemType Directory -Path $dir -Force
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, 'bootstrap'), "#!/bin/sh`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec $Program -NoProfile -NonInteractive -File $Script`n")
            $finding = Test-LambdaPSRuntimeProject -Path $dir -Target $Target -ErrorAction SilentlyContinue | Where-Object RuleId -eq 'R-RuntimePath'
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike "Bootstrap runs pwsh from '$Program', which is outside *"
        }

        It 'passes R-RuntimePath for a <Target> bootstrap that runs <Program>' -ForEach @(
            @{ Target = 'CMR'; Program = '/var/runtime/powershell/pwsh'; Script = '/var/runtime/runtime.ps1' }
            @{ Target = 'Layer'; Program = '/opt/powershell/pwsh'; Script = '/opt/runtime.ps1' }
            @{ Target = 'SelfContainedZip'; Program = '/var/task/powershell/pwsh'; Script = '/var/task/runtime.ps1' }
            @{ Target = 'CMR'; Program = 'pwsh'; Script = '/var/runtime/runtime.ps1' }
        ) {
            $dir = [System.IO.Path]::Combine($TestDrive, "inside-root-$Target-$($Program -replace '\W', '-')")
            $null = New-Item -ItemType Directory -Path $dir -Force
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, 'bootstrap'), "#!/bin/sh`nexport DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`nexec $Program -NoProfile -NonInteractive -File $Script`n")
            (Test-LambdaPSRuntimeProject -Path $dir -Target $Target -ErrorAction SilentlyContinue | Where-Object RuleId -eq 'R-RuntimePath').Passed | Should -BeTrue
        }
    }

    Context '-Target and the marker' {
        It 'lints against an explicit -Target rather than the marker' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'target-override')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4' -Target Layer

            $finding = Test-LambdaPSRuntimeProject -Path $dir -Target CMR -ErrorAction SilentlyContinue |
                Where-Object RuleId -eq 'R-RuntimePath'
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike 'Bootstrap does not reference /var/runtime.*'
        }

        It 'fails R-RuntimePath for a SelfContainedZip project linted as -Target Layer, whose bootstrap does not reference /opt' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'target-scz-as-layer')
            $null = New-LambdaPSFunctionProject -Path $dir -Handler handler -Target SelfContainedZip -PowerShellVersion '7.4'

            $findings = @(Test-LambdaPSRuntimeProject -Path $dir -Target Layer -ErrorAction SilentlyContinue)
            $finding = $findings | Where-Object RuleId -eq 'R-RuntimePath'
            $finding.Passed | Should -BeFalse
            $finding.Message | Should -BeLike 'Bootstrap does not reference /opt.*'
            Test-LambdaPSRuntimeProject -Path $dir -Quiet | Should -BeTrue -Because 'its own marker says SelfContainedZip'
        }

        It 'says under -Verbose that a project with no marker is checked as CMR' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'target-no-marker')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4' -Target Layer
            Remove-Item -LiteralPath ([System.IO.Path]::Combine($dir, 'lambdaps-runtime.json'))
            $verbose = @(Test-LambdaPSRuntimeProject -Path $dir -Quiet -Verbose 4>&1 | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] })
            ($verbose.Message -join ' ') | Should -BeLike '*has no lambdaps-runtime.json naming a target, so it is checked as a CMR project.*'
        }

        It 'does not read a marker that is not valid when -Target is given' {
            $dir = [System.IO.Path]::Combine($TestDrive, 'target-badmarker')
            $null = New-LambdaPSRuntimeProject -Path $dir -PowerShellVersion '7.4'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'lambdaps-runtime.json')) -Value '{ not json'

            Test-LambdaPSRuntimeProject -Path $dir -Target CMR -Quiet -ErrorAction Stop | Should -BeTrue
        }
    }

    Context 'pipeline input' {
        BeforeAll {
            $script:pipeA = [System.IO.Path]::Combine($TestDrive, 'pipe-a')
            $script:pipeB = [System.IO.Path]::Combine($TestDrive, 'pipe-b')
            $null = New-LambdaPSRuntimeProject -Path $script:pipeA -PowerShellVersion '7.4'
            $null = New-LambdaPSRuntimeProject -Path $script:pipeB -PowerShellVersion '7.4' -Target Layer
        }

        It 'binds a plain path string' {
            $script:pipeA | Test-LambdaPSRuntimeProject -Quiet | Should -BeTrue
        }

        It 'binds objects with a Path property' {
            @([pscustomobject]@{ Path = $script:pipeA }, [pscustomobject]@{ Path = $script:pipeB } | Test-LambdaPSRuntimeProject).Count | Should -Be 14
        }

        It 'returns ONE boolean under -Quiet for several piped projects, $true only if all passed' {
            $results = @($script:pipeA, $script:pipeB | Test-LambdaPSRuntimeProject -Quiet)
            $results | Should -Be @($true)

            $results = @($script:pipeA, $script:fixturePath['missingEnv'], $script:pipeB | Test-LambdaPSRuntimeProject -Quiet)
            $results | Should -Be @($false)
        }

        It 'returns $false under -Quiet for an empty pipeline' {
            @(@() | Test-LambdaPSRuntimeProject -Quiet) | Should -Be @($false)
        }

        # A blank path is this project's ProjectNotFound, not a binding error that would drop it.
        It 'reports <Case> as ProjectNotFound, and counts it under -Quiet' -ForEach @(
            @{ Case = 'a $null item'; Item = $null }
            @{ Case = 'an empty string'; Item = '' }
            @{ Case = 'a result with an empty ProjectPath'; Item = [PSCustomObject]@{ ProjectPath = '' } }
        ) {
            $output = @(@($Item, $script:pipeA) | Test-LambdaPSRuntimeProject -Quiet 2>&1)
            $output | Should -Be @($false) -Because 'the blank item fails, although the other project passes, and -Quiet writes no error'

            $errors = @()
            $null = @($Item) | Test-LambdaPSRuntimeProject -ErrorVariable errors -ErrorAction SilentlyContinue
            $errors.Count | Should -Be 1
            $errors[0].FullyQualifiedErrorId | Should -Be 'ProjectNotFound,Test-LambdaPSRuntimeProject'
        }

        It 'reports a blank -Path as ProjectNotFound' {
            { Test-LambdaPSRuntimeProject -Path '' -ErrorAction Stop } |
                Should -Throw -ErrorId 'ProjectNotFound,Test-LambdaPSRuntimeProject' -ExpectedMessage '*the path is empty*'
        }

        It 'trips the documented gate when any piped project fails' {
            $gate = if (-not ($script:pipeA, $script:fixturePath['missingEnv'] | Test-LambdaPSRuntimeProject -Quiet)) { 'tripped' } else { 'passed' }
            $gate | Should -Be 'tripped'
        }

        It 'keeps checking the projects after a missing one' {
            $missing = [System.IO.Path]::Combine($TestDrive, 'pipe-missing')
            $errors = @()
            $results = @($script:pipeA, $missing, $script:pipeB | Test-LambdaPSRuntimeProject -Quiet -ErrorVariable errors)
            $results | Should -Be @($false)
            $errors | Should -BeNullOrEmpty

            $findings = @($script:pipeA, $missing, $script:pipeB | Test-LambdaPSRuntimeProject -ErrorVariable errors -ErrorAction SilentlyContinue)
            @($findings | Where-Object RuleId -eq 'R-LineEndings').Count | Should -Be 2
            $errors[0].FullyQualifiedErrorId | Should -Be 'ProjectNotFound,Test-LambdaPSRuntimeProject'
        }

        It 'sets each finding''s Path to its own project''s full path' {
            $findings = @($script:pipeA, $script:pipeB | Test-LambdaPSRuntimeProject)
            $findings.Count | Should -BeGreaterThan 0
            $byPath = $findings | Group-Object Path
            @($byPath.Name) | Should -Be @($script:pipeA, $script:pipeB)
            $byPath[0].Count | Should -Be $byPath[1].Count
            @($byPath[0].Group.RuleId) | Should -Be @($byPath[1].Group.RuleId)
        }

        It 'checks piped projects of every non-CMR target against each one''s own marker, and reports a function project with no bootstrap without stopping' {
            $root = [System.IO.Path]::Combine($TestDrive, 'pipe-targets')
            $layer = [System.IO.Path]::Combine($root, 'a-layer')
            $selfContained = [System.IO.Path]::Combine($root, 'b-scz')
            $container = [System.IO.Path]::Combine($root, 'c-ctr')
            $function = [System.IO.Path]::Combine($root, 'd-fn')
            $null = New-LambdaPSRuntimeProject -Path $layer -PowerShellVersion '7.4' -Target Layer
            $null = New-LambdaPSFunctionProject -Path $selfContained -Handler handler -Target SelfContainedZip -PowerShellVersion '7.4'
            $null = New-LambdaPSFunctionProject -Path $container -Handler handler -Target Container -PowerShellVersion '7.4'
            $null = New-LambdaPSFunctionProject -Path $function -Handler handler
            $bootstrap = [System.IO.Path]::Combine($selfContained, 'bootstrap')
            [System.IO.File]::WriteAllText($bootstrap, ([System.IO.File]::ReadAllText($bootstrap) -replace '(?m)^export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1\n', ''))

            $errors = @()
            $findings = @(Get-ChildItem -LiteralPath $root -Directory | Sort-Object Name |
                    Test-LambdaPSRuntimeProject -ErrorVariable errors -ErrorAction SilentlyContinue)
            $findings.Count | Should -Be 21
            $byPath = @($findings | Group-Object Path)
            @($byPath.Name) | Should -Be @($layer, $selfContained, $container)
            $byPath | ForEach-Object { $_.Count | Should -Be 7 -Because $_.Name }
            $failed = @($findings | Where-Object { -not $_.Passed })
            @($failed | ForEach-Object { "$($_.RuleId) $($_.Path)" }) | Should -Be @("R-Globalization $selfContained")
            @($errors | ForEach-Object { "$($_.FullyQualifiedErrorId) $($_.TargetObject)" }) |
                Should -Be @("R-Globalization,Test-LambdaPSRuntimeProject $selfContained", "BootstrapNotFound,Test-LambdaPSRuntimeProject $function")
        }

        It 'reports a path on another provider as ProjectNotFound for that item, and checks the next project' {
            $errors = @()
            $findings = @('Env:', $script:pipeA | Test-LambdaPSRuntimeProject -ErrorVariable errors -ErrorAction SilentlyContinue)
            @($errors.FullyQualifiedErrorId) | Should -Be @('ProjectNotFound,Test-LambdaPSRuntimeProject')
            $errors[0].TargetObject | Should -Be 'Env:'
            $errors[0].Exception.Message | Should -BeLike '*provider*'
            $findings.Count | Should -Be 7
            $findings | ForEach-Object { $_.Path | Should -Be $script:pipeA }
            @('Env:', $script:pipeA | Test-LambdaPSRuntimeProject -Quiet) | Should -Be @($false)
        }

        It 'sets Path to the full path when the project is given relative to the current location' {
            Push-Location -LiteralPath $TestDrive
            try { $findings = @(Test-LambdaPSRuntimeProject -Path ./pipe-a) }
            finally { Pop-Location }
            $findings | ForEach-Object { $_.Path | Should -Be $script:pipeA }
        }
    }
}

# Write-Error inside a function leaves the caller's $? True, so the cmdlet uses $PSCmdlet.WriteError
# and the documented '$?' check sees a failing rule.
Describe 'Test-LambdaPSRuntimeProject sets $?' -Tag 'Unit' {
    It 'sets $? to False after a failing rule' {
        $null = Test-LambdaPSRuntimeProject -Path $script:fixturePath['missingEnv'] -ErrorAction SilentlyContinue
        $? | Should -BeFalse
    }
}
