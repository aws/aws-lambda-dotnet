BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
    $script:arm64Cases = @($script:pwshConfig.DefaultPatch.GetEnumerator() | ForEach-Object {
            @{ PowerShellVersion = $_.Key; Pin = $_.Value; Hash = $script:pwshConfig.ArchiveSha256[$_.Value].arm64 }
        })
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PwshConfig.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSRuntimeProject' -Tag 'Unit' {
    It 'scaffolds the complete runtime-image tree from the template' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-happy')
        $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'

        $written | Should -Not -BeNullOrEmpty
        $expected = @(
            'Dockerfile'
            'bootstrap'
            'PowerShellLambdaContext.cs'
            'lambdaps-runtime.json'
            [System.IO.Path]::Combine('modules', 'pwsh-runtime.psd1')
            [System.IO.Path]::Combine('modules', 'pwsh-runtime.psm1')
            [System.IO.Path]::Combine('modules', 'Private', 'Set-PSModulePath.ps1')
        )
        foreach ($relative in $expected) {
            Test-Path -LiteralPath ([System.IO.Path]::Combine($target, $relative)) |
                Should -BeTrue -Because "$relative must be scaffolded"
        }
    }

    It 'writes a lambdaps-runtime.json marker recording the CMR target and version' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-marker-cmr')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $marker = Get-Content -LiteralPath (
            [System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json
        $marker.Target | Should -Be 'CMR'
        $marker.PowerShellVersion | Should -Be '7.4'
        $marker.PwshVersion | Should -Be $script:pwshConfig.DefaultPatch['7.4']
    }

    It 'writes a Dockerfile that installs pwsh from the Microsoft RPM on amd64 and the verified tarball on arm64' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-rpm')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match 'provided:al2023'
        # packages.microsoft.com has no aarch64 pwsh RPM, so the install branches on BuildKit's TARGETARCH.
        $dockerfile | Should -Match 'ARG TARGETARCH'
        $dockerfile | Should -Match 'if \[ "\$TARGETARCH" = "arm64" \]'
        $dockerfile | Should -Match ([regex]::Escape("`"$($script:pwshConfig.ReleaseUrl)/v`${PWSH_VERSION}/powershell-`${PWSH_VERSION}-linux-arm64.tar.gz`""))
        $dockerfile | Should -Not -Match '__PWSH_RELEASE_URL__'
        # Both curls (the tarball and the Microsoft repo file) retry with the module's download limits.
        $retry = "--retry $($script:pwshConfig.Download.RetryCount) --retry-delay $($script:pwshConfig.Download.RetryDelaySeconds) " +
            "--connect-timeout $($script:pwshConfig.Download.ConnectTimeoutSeconds) "
        ([regex]::Matches($dockerfile, [regex]::Escape($retry))).Count | Should -Be 2
        $dockerfile | Should -Not -Match '__DOWNLOAD_RETRY_'
        $dockerfile | Should -Match 'sha256sum -c'
        # amd64 keeps the GPG-signed RPM path.
        $dockerfile | Should -Match 'packages\.microsoft\.com'
        $dockerfile | Should -Match 'dnf install -y "powershell-\$\{PWSH_VERSION\}"'
    }

    It 'bakes the pinned arm64 tarball SHA256 into the Dockerfile and leaves no token' -ForEach $script:arm64Cases {
        $target = [System.IO.Path]::Combine($TestDrive, "rt-arm64-$Pin")
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion $PowerShellVersion -PwshVersion $Pin -WarningVariable warnings
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match "ARG PWSH_ARM64_SHA256=$Hash"
        $dockerfile | Should -Not -Match '__PWSH_ARM64_SHA256__'
        $warnings | Should -BeNullOrEmpty
    }

    It 'keeps the arm64 hash token and warns when no hash is pinned for the version' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-arm64-unpinned')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -PwshVersion '7.4.2' -WarningVariable warnings -WarningAction SilentlyContinue
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        # The token stays so an arm64 build fails closed at the Dockerfile's hash check; amd64 is unaffected.
        $dockerfile | Should -Match 'ARG PWSH_ARM64_SHA256=__PWSH_ARM64_SHA256__'
        $dockerfile | Should -Match '(?m)^ARG PWSH_VERSION=7\.4\.2$' -Because 'the default 7.4 pin also starts with 7.4.2'
        @($warnings).Count | Should -Be 1
        "$($warnings[0])" | Should -Match 'No SHA256 is pinned for the pwsh 7\.4\.2 arm64 tarball'
    }

    It 'substitutes the resolved full pwsh version into the Dockerfile token' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-version')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Not -Match '__PWSH_VERSION__'
        $dockerfile | Should -Match "ARG PWSH_VERSION=$([regex]::Escape($script:pwshConfig.DefaultPatch['7.4']))"
    }

    It 'substitutes the default pwsh patch for PowerShell <PowerShellVersion>' -ForEach $script:arm64Cases {
        $target = [System.IO.Path]::Combine($TestDrive, "rt-default-$PowerShellVersion")
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion $PowerShellVersion
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match "(?m)^ARG PWSH_VERSION=$([regex]::Escape($Pin))$"
    }

    It 'honors an explicit -PwshVersion override' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-pin')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -PwshVersion '7.4.2' -WarningAction SilentlyContinue
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match '(?m)^ARG PWSH_VERSION=7\.4\.2$' -Because 'the default 7.4 pin also starts with 7.4.2'
    }

    It 'throws UnknownPwshVersion for an unmapped major.minor with no override' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-unknown')
        { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.9' } |
            Should -Throw -ErrorId 'UnknownPwshVersion,New-LambdaPSRuntimeProject'
        $target | Should -Not -Exist
    }

    It 'scaffolds an unmapped major.minor when -PwshVersion is supplied' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-unknown-ok')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.9' -PwshVersion '7.9.1' -WarningAction SilentlyContinue
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match 'ARG PWSH_VERSION=7\.9\.1'
    }

    It 'writes a Dockerfile with the precompile step and interpreter relocation' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-precompile')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match 'OutputAssembly'
        $dockerfile | Should -Match 'PowerShellLambdaContext\.dll'
        $dockerfile | Should -Match '/var/runtime/powershell'
    }

    It 'installs and relocates pwsh in one RUN, so no layer keeps the /opt copy' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-one-layer')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $runs = @([regex]::Matches($dockerfile, '(?ms)^RUN .*?(?<!\\)$\n(?!\s)').Value)
        $install = @($runs | Where-Object { $_ -match 'dnf install' })
        $install.Count | Should -Be 1
        $install[0] | Should -Match 'rm -rf /opt/microsoft'
    }

    # Runs the Dockerfile's own sed line (GNU sed, as in the image) on a copy of the bootstrap.
    It 'strips a leading UTF-8 byte order mark and CRLF from the bootstrap in the image' -Skip:(-not $IsLinux) {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-crlf')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $run = [regex]::Match($dockerfile, '(?m)^RUN (sed -i .*) /var/runtime/bootstrap$')
        $run.Success | Should -BeTrue -Because 'the Dockerfile repairs the bootstrap with sed'

        $original = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($target, 'bootstrap'))
        $copy = [System.IO.Path]::Combine($TestDrive, 'rt-crlf-bootstrap')
        [System.IO.File]::WriteAllText($copy, $original.Replace("`n", "`r`n"), [System.Text.UTF8Encoding]::new($true))
        & /bin/sh -c "$($run.Groups[1].Value) '$copy'"
        $LASTEXITCODE | Should -Be 0

        $bytes = [System.IO.File]::ReadAllBytes($copy)
        [System.Text.Encoding]::ASCII.GetString($bytes, 0, 2) | Should -Be '#!'
        [System.Text.Encoding]::UTF8.GetString($bytes) | Should -BeExactly $original
    }

    # Every target ships the one runtime interface client under Templates/Shared; its behavior is
    # tested in Component.RuntimeInterfaceClient.Tests.ps1. Here: each scaffold carries all of it,
    # unchanged.
    It 'scaffolds every Templates/Shared file unchanged for <Target>' -ForEach @(
        @{ Target = 'CMR'; Scaffold = 'Runtime' }
        @{ Target = 'Layer'; Scaffold = 'Runtime' }
        @{ Target = 'SelfContainedZip'; Scaffold = 'Function' }
        @{ Target = 'Container'; Scaffold = 'Function' }
    ) {
        $projectPath = [System.IO.Path]::Combine($TestDrive, "rt-shared-$Target")
        if ($Scaffold -eq 'Runtime') {
            $null = New-LambdaPSRuntimeProject -Path $projectPath -PowerShellVersion '7.4' -Target $Target
        }
        else {
            $null = New-LambdaPSFunctionProject -Path $projectPath -Handler handler -PowerShellVersion '7.4' -Target $Target
        }

        $shared = [System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'Templates', 'Shared')
        # A '<name>.append' file is not shipped itself: its text ends the target's own <name>.
        $files = @(Get-ChildItem -LiteralPath $shared -Recurse -File | Where-Object Extension -ne '.append')
        $files.Count | Should -BeGreaterThan 0
        foreach ($file in $files) {
            $relative = [System.IO.Path]::GetRelativePath($shared, $file.FullName)
            $emitted = [System.IO.Path]::Combine($projectPath, $relative)
            $emitted | Should -Exist -Because "every target ships $relative"
            [System.IO.File]::ReadAllText($emitted) | Should -BeExactly ([System.IO.File]::ReadAllText($file.FullName)) -Because "$relative is copied unchanged"
        }
        foreach ($append in @(Get-ChildItem -LiteralPath $shared -Recurse -File -Filter '*.append')) {
            $relative = [System.IO.Path]::GetRelativePath($shared, $append.FullName) -replace '\.append$', ''
            $emitted = [System.IO.Path]::Combine($projectPath, $relative)
            [System.IO.File]::ReadAllText($emitted).EndsWith([System.IO.File]::ReadAllText($append.FullName), [System.StringComparison]::Ordinal) |
                Should -BeTrue -Because "every target's $relative ends with Templates/Shared/$($append.Name)"
            [System.IO.File]::ReadAllText($emitted) | Should -Match "\S\n\n$([regex]::Escape([System.IO.File]::ReadAllText($append.FullName)))\z" -Because 'one blank line separates the appended section'
            "$emitted.append" | Should -Not -Exist
        }
    }

    # The scaffold test above cannot catch a path that is spelled right but does not exist. The directory pwsh ships is 'Modules' with a capital M, and Lambda runs on
    # case-sensitive Linux; because Set-PSModulePath ASSIGNS rather than appends, a lowercase
    # '.../powershell/modules' would also drop $PSHOME/Modules entirely, so no module shipped with
    # pwsh (Microsoft.PowerShell.Archive, ThreadJob, CimCmdlets) would load. A hello-world handler
    # still works in that case, so only resolving the path catches it.
    # This test EXECUTES the emitted function and requires the tier to resolve to a real directory.
    # Skipped on Windows: the emitted Set-PSModulePath is Lambda-side code that separates tiers with
    # ':', which is also what follows a Windows drive letter, so the entries cannot be split back
    # apart on this host. The scaffold test above still checks the emitted file byte for byte there.
    Context 'the pwsh-shipped module tier actually resolves' -Skip:$IsWindows {
        It 'ends PSModulePath at an existing directory for <Target>' -ForEach @(
            @{ Target = 'CMR'; Scaffold = 'Runtime' }
            @{ Target = 'Layer'; Scaffold = 'Runtime' }
            @{ Target = 'SelfContainedZip'; Scaffold = 'Function' }
            @{ Target = 'Container'; Scaffold = 'Function' }
        ) {
            # NOTE: not $target -- PowerShell variable names are case-insensitive, so a local $target
            # would overwrite the -ForEach $Target and every case would scaffold the same thing.
            $projectPath = [System.IO.Path]::Combine($TestDrive, "rt-pshome-$Target")
            if ($Scaffold -eq 'Runtime') {
                $null = New-LambdaPSRuntimeProject -Path $projectPath -PowerShellVersion '7.4' -Target $Target
            }
            else {
                $null = New-LambdaPSFunctionProject -Path $projectPath -Handler handler -PowerShellVersion '7.4' -Target $Target
            }

            $savedModulePath = $env:PSModulePath
            $savedTaskRoot = $env:LAMBDA_TASK_ROOT
            try {
                $env:LAMBDA_TASK_ROOT = '/var/task'
                . ([System.IO.Path]::Combine($projectPath, 'modules', 'Private', 'Set-PSModulePath.ps1'))
                Set-PSModulePath

                $entries = $env:PSModulePath -split ':'
                $last = $entries[-1]

                # Spelled from $PSHOME, so it is correct for whichever interpreter is running and
                # cannot drift per target.
                $last | Should -Be ([System.IO.Path]::Combine($PSHOME, 'Modules'))
                Test-Path -LiteralPath $last | Should -BeTrue -Because 'a tier that resolves to nothing silently hides every pwsh-shipped module'
                $last | Should -Not -Match 'powershell/modules$'
            }
            finally {
                $env:PSModulePath = $savedModulePath
                $env:LAMBDA_TASK_ROOT = $savedTaskRoot
            }
        }
    }

    It 'throws on an existing non-empty directory without -Force' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-exists')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $before = @(Get-ChildItem -LiteralPath $target -Recurse -Force).Count
        { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' } |
            Should -Throw -ErrorId 'TargetNotEmpty,New-LambdaPSRuntimeProject'
        @(Get-ChildItem -LiteralPath $target -Recurse -Force).Count | Should -Be $before
    }

    It 'scaffolds into an existing empty directory without -Force' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-empty')
        $null = New-Item -ItemType Directory -Path $target
        { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' } | Should -Not -Throw
        [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Exist
    }

    It 'overwrites an existing directory when -Force is supplied' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-force')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
        $bootstrap = [System.IO.Path]::Combine($target, 'bootstrap')
        $original = [System.IO.File]::ReadAllText($bootstrap)
        [System.IO.File]::WriteAllText($bootstrap, 'edited')

        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Force

        [System.IO.File]::ReadAllText($bootstrap) | Should -BeExactly $original
    }

    # -Force REPLACES the target rather than merging into it. Merging produced a hybrid project: a CMR
    # scaffold re-scaffolded as '-Target Layer -Force' kept the CMR Dockerfile, so
    # Build-LambdaPSRuntimeImage found a Dockerfile in a Layer project and built an image whose
    # bootstrap execs /opt/powershell/pwsh -- absent from that image. Worse, the validator passed the
    # hybrid green, because the marker and the bootstrap were both consistently Layer.
    Context '-Force replaces rather than merges' {
        It 'removes a previous target-specific file that the new target does not emit' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-crosstarget')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Exist

            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer -Force

            # The CMR Dockerfile must be gone, and the bootstrap must be the Layer's.
            [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Not -Exist
            Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'bootstrap')) -Raw | Should -Match '/opt/runtime\.ps1'
        }

        It 'removes a stray file the customer left in the target' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-stray')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            $stray = [System.IO.Path]::Combine($target, 'stale-artifact.txt')
            [System.IO.File]::WriteAllText($stray, 'x')

            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Force

            $stray | Should -Not -Exist
        }

        It 'replaces a Layer project with a CMR one that lints clean' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-layer-to-cmr')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer

            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target CMR -Force

            [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Exist
            Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'bootstrap')) -Raw | Should -Match '/var/runtime'
            (Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).Target | Should -Be 'CMR'
            Test-LambdaPSRuntimeProject -Path $target -Quiet | Should -BeTrue
        }

        It 'deletes nothing under -WhatIf' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-whatif-force')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            $before = @(Get-ChildItem -LiteralPath $target -Recurse -Force).Count

            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer -Force -WhatIf

            @(Get-ChildItem -LiteralPath $target -Recurse -Force).Count | Should -Be $before
            [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Exist
        }
    }

    # -Confirm runs in a child pwsh, whose console answers the prompts. The prompt text does not reach
    # redirected stdout on every platform, so these assert what was written. Each test gives one answer
    # more than the prompts it expects, chosen so that an extra prompt (per folder, item or module)
    # would change what is written.
    Context '-Confirm' {
        BeforeAll {
            # Relative paths of every file under a folder, sorted.
            $script:fileList = {
                param([string]$Root)
                @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force | ForEach-Object { [System.IO.Path]::GetRelativePath($Root, $_.FullName) } | Sort-Object)
            }
        }

        It 'asks once to write the files, not once per folder: one yes writes the whole project' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-confirm')
            $reference = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-reference')
            $null = New-LambdaPSRuntimeProject -Path $reference -PowerShellVersion '7.4'

            $null = Invoke-InChildPwsh -Command "`$null = New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion 7.4 -Confirm" -Answer 'Y', 'N', 'N'

            $written = & $script:fileList $target
            $written.Count | Should -BeGreaterThan 0
            $written | Should -Be (& $script:fileList $reference)
            [System.IO.Path]::Combine($target, 'modules') | Should -Exist
        }

        It 'asks once to delete and once to write under -Force, not once per removed item' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-force')
            $reference = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-force-reference')
            $null = New-LambdaPSRuntimeProject -Path $reference -PowerShellVersion '7.4' -Target Layer
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'

            $null = Invoke-InChildPwsh -Command "`$null = New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion 7.4 -Target Layer -Force -Confirm" -Answer 'Y', 'Y', 'N', 'N'

            & $script:fileList $target | Should -Be (& $script:fileList $reference)
            [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Not -Exist
            (Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).Target | Should -Be 'Layer'
        }

        It 'asks once to copy the modules, not once per module' {
            $moduleRoot = [System.IO.Path]::Combine($TestDrive, 'confirm-set-psmodules')
            foreach ($name in 'LambdaPSConfirmSetA', 'LambdaPSConfirmSetB') {
                $moduleDir = [System.IO.Path]::Combine($moduleRoot, $name, '1.0.0')
                $null = New-Item -ItemType Directory -Force -Path $moduleDir
                Set-Content -LiteralPath ([System.IO.Path]::Combine($moduleDir, "$name.psd1")) -Value "@{ ModuleVersion = '1.0.0'; GUID = '$([guid]::NewGuid())' }"
            }
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-module-set')
            $savedModulePath = $env:PSModulePath
            $env:PSModulePath = $moduleRoot + [System.IO.Path]::PathSeparator + $env:PSModulePath
            try {
                $modules = "@{ ModuleName = 'LambdaPSConfirmSetA'; RequiredVersion = '1.0.0' }, @{ ModuleName = 'LambdaPSConfirmSetB'; RequiredVersion = '1.0.0' }"
                # One yes writes the files and one copies the modules; a prompt per module would take the 'N'.
                $null = Invoke-InChildPwsh -Command "`$null = New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion 7.4 -IncludeModule $modules -Confirm" -Answer 'Y', 'Y', 'N'
            }
            finally { $env:PSModulePath = $savedModulePath }

            [System.IO.Path]::Combine($target, 'modules', 'LambdaPSConfirmSetA', '1.0.0') | Should -Exist
            [System.IO.Path]::Combine($target, 'modules', 'LambdaPSConfirmSetB', '1.0.0') | Should -Exist
        }

        It 'stages no modules when the file write is declined' {
            $moduleRoot = [System.IO.Path]::Combine($TestDrive, 'confirm-psmodules')
            $moduleDir = [System.IO.Path]::Combine($moduleRoot, 'LambdaPSConfirmFake', '1.0.0')
            $null = New-Item -ItemType Directory -Force -Path $moduleDir
            Set-Content -LiteralPath ([System.IO.Path]::Combine($moduleDir, 'LambdaPSConfirmFake.psd1')) -Value "@{ ModuleVersion = '1.0.0'; GUID = '$([guid]::NewGuid())' }"
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-decline-write')
            $savedModulePath = $env:PSModulePath
            $env:PSModulePath = $moduleRoot + [System.IO.Path]::PathSeparator + $env:PSModulePath
            try {
                $command = "`$null = New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion 7.4 -IncludeModule @{ ModuleName = 'LambdaPSConfirmFake'; RequiredVersion = '1.0.0' } -Confirm"
                # A module copy asked about after the declined write would take the 'Y' and create the target.
                $result = Invoke-InChildPwsh -Command $command -Answer 'N', 'Y'
            }
            finally { $env:PSModulePath = $savedModulePath }

            $result.Output | Should -BeLike '*because writing the project files was declined*'
            $target | Should -Not -Exist
        }

        It 'writes nothing over the old project when the -Force delete is declined' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-decline')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            $before = @(Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object FullName)
            # A file write asked about after the declined delete would take the 'Y' and write over the old project.
            $result = Invoke-InChildPwsh -Command "`$null = New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion 7.4 -Target Layer -Force -Confirm" -Answer 'N', 'Y'

            $result.Output | Should -BeLike '*because deleting its existing contents was declined*'
            @(Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object FullName) | Should -Be $before
            (Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).Target | Should -Be 'CMR'
        }

        # A declined download stops the scaffold, as a declined delete does (J3): before, the project was
        # written over the old one, or written without the modules it was asked to include.
        It 'deletes and writes nothing, naming the modules, when the download is declined' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-confirm-decline-download')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'
            $before = @(Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object FullName)
            $stub = @"
