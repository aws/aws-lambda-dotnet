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
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSFunctionProject' -Tag 'Unit' {
    It 'scaffolds the handler, manifest, and modules directory' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-happy')
        $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler'

        $written | Should -Not -BeNullOrEmpty
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'handler.ps1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'manifest.json')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'modules')) | Should -BeTrue
    }

    It 'records the runtime-accepted handler string, alias, and architecture in the manifest' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-manifest')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'app' -RuntimeAlias 'prod' -Architecture 'arm64'
        $manifest = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'manifest.json')) -Raw | ConvertFrom-Json
        # HandlerString must be the script-file form the runtime's Get-Handler accepts,
        # NOT the bare noun (which the runtime rejects as "Invalid Lambda Handler").
        $manifest.HandlerString | Should -Be 'app.ps1'
        $manifest.HandlerString | Should -Not -Be 'app'
        $manifest.ScriptFile | Should -Be 'app.ps1'
        $manifest.RuntimeAlias | Should -Be 'prod'
        $manifest.Architecture | Should -Be 'arm64'
    }

    It 'names the handler, alias and architecture in the README, and writes no runtime marker' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-readme')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'app' -RuntimeAlias 'prod' -Architecture 'arm64'
        $readme = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'README.md')) -Raw
        $readme | Should -BeLike '*The handler is ?app.ps1?.*'
        $readme | Should -BeLike "*Handler                    = 'app.ps1'*"
        $readme | Should -BeLike "*Architecture               = 'arm64'*"
        $readme | Should -BeLike "*RuntimeConfig_RuntimeAlias = 'prod'*"
        $function = InModuleScope $script:moduleName { $script:Config.function }
        $readme | Should -BeLike "*Timeout                    = $($function.Timeout)*"
        $readme | Should -BeLike "*MemorySize                 = $($function.MemorySize)*"
        $readme | Should -Not -BeLike '*__*__*'
        [System.IO.Path]::Combine($target, 'lambdaps-runtime.json') | Should -Not -Exist
    }

    It 'accepts a .ps1 handler name without doubling the extension' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-dotps1')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler.ps1'
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'handler.ps1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'handler.ps1.ps1')) | Should -BeFalse
        $manifest = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'manifest.json')) -Raw | ConvertFrom-Json
        $manifest.HandlerString | Should -Be 'handler.ps1'
        $manifest.ScriptFile | Should -Be 'handler.ps1'
    }

    It 'emits a starter handler that reads $LambdaInput.name and hints at #Requires' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-starter')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler'
        $body = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'handler.ps1')) -Raw
        $body | Should -Match 'LambdaInput\.name'
        $body | Should -Match 'Hello, \$name'
        $body | Should -Match 'requestId'
        $body | Should -Match '#Requires -Modules'
        $body | Should -Match '(?m)^param\(\$LambdaInput, \$LambdaContext\)'
    }

    It 'throws on an existing non-empty directory without -Force' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-exists')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler'
        $before = @(Get-ChildItem -LiteralPath $target -Recurse -Force).FullName
        { New-LambdaPSFunctionProject -Path $target -Handler 'handler' } |
            Should -Throw -ErrorId 'TargetNotEmpty,New-LambdaPSFunctionProject'
        @(Get-ChildItem -LiteralPath $target -Recurse -Force).FullName | Should -Be $before
    }

    # -Confirm runs in a child pwsh, whose console answers the prompts. The prompt text does not reach
    # redirected stdout on every platform, so these assert what was written, or that nothing was.
    It 'writes nothing over the old project when the -Force delete is declined under -Confirm' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-confirm-decline')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target Container -PowerShellVersion '7.4'
        $before = @(Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object FullName)
        # A file write asked about after the declined delete would take the 'Y' and write over the old project.
        $result = Invoke-InChildPwsh -Command "`$null = New-LambdaPSFunctionProject -Path '$target' -Handler handler -Force -Confirm" -Answer 'N', 'Y'

        $result.Output | Should -BeLike '*because deleting its existing contents was declined*'
        @(Get-ChildItem -LiteralPath $target -Recurse -Force | ForEach-Object FullName) | Should -Be $before
    }

    # A declined download stops the scaffold, as a declined delete does, so no project is written
    # without the modules it was asked to include.
    It 'writes nothing, naming the modules, when the download is declined under -Confirm' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-confirm-decline-download')
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
        $command = "`$r = New-LambdaPSFunctionProject -Path '$target' -Handler handler " +
            "-IncludeModule @{ ModuleName = 'LambdaPSFake.NotInstalled'; RequiredVersion = '1.0.0' } -Confirm; 'WRITTEN:' + (@(`$r) -join ',')"

        # 'N' declines the download; the 'Y's would write the files and copy the modules if the scaffold went on.
        $result = Invoke-InChildPwsh -Stub $stub -Command $command -Answer 'N', 'Y', 'Y'

        $result.Calls | Should -BeNullOrEmpty
        $result.Output -split "`n" | Should -Contain 'WRITTEN:'
        $result.Output | Should -BeLike '*LambdaPSFake.NotInstalled 1.0.0 was declined*'
        $target | Should -Not -Exist
    }

    It 'replaces rather than merges under -Force' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-force-replace')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target Container -PowerShellVersion '7.4'
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Force

        [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Not -Exist
        [System.IO.Path]::Combine($target, 'lambdaps-runtime.json') | Should -Not -Exist
        [System.IO.Path]::Combine($target, 'bootstrap') | Should -Not -Exist
        [System.IO.Path]::Combine($target, 'manifest.json') | Should -Exist
    }

    It 'replaces an edited handler when -Force re-scaffolds a SelfContainedZip project as Container' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-force-scz-to-container')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target SelfContainedZip -PowerShellVersion '7.4'
        $handler = [System.IO.Path]::Combine($target, 'handler.ps1')
        Add-Content -LiteralPath $handler -Value '# my edit'

        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target Container -PowerShellVersion '7.4' -Force

        Get-Content -LiteralPath $handler -Raw | Should -Not -Match '# my edit'
        (Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).Target | Should -Be 'Container'
        [System.IO.Path]::Combine($target, 'Dockerfile') | Should -Exist
    }

    It 'defaults to the RuntimeBound shape (no runtime marker, handler-only tree)' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-default-runtimebound')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler'
        # RuntimeBound is handler-only: no self-contained runtime marker or bootstrap.
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) | Should -BeFalse
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'bootstrap')) | Should -BeFalse
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'manifest.json')) | Should -BeTrue
        # Dot-files such as modules/.gitkeep are left out: Windows does not hide them, so Get-ChildItem
        # lists them there and not on Linux or macOS.
        @(Get-ChildItem -LiteralPath $target -Recurse -File -Force | Where-Object { -not $_.Name.StartsWith('.') } |
                ForEach-Object { [System.IO.Path]::GetRelativePath($target, $_.FullName) } | Sort-Object) |
            Should -Be @('handler.ps1', 'manifest.json', 'README.md') -Because 'the runtime it binds to brings everything else'
    }

    It 'suggests the configured timeout and memory in the <Target> README''s Publish-LMFunction command' -ForEach @(
        @{ Target = 'SelfContainedZip'; Pattern = '-Timeout {0} -MemorySize {1}' }
        @{ Target = 'Container'; Pattern = '-Timeout\s+{0} -MemorySize {1}' }
        @{ Target = 'RuntimeBound'; Pattern = 'Timeout\s+= {0}\s+MemorySize\s+= {1}' }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "fn-readme-sizing-$Target")
        $versionArgs = if ($Target -ne 'RuntimeBound') { @{ PowerShellVersion = '7.4' } } else { @{} }
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler 'handler' -Target $Target @versionArgs
        $function = InModuleScope $script:moduleName { $script:Config.function }
        $readme = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($projectDir, 'README.md'))

        $readme | Should -Match ($Pattern -f $function.Timeout, $function.MemorySize)
        $readme | Should -Not -Match '__\w+__'
    }

    It 'gives the README the runtime settings section from Templates/Shared' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-readme-settings')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler'
        $readme = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($target, 'README.md'))
        $append = [System.IO.File]::ReadAllText([System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'Templates', 'Shared', 'README.md.append'))

        $readme | Should -Match "every invoke then\s+fails\.\n\n## Runtime settings"
        $readme.EndsWith($append, [System.StringComparison]::Ordinal) | Should -BeTrue
    }

    Context '-Module staging' {
        It 'forwards -Module to Copy-LambdaPSModuleSet against the project modules directory' {
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@('AWS.Tools.S3 5.0.103') }
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }

            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module')
            $modulesDir = [System.IO.Path]::Combine($target, 'modules')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Path -eq $modulesDir -and $Module[0].Name -eq 'AWS.Tools.S3' }
        }

        It 'downloads missing modules first (Save by default) and stages from the download folder' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-save')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $MissingModuleAction -eq 'Save' -and $Module[0].Name -eq 'AWS.Tools.S3' }
            Should -Invoke 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $SearchPath -like '*lambdaps-modules-*' }
        }

        It 'names the repositories to download from when -ModuleRepository is given' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ Downloaded = @(); Pending = @() } }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $null = New-LambdaPSFunctionProject -Path ([System.IO.Path]::Combine($TestDrive, 'fn-repo')) -Handler 'handler' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.274' } -ModuleRepository 'Internal', 'PSGallery'

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { "$ModuleRepository" -eq 'Internal PSGallery' }
        }

        It 'forwards -MissingModuleAction Install to the resolver' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-install')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -MissingModuleAction Install `
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

            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-stop')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -MissingModuleAction Stop `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            Should -Invoke 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'fails before writing anything under -MissingModuleAction Stop when a module is not installed' {
            # Stop must fail before the template is copied, or it leaves a half-written project.
            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-stop-missing')
            { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -MissingModuleAction Stop `
                    -Module @{ ModuleName = 'LambdaPSFake.NotInstalled'; RequiredVersion = '1.0.0' } } |
                Should -Throw -ErrorId 'ModuleNotFoundLocally*'
            Test-Path -LiteralPath $target | Should -BeFalse
        }

        It 'rejects a file at -Path with TargetIsFile, even with -Force' {
            $file = [System.IO.Path]::Combine($TestDrive, 'fn-a-file')
            Set-Content -LiteralPath $file -Value 'x'
            $target = $file
            { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Force } | Should -Throw -ErrorId 'TargetIsFile*'
            Get-Content -LiteralPath $file | Should -Be 'x'
        }

        It 'leaves an existing project untouched when a download fails under -Force' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith { throw 'download failed' }

            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-keep')
            $null = New-Item -ItemType Directory -Path $target -Force
            $keep = [System.IO.Path]::Combine($target, 'keep.txt')
            Set-Content -LiteralPath $keep -Value 'mine'

            { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Force `
                    -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' } } | Should -Throw '*download failed*'
            Test-Path -LiteralPath $keep | Should -BeTrue
        }

        It 'deletes the download folder afterwards' {
            Mock 'Save-LambdaPSMissingModule' -ModuleName $script:moduleName -MockWith {
                $null = New-Item -ItemType Directory -Path $Path -Force
                $script:lastDownloadRoot = $Path
                [PSCustomObject]@{ Downloaded = @(); Pending = @() }
            }
            Mock 'Copy-LambdaPSModuleSet' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-cleanup')
            $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' `
                -Module @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }

            # The mock body runs in this test file's scope, so $script: here is the test script.
            $root = $script:lastDownloadRoot
            $root | Should -Not -BeNullOrEmpty
            Test-Path -LiteralPath $root | Should -BeFalse
        }

        It 'throws ModuleVersionRequired for a minimum-version spec before scaffolding' {
            $target = [System.IO.Path]::Combine($TestDrive, 'fn-module-minver')
            { New-LambdaPSFunctionProject -Path $target -Handler 'handler' `
                    -Module @{ ModuleName = 'AWS.Tools.S3'; ModuleVersion = '5.0.103' } } |
                Should -Throw -ErrorId 'ModuleVersionRequired,New-LambdaPSFunctionProject'
        }
    }
}

Describe 'New-LambdaPSFunctionProject -Target SelfContainedZip' -Tag 'Unit' {
    It 'emits the self-contained tree with a SelfContainedZip marker' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-scz')
        $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'

        $written | Should -Not -BeNullOrEmpty
        foreach ($rel in @(
                'bootstrap',
                'runtime.ps1',
                'handler.ps1',
                'PowerShellLambdaContext.cs',
                'lambdaps-runtime.json',
                [System.IO.Path]::Combine('modules', 'pwsh-runtime.psd1'),
                [System.IO.Path]::Combine('modules', 'Private', 'Set-PSModulePath.ps1'))) {
            Test-Path -LiteralPath ([System.IO.Path]::Combine($target, $rel)) | Should -BeTrue -Because "the template ships $rel"
        }

        $marker = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json
        $marker.Target | Should -Be 'SelfContainedZip'
        $marker.PowerShellVersion | Should -Be '7.4'
        @(Get-ChildItem -LiteralPath $target -Recurse -File -Filter '*Dockerfile*') | Should -BeNullOrEmpty
    }

    It 'ships a bootstrap that references /var/task and not /var/runtime or /opt' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-scz-bootstrap')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $bootstrap = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'bootstrap')) -Raw
        $bootstrap | Should -Match '/var/task'
        $bootstrap | Should -Not -Match '/var/runtime'
        $bootstrap | Should -Not -Match '/opt'
    }

    It 'throws when -PowerShellVersion is omitted for SelfContainedZip' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-scz-noversion')
        { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'SelfContainedZip' } |
            Should -Throw -ErrorId 'PowerShellVersionRequired,New-LambdaPSFunctionProject'
        $target | Should -Not -Exist
    }

    It 'throws for an unmapped version with no -PwshVersion override' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-scz-unmapped')
        { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '9.9' } |
            Should -Throw -ErrorId 'UnknownPwshVersion,New-LambdaPSFunctionProject'
    }
}

Describe 'New-LambdaPSFunctionProject -Target Container' -Tag 'Unit' {
    It 'emits the container-per-function tree with a Container marker' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container')
        $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'

        $written | Should -Not -BeNullOrEmpty
        foreach ($rel in @(
                'Dockerfile',
                'bootstrap',
                'lambda-entrypoint.sh',
                'handler.ps1',
                'PowerShellLambdaContext.cs',
                'lambdaps-runtime.json',
                [System.IO.Path]::Combine('modules', 'pwsh-runtime.psd1'),
                [System.IO.Path]::Combine('modules', 'Private', 'Set-PSModulePath.ps1'))) {
            Test-Path -LiteralPath ([System.IO.Path]::Combine($target, $rel)) | Should -BeTrue -Because "the template ships $rel"
        }

        $marker = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json
        $marker.Target | Should -Be 'Container'
        $marker.PowerShellVersion | Should -Be '7.4'
    }

    It 'substitutes the resolved pwsh version into the Dockerfile and leaves no token' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-version')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match "ARG PWSH_VERSION=$([regex]::Escape($script:pwshConfig.DefaultPatch['7.4']))"
        $dockerfile | Should -Not -Match '__PWSH_VERSION__'
    }

    It 'ships a Dockerfile that sets CMD to the handler and bakes the handler into /var/task' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-dockerfile')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match 'CMD \[ "handler\.ps1" \]'
        $dockerfile | Should -Match '(?m)^COPY \. /var/task$'
    }

    # Lambda runs the ENTRYPOINT with CMD as its argument; the entrypoint makes that the handler.
    It 'keeps the runtime image''s ENTRYPOINT, which takes CMD as the handler, and removes the runtime files from /var/task' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-entrypoint')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        @([regex]::Matches($dockerfile, '(?m)^ENTRYPOINT .*$').Value) | Should -Be @('ENTRYPOINT [ "/var/runtime/lambda-entrypoint.sh" ]')
        $dockerfile.IndexOf('ENTRYPOINT [') | Should -BeLessThan $dockerfile.IndexOf('CMD [ "handler.ps1" ]')
        $dockerfile | Should -Match '(?m)^RUN rm -rf .*/var/task/lambda-entrypoint\.sh .*/var/task/bootstrap '
    }

    # The whole project goes into /var/task, so a handler can use a script or data file beside it.
    It 'copies the whole project into /var/task, leaving out the scaffolding with .dockerignore' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-context')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $ignorePath = [System.IO.Path]::Combine($target, '.dockerignore')
        $ignorePath | Should -Exist
        $ignored = @(Get-Content -LiteralPath $ignorePath | Where-Object { $_ -and -not $_.StartsWith('#') })

        $dockerfile | Should -Match '(?m)^COPY \. /var/task$'
        foreach ($scaffolding in 'Dockerfile', 'README.md', 'lambdaps-runtime.json', '.dockerignore', '**/.git', '**/.gitignore', '**/*.zip') {
            $ignored | Should -Contain $scaffolding
        }
        # Every other COPY reads from the same context, so .dockerignore must leave its source in.
        $sources = @([regex]::Matches($dockerfile, '(?m)^COPY (?:--\S+ )*(\S+)\s+/var/runtime') | ForEach-Object { $_.Groups[1].Value })
        $sources | Should -Not -BeNullOrEmpty
        foreach ($source in $sources) {
            @($ignored | Where-Object { $source -like $_ }) | Should -BeNullOrEmpty -Because "COPY $source needs it in the build context"
        }
    }

    It 'builds on the CMR runtime image''s Dockerfile, the one copy of the runtime steps' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-base')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $runtimeTarget = [System.IO.Path]::Combine($TestDrive, 'rt-container-base')
        $null = New-LambdaPSRuntimeProject -Path $runtimeTarget -PowerShellVersion '7.4'
        $dockerfile = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($target, 'Dockerfile'))
        $runtimeDockerfile = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($runtimeTarget, 'Dockerfile'))

        $dockerfile.StartsWith($runtimeDockerfile) | Should -BeTrue
        # The shared part must read right in both: nothing in it may say an image needs no CMD.
        $dockerfile | Should -Not -Match 'needs no CMD'
        Test-Path -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile.append')) | Should -BeFalse
    }

    It 'ships a bootstrap that references /var/runtime' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-bootstrap')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $bootstrap = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'bootstrap')) -Raw
        $bootstrap | Should -Match '/var/runtime'
    }

    It 'honors an explicit -PwshVersion override' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-pin')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4' -PwshVersion '7.4.2' -WarningAction SilentlyContinue
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match '(?m)^ARG PWSH_VERSION=7\.4\.2$' -Because 'the default 7.4 pin also starts with 7.4.2'
    }

    It 'ships a Dockerfile that installs pwsh from the RPM on amd64 and the verified tarball on arm64' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-arch')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match 'ARG TARGETARCH'
        $dockerfile | Should -Match 'if \[ "\$TARGETARCH" = "arm64" \]'
        $dockerfile | Should -Match 'powershell-\$\{PWSH_VERSION\}-linux-arm64\.tar\.gz'
        $dockerfile | Should -Match 'sha256sum -c'
        $dockerfile | Should -Match 'packages\.microsoft\.com'
        $dockerfile | Should -Match 'dnf install -y "powershell-\$\{PWSH_VERSION\}"'
    }

    It 'bakes the pinned arm64 tarball SHA256 into the Dockerfile and leaves no token' -ForEach $script:arm64Cases {
        $target = [System.IO.Path]::Combine($TestDrive, "fn-container-arm64-$Pin")
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion $PowerShellVersion -PwshVersion $Pin -WarningVariable warnings
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match "ARG PWSH_ARM64_SHA256=$Hash"
        $dockerfile | Should -Not -Match '__PWSH_ARM64_SHA256__'
        $warnings | Should -BeNullOrEmpty
    }

    It 'keeps the arm64 hash token and warns when no hash is pinned for the version' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-arm64-unpinned')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '7.4' -PwshVersion '7.4.2' -WarningVariable warnings -WarningAction SilentlyContinue
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match 'ARG PWSH_ARM64_SHA256=__PWSH_ARM64_SHA256__'
        @($warnings).Count | Should -Be 1
        "$($warnings[0])" | Should -Match 'No SHA256 is pinned for the pwsh 7\.4\.2 arm64 tarball'
    }

    It 'throws when -PowerShellVersion is omitted for Container' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-noversion')
        { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' } |
            Should -Throw -ErrorId 'PowerShellVersionRequired,New-LambdaPSFunctionProject'
    }

    It 'throws for an unmapped version with no -PwshVersion override' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-container-unmapped')
        { New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target 'Container' -PowerShellVersion '9.9' } |
            Should -Throw -ErrorId 'UnknownPwshVersion,New-LambdaPSFunctionProject'
        $target | Should -Not -Exist
    }
}

# The scaffolders take their helpers' comma-returned string[] with a typed assignment. Wrapping it in
# @(...) would make the output ONE string holding every path joined by spaces, -WhatIf would return
# @(''), and staged module directories would not be reported.
Describe 'New-LambdaPSFunctionProject output' -Tag 'Unit' {
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
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-out-files')
        $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler'

        , $written | Should -BeOfType ([string[]])
        $files = @(Get-ChildItem -LiteralPath $target -Recurse -File -Force)
        $written.Count | Should -Be $files.Count
        foreach ($path in $written) {
            Test-Path -LiteralPath $path -PathType Leaf | Should -BeTrue -Because "'$path' must be a single written file"
        }
    }

    It 'reports every staged module directory alongside the files' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-out-module')
        $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -IncludeModule $script:fakeSpecs

        foreach ($fakeName in $script:fakeNames) {
            $moduleDir = [System.IO.Path]::GetFullPath(
                [System.IO.Path]::Combine($target, 'modules', $fakeName, '1.0.0'))
            $written | Should -Contain $moduleDir
        }
    }

    It 'reports a RequiredModules dependency''s directory alongside the module''s' {
        $dependentRoot = [System.IO.Path]::Combine($TestDrive, 'psmodules-dependent')
        $null = New-FakeModule -Root $dependentRoot -Name 'LambdaPSOutputFakeDependent' -Version '1.0.0' `
            -Requires "@(@{ ModuleName='LambdaPSOutputFakeB'; RequiredVersion='1.0.0' })"
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-out-dependency')
        $savedModulePath = $env:PSModulePath
        $env:PSModulePath = $dependentRoot + [System.IO.Path]::PathSeparator + $env:PSModulePath
        try {
            $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler' `
                -IncludeModule @{ ModuleName = 'LambdaPSOutputFakeDependent'; RequiredVersion = '1.0.0' }
        }
        finally { $env:PSModulePath = $savedModulePath }

        $written | Should -Contain ([System.IO.Path]::GetFullPath([System.IO.Path]::Combine($target, 'modules', 'LambdaPSOutputFakeB', '1.0.0')))
    }

    It 'stages -IncludeModule into modules/ for Container, which the Dockerfile copies to /var/runtime/modules' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-out-container-module')
        $null = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -Target Container -PowerShellVersion '7.4' -IncludeModule $script:fakeSpecs[0]

        [System.IO.Path]::Combine($target, 'modules', 'LambdaPSOutputFakeA', '1.0.0') | Should -Exist
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($target, 'Dockerfile')) -Raw
        $dockerfile | Should -Match '(?m)^COPY\s+modules\s+/var/runtime/modules$'
    }

    It 'returns no paths under -WhatIf' {
        $target = [System.IO.Path]::Combine($TestDrive, 'fn-out-whatif')
        $written = New-LambdaPSFunctionProject -Path $target -Handler 'handler' -WhatIf

        @($written).Count | Should -Be 0 -Because 'no path (not even an empty string) was written'
        $target | Should -Not -Exist
    }
}