function script:Save-LambdaPSModuleFromRepository {
    param([string]`$Name, [string]`$VersionRange, [string]`$Path, [string[]]`$ModuleRepository)
    `$script:LambdaPSCalls.Add("download `$Name")
    `$exact = `$VersionRange.Trim('[', ']')
    `$d = [System.IO.Path]::Combine(`$Path, `$Name, `$exact)
    `$null = New-Item -ItemType Directory -Path `$d -Force -Confirm:`$false
    Set-Content -LiteralPath ([System.IO.Path]::Combine(`$d, "`$Name.psd1")) -Value "@{ ModuleVersion = '`$exact' }" -Confirm:`$false
    [PSCustomObject]@{ Name = `$Name; Version = `$exact; Repository = 'Internal' }
}
"@
            $command = "`$r = New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion 7.4 -Target Layer -Force " +
                "-IncludeModule @{ ModuleName = 'LambdaPSFake.NotInstalled'; RequiredVersion = '1.0.0' } -Confirm; 'WRITTEN:' + (@(`$r) -join ',')"

            # 'N' declines the download; the 'Y's would delete, write and copy if the scaffold went on.
            $result = Invoke-InChildPwsh -Stub $stub -Command $command -Answer 'N', 'Y', 'Y', 'Y'

            $result.Calls | Should -BeNullOrEmpty
            $result.Output -split "`n" | Should -Contain 'WRITTEN:'
            $result.Output | Should -BeLike '*LambdaPSFake.NotInstalled 1.0.0 was declined*'
            @(Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object FullName) | Should -Be $before
            (Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).Target | Should -Be 'CMR'
        }
    }

    Context '-Module staging' {
        It 'forwards -Module to Copy-LambdaPSModuleSet against the project modules directory' {
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@('AWS.Tools.S3 5.0.103') }
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }

            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module')
            $modulesDir = [System.IO.Path]::Combine($target, 'modules')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Path -eq $modulesDir -and $Module[0].Name -eq 'AWS.Tools.S3' }
        }

        It 'names the repositories to download from when -ModuleRepository is given' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $null = New-LambdaPSRuntimeProject -Path ([System.IO.Path]::Combine($TestDrive, 'rt-repo')) -PowerShellVersion '7.4' `
                -IncludeModule @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -ModuleRepository 'Internal', 'PSGallery'

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { "$ModuleRepository" -eq 'Internal PSGallery' }
        }

        It 'downloads missing modules first (Save by default) and stages from the download folder' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-save')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $MissingModuleAction -eq 'Save' -and $Module[0].Name -eq 'AWS.Tools.S3' }
            Should -Invoke 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $SearchPath -like '*lambdaps-modules-*' }
        }

        It 'forwards -MissingModuleAction Install to the resolver' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-install')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -MissingModuleAction Install `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $MissingModuleAction -eq 'Install' }
        }

        It 'never downloads under -MissingModuleAction Stop' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { throw 'must not be called' }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }
            # The up-front Stop check walks the set; report it as fully installed.
            Mock 'Get-LambdaPSModuleClosure' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Resolved = @(); Missing = @(); Pending = @(); Error = $null }
            }

            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-stop')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -MissingModuleAction Stop `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'fails before writing anything under -MissingModuleAction Stop when a module is not installed' {
            # Stop must fail before the template is copied, or it leaves a half-written project.
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-stop-missing')
            { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -MissingModuleAction Stop `
                    -Module @{ ModuleName = 'LambdaPSFake.NotInstalled'; RequiredVersion = '1.0.0' } } |
                Should -Throw -ErrorId 'ModuleNotFoundLocally*'
            Test-Path -LiteralPath $target | Should -BeFalse
        }

        It 'rejects a file at -Path with TargetIsFile, even with -Force' {
            $file = [System.IO.Path]::Combine($TestDrive, 'rt-a-file')
            Set-Content -LiteralPath $file -Value 'x'
            $target = $file
            { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Force } | Should -Throw -ErrorId 'TargetIsFile*'
            Get-Content -LiteralPath $file | Should -Be 'x'
        }

        It 'leaves an existing project untouched when a download fails under -Force' {
            # The download writes nothing, so the module is still missing afterwards: ModuleDownloadFailed.
            Mock 'Save-LambdaPSAWSToolsModule' -ModuleName $script:moduleName -MockWith { }

            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-keep')
            $null = New-Item -ItemType Directory -Path $target -Force
            $keep = [System.IO.Path]::Combine($target, 'keep.txt')
            Set-Content -LiteralPath $keep -Value 'mine'

            { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Force `
                    -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.99.999' } } |
                Should -Throw -ErrorId 'ModuleDownloadFailed,New-LambdaPSRuntimeProject'
            Test-Path -LiteralPath $keep | Should -BeTrue
        }

        It 'leaves an existing project untouched when a module is not pinned under -Force' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-keep-unpinned')
            $null = New-Item -ItemType Directory -Path $target -Force
            $keep = [System.IO.Path]::Combine($target, 'keep.txt')
            Set-Content -LiteralPath $keep -Value 'mine'

            { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Force -IncludeModule 'AWS.Tools.S3' } |
                Should -Throw -ErrorId 'ModuleVersionRequired,New-LambdaPSRuntimeProject'
            Test-Path -LiteralPath $keep | Should -BeTrue
        }

        It 'deletes the download folder afterwards' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                $null = New-Item -ItemType Directory -Path $Path -Force
                $script:lastDownloadRoot = $Path
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-cleanup')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            # The mock body runs in this test file's scope, so $script: here is the test script.
            $root = $script:lastDownloadRoot
            $root | Should -Not -BeNullOrEmpty
            Test-Path -LiteralPath $root | Should -BeFalse
        }

        It 'throws ModuleVersionRequired for a minimum-version spec before scaffolding' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-minver')
            { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' `
                    -Module @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.103' } } |
                Should -Throw -ErrorId 'ModuleVersionRequired,New-LambdaPSRuntimeProject'
            $target | Should -Not -Exist
        }

        It 'throws ModuleVersionRequired for a bare module name before scaffolding' {
            $target = [System.IO.Path]::Combine($TestDrive, 'rt-module-bare')
            { New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -IncludeModule 'AWS.Tools.S3' } |
                Should -Throw -ErrorId 'ModuleVersionRequired,New-LambdaPSRuntimeProject'
            $target | Should -Not -Exist
        }
    }

    Context '-Target Layer' {
        It 'scaffolds the complete layer tree from the template' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-happy')
            $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer

            $written | Should -Not -BeNullOrEmpty
            $expected = @(
                'bootstrap'
                'runtime.ps1'
                'PowerShellLambdaContext.cs'
                'lambdaps-runtime.json'
                [System.IO.Path]::Combine('modules', 'pwsh-runtime.psd1')
                [System.IO.Path]::Combine('modules', 'Private', 'Set-PSModulePath.ps1')
            )
            foreach ($relative in $expected) {
                Test-Path -LiteralPath ([System.IO.Path]::Combine($target, $relative)) |
                    Should -BeTrue -Because "$relative must be scaffolded for the Layer target"
            }
        }

        It 'suggests the configured timeout and memory for functions that use the layer' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-readme-sizing')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer
            $function = InModuleScope $script:moduleName { $script:Config.function }
            $readme = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($target, 'README.md'))

            $readme | Should -Match "-Timeout $($function.Timeout) -MemorySize $($function.MemorySize)"
            $readme | Should -Not -Match '__\w+__'
        }

        It 'does not emit a CMR Dockerfile for the Layer target' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-nodockerfile')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer
            Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) |
                Should -BeFalse
            @(Get-ChildItem -LiteralPath $target -Recurse -File -Filter '*Dockerfile*') | Should -BeNullOrEmpty
        }

        It 'records the target and PowerShell version in the marker' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-version')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer
            $marker = Get-Content -LiteralPath (
                [System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json
            $marker.Target | Should -Be 'Layer'
            $marker.PowerShellVersion | Should -Be '7.4'
        }

        It 'writes a layer bootstrap that references /opt and not /var/runtime' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-bootstrap')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -Target Layer
            $bootstrap = Get-Content -LiteralPath (
                [System.IO.Path]::Combine($target, 'bootstrap')) -Raw
            # Evaluate functional lines only: the template comment names the CMR path ("NOT
            # /var/runtime") to contrast the layer layout, which is not a functional reference.
            $code = ($bootstrap -split "`n" |
                Where-Object { $_ -notmatch '^\s*#(?!!)' }) -join "`n"
            $code | Should -Match '/opt'
            $code | Should -Not -Match '/var/runtime'
        }

        It 'records a -PwshVersion override in the marker, for Build-LambdaPSRuntimePackage' {
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-marker')
            $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -PwshVersion '7.4.2' -Target Layer -WarningAction SilentlyContinue
            $marker = Get-Content -LiteralPath (
                [System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json
            $marker.Target | Should -Be 'Layer'
            $marker.PowerShellVersion | Should -Be '7.4'
            $marker.PwshVersion | Should -Be '7.4.2'
        }

        It 'records Layer 7.5 with a pinned 7.5.11 and stages modules and their dependencies beside the runtime''s Private, with no arm64 warning' {
            $name = 'LambdaPSLayerFake'
            $dependency = 'LambdaPSLayerFakeDependency'
            $moduleRoot = [System.IO.Path]::Combine($TestDrive, 'layer-psmodules')
            $moduleDir = [System.IO.Path]::Combine($moduleRoot, $name, '1.0.0')
            $dependencyDir = [System.IO.Path]::Combine($moduleRoot, $dependency, '2.0.0')
            $null = New-Item -ItemType Directory -Force -Path $moduleDir, $dependencyDir
            Set-Content -LiteralPath ([System.IO.Path]::Combine($moduleDir, "$name.psd1")) -Value "@{ ModuleVersion = '1.0.0'; GUID = '$([guid]::NewGuid())'; RequiredModules = @(@{ ModuleName = '$dependency'; RequiredVersion = '2.0.0' }) }"
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dependencyDir, "$dependency.psd1")) -Value "@{ ModuleVersion = '2.0.0'; GUID = '$([guid]::NewGuid())' }"
            $specs = @(@{ ModuleName = $name; RequiredVersion = '1.0.0' })
            $target = [System.IO.Path]::Combine($TestDrive, 'layer-75')
            $savedModulePath = $env:PSModulePath
            $env:PSModulePath = $moduleRoot + [System.IO.Path]::PathSeparator + $env:PSModulePath
            try {
                $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.5' -PwshVersion '7.5.11' -Target Layer -IncludeModule $specs -WarningVariable warnings -WarningAction SilentlyContinue
            }
            finally { $env:PSModulePath = $savedModulePath }

            $marker = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json
            $marker.Target | Should -Be 'Layer'
            $marker.PowerShellVersion | Should -Be '7.5'
            $marker.PwshVersion | Should -Be '7.5.11'
            @(Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($target, 'modules')) -Directory | ForEach-Object Name | Sort-Object) |
                Should -Be @($name, $dependency, 'Private')
            [System.IO.Path]::Combine($target, 'modules', $name, '1.0.0', "$name.psd1") | Should -Exist
            [System.IO.Path]::Combine($target, 'modules', $dependency, '2.0.0', "$dependency.psd1") | Should -Exist
            [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Not -Exist
            @($warnings) | Should -BeNullOrEmpty
        }
    }
}