# -Handler applies to the template targets too: the template's handler.ps1 is emitted under the
# requested name, and for Container the Dockerfile's CMD follows it.
Describe 'New-LambdaPSFunctionProject binding parameters on the template targets' -Tag 'Unit' {
    It 'warns that -RuntimeAlias and -Architecture are ignored for <Target>' -ForEach @(
        @{ Target = 'SelfContainedZip' }, @{ Target = 'Container' }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "ignored-$Target")
        $warnings = @()
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler fn -Target $Target -PowerShellVersion '7.4' `
            -RuntimeAlias staging -Architecture arm64 -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -BeLike '*-RuntimeAlias and -Architecture apply only to -Target RuntimeBound*'
        @($warnings).Count | Should -Be 1
        ($warnings -join ' ') | Should -BeLike '*Choose the architecture when you build it.*'
        $recorded = (Get-Content -LiteralPath ([System.IO.Path]::Combine($projectDir, 'lambdaps-runtime.json')) -Raw | ConvertFrom-Json).PSObject.Properties.Name
        $recorded | Should -Not -Contain 'Architecture'
        $recorded | Should -Not -Contain 'RuntimeAlias'
    }

    It 'warns in the singular, with the build-time hint, for -Architecture alone on Container' {
        $projectDir = [System.IO.Path]::Combine($TestDrive, 'ignored-architecture-only')
        $warnings = @()
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler fn -Target Container -PowerShellVersion '7.4' `
            -Architecture arm64 -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -BeLike '*-Architecture applies only to -Target RuntimeBound, so it is ignored*Choose the architecture when you build it.*'
        # The Dockerfile is the usual multi-architecture one: the architecture is chosen at build time.
        $pin = $script:pwshConfig.DefaultPatch['7.4']
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($projectDir, 'Dockerfile')) -Raw
        $dockerfile | Should -Match "(?m)^ARG PWSH_VERSION=$([regex]::Escape($pin))$"
        $dockerfile | Should -Match "(?m)^ARG PWSH_ARM64_SHA256=$($script:pwshConfig.ArchiveSha256[$pin].arm64)$"
    }

    It 'names the build-time architecture choice only when -Architecture is the ignored parameter' {
        $projectDir = [System.IO.Path]::Combine($TestDrive, 'ignored-alias-only')
        $warnings = @()
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler fn -Target Container -PowerShellVersion '7.4' `
            -RuntimeAlias staging -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -BeLike '*-RuntimeAlias applies only to -Target RuntimeBound*'
        ($warnings -join ' ') | Should -Not -BeLike '*architecture*'
    }

    It 'warns that -PowerShellVersion and -PwshVersion are ignored for RuntimeBound' {
        $projectDir = [System.IO.Path]::Combine($TestDrive, 'ignored-bound')
        $warnings = @()
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler fn -PowerShellVersion '7.4' -PwshVersion '7.4.20' `
            -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -BeLike '*-PowerShellVersion and -PwshVersion apply only to -Target SelfContainedZip and Container*'
        @(Get-ChildItem -LiteralPath $projectDir -Recurse -File | Where-Object { -not $_.Name.StartsWith('.') } | ForEach-Object Name | Sort-Object) |
            Should -Be @('fn.ps1', 'manifest.json', 'README.md')
    }

    It 'records no architecture for RuntimeBound unless one is chosen, so the deploy follows the runtime' {
        $projectDir = [System.IO.Path]::Combine($TestDrive, 'bound-noarch')
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler fn

        $manifest = Get-Content -LiteralPath ([System.IO.Path]::Combine($projectDir, 'manifest.json')) -Raw | ConvertFrom-Json
        $manifest.Architecture | Should -BeNullOrEmpty
        $manifest.RuntimeAlias | Should -Be 'default'
    }

    It 'does not warn when neither is given' {
        $projectDir = [System.IO.Path]::Combine($TestDrive, 'ignored-none')
        $warnings = @()
        $null = New-LambdaPSFunctionProject -Path $projectDir -Handler fn -Target SelfContainedZip -PowerShellVersion '7.4' `
            -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -Not -BeLike '*apply only to -Target RuntimeBound*'
    }
}

Describe 'New-LambdaPSFunctionProject -Handler on the template targets' -Tag 'Unit' {
    It 'emits myfn.ps1 instead of handler.ps1 for <Target> (-Handler <Handler>)' -ForEach @(
        @{ Target = 'SelfContainedZip'; Handler = 'myfn' }
        @{ Target = 'SelfContainedZip'; Handler = 'myfn.ps1' }
        @{ Target = 'Container'; Handler = 'myfn' }
        @{ Target = 'Container'; Handler = 'myfn.ps1' }
    ) {
        $projectPath = [System.IO.Path]::Combine($TestDrive, "fn-handler-$Target-$($Handler.Replace('.', '-'))")
        $null = New-LambdaPSFunctionProject -Path $projectPath -Handler $Handler -Target $Target -PowerShellVersion '7.4'

        $script = [System.IO.Path]::Combine($projectPath, 'myfn.ps1')
        Test-Path -LiteralPath $script -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($projectPath, 'handler.ps1')) | Should -BeFalse
        Test-Path -LiteralPath ([System.IO.Path]::Combine($projectPath, 'myfn.ps1.ps1')) | Should -BeFalse
        Get-Content -LiteralPath $script -Raw | Should -Match 'param\(\s*\$LambdaInput,\s*\$LambdaContext\s*\)'
    }

    # Lambda runs on Linux, where Handler.ps1 and handler.ps1 are different files.
    It 'keeps the case of -Handler Handler for <Target>' -Skip:(-not $IsLinux) -ForEach @(
        @{ Target = 'RuntimeBound' }
        @{ Target = 'SelfContainedZip' }
        @{ Target = 'Container' }
    ) {
        $projectPath = [System.IO.Path]::Combine($TestDrive, "fn-handler-case-$Target")
        $versionArgs = if ($Target -ne 'RuntimeBound') { @{ PowerShellVersion = '7.4' } } else { @{} }
        $null = New-LambdaPSFunctionProject -Path $projectPath -Handler 'Handler' -Target $Target @versionArgs

        $names = @(Get-ChildItem -LiteralPath $projectPath -File | ForEach-Object Name)
        $names | Should -Contain 'Handler.ps1'
        $names -ccontains 'handler.ps1' | Should -BeFalse
    }

    It 'points the Container Dockerfile CMD at the renamed handler' {
        $projectPath = [System.IO.Path]::Combine($TestDrive, 'fn-handler-container-dockerfile')
        $null = New-LambdaPSFunctionProject -Path $projectPath -Handler 'myfn' -Target 'Container' -PowerShellVersion '7.4'
        $dockerfile = Get-Content -LiteralPath ([System.IO.Path]::Combine($projectPath, 'Dockerfile')) -Raw

        $dockerfile | Should -Match 'CMD \[ "myfn\.ps1" \]'
        $dockerfile | Should -Not -Match 'handler\.ps1'
    }

    It 'rejects -Handler <Handler> for <Target> before writing anything' -ForEach @(
        @{ Target = 'SelfContainedZip'; Handler = 'runtime' }
        @{ Target = 'Container'; Handler = 'runtime' }
        @{ Target = 'SelfContainedZip'; Handler = 'runtime.ps1' }
        @{ Target = 'Container'; Handler = 'runtime.ps1' }
    ) {
        $projectPath = [System.IO.Path]::Combine($TestDrive, "fn-handler-reserved-$Target-$($Handler.Replace('.', '-'))")
        { New-LambdaPSFunctionProject -Path $projectPath -Handler $Handler -Target $Target -PowerShellVersion '7.4' } |
            Should -Throw -ErrorId 'HandlerNameReserved*'
        Test-Path -LiteralPath $projectPath | Should -BeFalse
    }

    It 'still lints clean for <Target> with a renamed handler' -ForEach @(
        @{ Target = 'SelfContainedZip' }
        @{ Target = 'Container' }
    ) {
        $projectPath = [System.IO.Path]::Combine($TestDrive, "fn-handler-lint-$Target")
        $null = New-LambdaPSFunctionProject -Path $projectPath -Handler 'myfn' -Target $Target -PowerShellVersion '7.4'
        Test-LambdaPSRuntimeProject -Path $projectPath -Quiet | Should -BeTrue
    }
}

# '$' also matches before a final newline, so the handler and version patterns are anchored with \A and \z.
Describe 'New-LambdaPSFunctionProject parameter validation' -Tag 'Unit' {
    It 'rejects -<Parameter> with a trailing newline' -ForEach @(
        @{ Parameter = 'Handler'; Arguments = @{ Handler = "fn`n" } }
        @{ Parameter = 'PowerShellVersion'; Arguments = @{ Handler = 'fn'; Target = 'Container'; PowerShellVersion = "7.4`n" } }
        @{ Parameter = 'PwshVersion'; Arguments = @{ Handler = 'fn'; Target = 'Container'; PowerShellVersion = '7.4'; PwshVersion = "7.4.20`n" } }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "fn-newline-$Parameter")
        { New-LambdaPSFunctionProject -Path $projectDir @Arguments } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionProject' -ExpectedMessage "*parameter '$Parameter'*"
        $projectDir | Should -Not -Exist
    }

    # \d also matches other scripts' digits, such as Arabic-Indic ones; a version is ASCII digits only.
    It 'rejects -<Parameter> in non-ASCII digits, naming the form it takes' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Arguments = @{ Handler = 'fn'; Target = 'Container'; PowerShellVersion = "$([char]0x0667).$([char]0x0664)" }; Message = '*is not a PowerShell version in major.minor form, for example 7.4.' }
        @{ Parameter = 'PwshVersion'; Arguments = @{ Handler = 'fn'; Target = 'Container'; PowerShellVersion = '7.4'; PwshVersion = "7.4.$([char]0x0662)$([char]0x0660)" }; Message = '*is not a pwsh release in x.y.z form, for example 7.4.20.' }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "fn-digits-$Parameter")
        { New-LambdaPSFunctionProject -Path $projectDir @Arguments } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionProject' -ExpectedMessage $Message
        $projectDir | Should -Not -Exist
    }

    # Case-sensitive, so U+212A KELVIN SIGN does not pass as 'k'.
    It 'rejects the -Handler <Handler>' -ForEach @(
        @{ Handler = '1handler' }, @{ Handler = 'sub/handler' }, @{ Handler = 'handler.ps1::Invoke' }
        @{ Handler = "$([char]0x212A)handler" }, @{ Handler = "hand$([char]0x212A)ler" }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "fn-badhandler-$($Handler -replace '\W', '')")
        { New-LambdaPSFunctionProject -Path $projectDir -Handler $Handler } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionProject' -ExpectedMessage "*parameter 'Handler'*is not a handler script name*set when you deploy*"
        $projectDir | Should -Not -Exist
    }

    # The alias is recorded in manifest.json and first sent to Lambda by the deploy, which refuses a
    # name Lambda does not take. Case-sensitive, so U+212A KELVIN SIGN does not pass as 'k'.
    It 'rejects the -RuntimeAlias <Name>' -ForEach @(
        @{ Name = 'prod-'; Value = 'prod-' }, @{ Name = '.prod'; Value = '.prod' }, @{ Name = 'pro d'; Value = 'pro d' }
        @{ Name = 'a trailing newline'; Value = "prod`n" }, @{ Name = 'with a Kelvin sign'; Value = "prod$([char]0x212A)" }
        @{ Name = '129 characters'; Value = ('p' * 129) }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "fn-alias-$($Name -replace '\W', '')")
        { New-LambdaPSFunctionProject -Path $projectDir -Handler fn -RuntimeAlias $Value } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionProject' -ExpectedMessage '*is not a valid runtime alias name*'
        $projectDir | Should -Not -Exist
    }
}

Describe 'New-LambdaPSFunctionProject help' -Tag 'Unit' {
    It 'names the TargetOwnedByAnotherUser refusal under -Force' {
        (Get-Help New-LambdaPSFunctionProject -Parameter Force | Out-String) -replace '\s+', ' ' |
            Should -BeLike '*TargetOwnedByAnotherUser*'
    }
}