# The scaffolders take their helpers' comma-returned string[] with a typed assignment. Wrapping it in
# @(...) would make the output ONE string holding every path joined by spaces, -WhatIf would return
# @(''), and staged module directories would not be reported.
Describe 'New-LambdaPSRuntimeProject output' -Tag 'Unit' {
    BeforeAll {
        # Two throwaway modules on PSModulePath, so staging resolves locally with no download. Two,
        # because joining the output into one string loses module directories only when more than
        # one module is staged.
        $script:fakeModuleRoot = [System.IO.Path]::Combine($TestDrive, 'psmodules')
        $script:fakeNames = 'LambdaPSOutputFakeA', 'LambdaPSOutputFakeB'
        foreach ($fakeName in $script:fakeNames) {
            $fakeDir = [System.IO.Path]::Combine($script:fakeModuleRoot, $fakeName, '1.0.0')
            $null = New-Item -ItemType Directory -Force -Path $fakeDir
            Set-Content -LiteralPath ([System.IO.Path]::Combine($fakeDir, "$fakeName.psd1")) -Value (
                "@{ ModuleVersion = '1.0.0'; GUID = '$([guid]::NewGuid())' }")
        }
        $script:savedModulePath = $env:PSModulePath
        $env:PSModulePath = $script:fakeModuleRoot + [System.IO.Path]::PathSeparator + $env:PSModulePath
        $script:fakeSpecs = @($script:fakeNames | ForEach-Object { @{ ModuleName = $_; RequiredVersion = '1.0.0' } })
    }

    AfterAll {
        $env:PSModulePath = $script:savedModulePath
    }

    It 'returns one path per file written, each an existing file' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-out-files')
        $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4'

        , $written | Should -BeOfType ([string[]])
        $files = @(Get-ChildItem -LiteralPath $target -Recurse -File -Force)
        $written.Count | Should -Be $files.Count
        foreach ($path in $written) {
            Test-Path -LiteralPath $path -PathType Leaf | Should -BeTrue -Because "'$path' must be a single written file"
        }
    }

    It 'reports every staged module directory alongside the files' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-out-module')
        $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -IncludeModule $script:fakeSpecs

        foreach ($fakeName in $script:fakeNames) {
            $moduleDir = [System.IO.Path]::GetFullPath(
                [System.IO.Path]::Combine($target, 'modules', $fakeName, '1.0.0'))
            $written | Should -Contain $moduleDir
        }
    }

    It 'returns no paths under -WhatIf' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-out-whatif')
        $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -WhatIf

        @($written).Count | Should -Be 0 -Because 'no path (not even an empty string) was written'
        $target | Should -Not -Exist
    }

    It 'returns no paths under -Force -WhatIf over a project with staged modules' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-out-whatif-force-modules')
        $null = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -IncludeModule $script:fakeSpecs

        $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -IncludeModule $script:fakeSpecs -Force -WhatIf

        @($written).Count | Should -Be 0 -Because 'the module folders on disk are the earlier scaffold''s, not written by this run'
    }

    It 'writes nothing under -WhatIf with -IncludeModule' {
        $target = [System.IO.Path]::Combine($TestDrive, 'rt-out-whatif-modules')
        $written = New-LambdaPSRuntimeProject -Path $target -PowerShellVersion '7.4' -IncludeModule $script:fakeSpecs -WhatIf

        @($written).Count | Should -Be 0
        $target | Should -Not -Exist
    }
}

# '$' also matches before a final newline, so the version patterns are anchored with \A and \z.
Describe 'New-LambdaPSRuntimeProject parameter validation' -Tag 'Unit' {
    It 'rejects -<Parameter> with a trailing newline' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Arguments = @{ PowerShellVersion = "7.4`n" } }
        @{ Parameter = 'PwshVersion'; Arguments = @{ PowerShellVersion = '7.4'; PwshVersion = "7.4.20`n" } }
    ) {
        $target = [System.IO.Path]::Combine($TestDrive, "rt-newline-$Parameter")
        { New-LambdaPSRuntimeProject -Path $target @Arguments } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeProject' -ExpectedMessage "*parameter '$Parameter'*"
        $target | Should -Not -Exist
    }

    # \d also matches other scripts' digits, such as Arabic-Indic ones; a version is ASCII digits only.
    It 'rejects -<Parameter> in non-ASCII digits, naming the form it takes' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Arguments = @{ PowerShellVersion = "$([char]0x0667).$([char]0x0664)" }; Message = '*is not a PowerShell version in major.minor form, for example 7.4.' }
        @{ Parameter = 'PwshVersion'; Arguments = @{ PowerShellVersion = '7.4'; PwshVersion = "7.4.$([char]0x0662)$([char]0x0660)" }; Message = '*is not a pwsh release in x.y.z form, for example 7.4.20.' }
    ) {
        $target = [System.IO.Path]::Combine($TestDrive, "rt-digits-$Parameter")
        { New-LambdaPSRuntimeProject -Path $target @Arguments } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeProject' -ExpectedMessage $Message
        $target | Should -Not -Exist
    }

    It 'rejects -<Parameter> missing a version part, naming the form it takes' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Arguments = @{ PowerShellVersion = '7' }; Message = '*is not a PowerShell version in major.minor form, for example 7.4.' }
        @{ Parameter = 'PwshVersion'; Arguments = @{ PowerShellVersion = '7.4'; PwshVersion = '7.4' }; Message = '*is not a pwsh release in x.y.z form, for example 7.4.20.' }
    ) {
        $target = [System.IO.Path]::Combine($TestDrive, "rt-short-$Parameter")
        { New-LambdaPSRuntimeProject -Path $target @Arguments } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeProject' -ExpectedMessage $Message
        $target | Should -Not -Exist
    }

    It 'rejects a whitespace-only -Path' {
        { New-LambdaPSRuntimeProject -Path '   ' -PowerShellVersion '7.4' } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSRuntimeProject'
    }
}

Describe 'New-LambdaPSRuntimeProject help' -Tag 'Unit' {
    It 'names the TargetOwnedByAnotherUser refusal under -Force' {
        (Get-Help New-LambdaPSRuntimeProject -Parameter Force | Out-String) -replace '\s+', ' ' |
            Should -BeLike '*TargetOwnedByAnotherUser*'
    }
}
