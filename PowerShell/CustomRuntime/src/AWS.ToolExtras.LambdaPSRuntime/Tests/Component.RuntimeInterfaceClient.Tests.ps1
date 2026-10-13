# Tests for the PowerShell that runs INSIDE Lambda -- the runtime interface client (RIC) under
# Templates/. Build coverage only measures Public/ and Private/, and a hello-world handler run end to
# end never exercises most of the RIC, so these tests are its only automated verification. They
# dot-source the template functions and run them offline. Every target ships the one copy under
# Templates/Shared.

BeforeDiscovery {
    $templatesRoot = [System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'Templates')
    # Container has no bootstrap of its own: it scaffolds the CMR one.
    $script:Bootstraps = @(
        @{ Template = 'RuntimeImage'; Runtime = '/var/runtime/runtime.ps1' }
        @{ Template = 'LayerRuntime'; Runtime = '/opt/runtime.ps1' }
        @{ Template = 'SelfContainedZip'; Runtime = '/var/task/runtime.ps1' }
    ) | ForEach-Object { $_.Bootstrap = [System.IO.Path]::Combine($templatesRoot, $_.Template, 'bootstrap'); $_ }
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeRuntimeApi.ps1'))

    $script:templatesRoot = [System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'Templates')
    $script:Modules = [System.IO.Path]::Combine($script:templatesRoot, 'Shared', 'modules')

    # The RIC reads these from the environment; save them so the tests leave the process as they found it.
    $script:savedEnvironment = @{}
    # The runtime settings are cleared too, so a runner that sets them cannot change what is tested;
    # the child runtimes inherit this process's environment.
    foreach ($name in 'AWS_LAMBDA_RUNTIME_API', 'AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID', 'POWERSHELL_VERSION',
        'POWERSHELL_RUNTIME_JSON_DEPTH', 'POWERSHELL_RUNTIME_VERBOSE') {
        $script:savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    $env:POWERSHELL_RUNTIME_JSON_DEPTH = $env:POWERSHELL_RUNTIME_VERBOSE = $null
}

AfterAll {
    foreach ($name in $script:savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $script:savedEnvironment[$name])
    }
}

Describe 'Runtime interface client (Templates/Shared/modules)' -Tag 'Component' {
    Context 'the target bootstraps' {
        It 'only starts the shared runtime.ps1, non-interactively (<Template>)' -ForEach $script:Bootstraps {
            # Each is an sh wrapper that execs pwsh -File on it, which runs it in the global scope where
            # the handler script is dot-sourced. -NonInteractive: nothing can answer a prompt in Lambda.
            $code = Get-Content -LiteralPath $Bootstrap | Where-Object { $_ -notmatch '^\s*#' -or $_ -match '^#!' }
            $code[0] | Should -BeExactly '#!/bin/sh'
            ($code -join "`n") | Should -Match ('(?m)^exec /\S+/pwsh -NoProfile -NonInteractive -File {0}$' -f [regex]::Escape($Runtime))
            ($code -join "`n") | Should -Not -Match 'Event loop|Get-LambdaNextInvocation'
        }
    }

    # ConvertTo-Json's default depth of 2 would replace nested output with type names.
    Context 'Invoke-FunctionHandler serializes the full response' {
        It 'returns deeply nested output intact' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Invoke-FunctionHandler.ps1'))
            $handler = Join-Path $TestDrive "deep.ps1"
            Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext) @{ a = @{ b = @{ c = @{ d = @{ e = 5 } } } } }'

            $json = Invoke-FunctionHandler ([PSCustomObject]@{ incomingEvent = '{}' }) `
                ([PSCustomObject]@{ handlerType = 'Script'; scriptFilePath = $handler }) ([PSCustomObject]@{}) 3>$null

            $json | Should -Not -Match 'System\.Collections\.Hashtable'
            ($json | ConvertFrom-Json).a.b.c.d.e | Should -Be 5
        }

        It 'lowers the depth to POWERSHELL_RUNTIME_JSON_DEPTH when it is set' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Invoke-FunctionHandler.ps1'))
            $handler = Join-Path $TestDrive "shallow.ps1"
            Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext) @{ a = @{ b = @{ c = 1 } } }'
            $env:POWERSHELL_RUNTIME_JSON_DEPTH = '1'
            try {
                $json = Invoke-FunctionHandler ([PSCustomObject]@{ incomingEvent = '{}' }) `
                    ([PSCustomObject]@{ handlerType = 'Script'; scriptFilePath = $handler }) ([PSCustomObject]@{}) 3>$null
            }
            finally { $env:POWERSHELL_RUNTIME_JSON_DEPTH = $null }

            $json | Should -Match 'System\.Collections\.Hashtable'
        }

        It 'uses the full depth when POWERSHELL_RUNTIME_JSON_DEPTH is ''<Value>''' -ForEach @(
            @{ Value = '0' }, @{ Value = '101' }, @{ Value = '-1' }, @{ Value = 'abc' }, @{ Value = ' ' }
        ) {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Invoke-FunctionHandler.ps1'))
            $handler = Join-Path $TestDrive "deep.ps1"
            Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext) @{ a = @{ b = @{ c = @{ d = @{ e = 5 } } } } }'
            $env:POWERSHELL_RUNTIME_JSON_DEPTH = $Value
            try {
                $json = Invoke-FunctionHandler ([PSCustomObject]@{ incomingEvent = '{}' }) `
                    ([PSCustomObject]@{ handlerType = 'Script'; scriptFilePath = $handler }) ([PSCustomObject]@{}) 3>$null
            }
            finally { $env:POWERSHELL_RUNTIME_JSON_DEPTH = $null }

            ($json | ConvertFrom-Json).a.b.c.d.e | Should -Be 5
        }

        It 'passes a JSON array event as an array, keeping <Event> intact' -ForEach @(
            @{ Event = '[]'; Count = 0 }
            @{ Event = '[{"x":1}]'; Count = 1 }
        ) {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Invoke-FunctionHandler.ps1'))
            $handler = Join-Path $TestDrive "array-$Count.ps1"
            Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext) @{ IsArray = $LambdaInput -is [array]; Count = @($LambdaInput).Count }'

            $json = Invoke-FunctionHandler ([PSCustomObject]@{ incomingEvent = $Event }) `
                ([PSCustomObject]@{ handlerType = 'Script'; scriptFilePath = $handler }) ([PSCustomObject]@{})

            $result = $json | ConvertFrom-Json
            $result.IsArray | Should -BeTrue
            $result.Count | Should -Be $Count
        }

        It 'sends <Value> as JSON, not as PowerShell text' -ForEach @(
            @{ Value = '$false'; Expected = 'false' }
            @{ Value = '0'; Expected = '0' }
        ) {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Invoke-FunctionHandler.ps1'))
            $handler = Join-Path $TestDrive "falsy-$Expected.ps1"
            Set-Content -LiteralPath $handler -Value "param(`$LambdaInput, `$LambdaContext) $Value"

            $json = Invoke-FunctionHandler ([PSCustomObject]@{ incomingEvent = '{}' }) `
                ([PSCustomObject]@{ handlerType = 'Script'; scriptFilePath = $handler }) ([PSCustomObject]@{})

            $json | Should -BeExactly $Expected
        }
    }

    # A rejected post must surface: otherwise the invoke stays open until 'Task timed out' with no
    # cause in the log.
    Context 'posting to the Runtime API' {
        BeforeEach {
            $env:AWS_LAMBDA_RUNTIME_API = '127.0.0.1:9001'
            $env:AWS_LAMBDA_RUNTIME_AWS_REQUEST_ID = 'req-1'
            # Set by the bootstrap in Lambda; the request's User-Agent is built from it.
            $env:POWERSHELL_VERSION = $PSVersionTable.PSVersion.ToString()
        }

        It 'accepts a 202 without error' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', '_SendRuntimeApiRequest.ps1'))
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Send-FunctionHandlerResponse.ps1'))
            $fake = [LambdaPSTests.Fakes.FixedStatusHandler]::new(202, '{"status":"OK"}')
            $client = [System.Net.Http.HttpClient]::new($fake)

            # Called directly, not inside a { } block: the RIC functions are 'private:', so a child
            # scope such as a Should script block cannot see them.
            Send-FunctionHandlerResponse $client '{"ok":true}'
            $fake.Calls | Should -Be 1
            $fake.LastUri | Should -Match '/runtime/invocation/req-1/response$'
        }

        It 'throws, naming the status, when the response is rejected' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', '_SendRuntimeApiRequest.ps1'))
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Send-FunctionHandlerResponse.ps1'))
            $fake = [LambdaPSTests.Fakes.FixedStatusHandler]::new(413, '{"errorType":"RequestEntityTooLarge"}')
            $client = [System.Net.Http.HttpClient]::new($fake)

            # The bootstrap sends the response inside a try whose catch posts to /error, so throwing
            # here turns a silent timeout into a reported invocation error.
            $caught = $null
            try { Send-FunctionHandlerResponse $client '{"big":true}' } catch { $caught = $_ }
            $caught | Should -Not -BeNullOrEmpty
            $caught.Exception.Message | Should -BeLike '*HTTP 413*RequestEntityTooLarge*'
        }

        It 'logs, rather than dies, when the error report itself is rejected' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', '_SendRuntimeApiRequest.ps1'))
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Send-FunctionHandlerError.ps1'))
            $fake = [LambdaPSTests.Fakes.FixedStatusHandler]::new(500, 'boom')
            $client = [System.Net.Http.HttpClient]::new($fake)
            try { throw 'handler failed' } catch { $record = $_ }

            $output = Send-FunctionHandlerError $client $record 6>&1 | Out-String
            $output | Should -Match 'Failed to report the error'
            $output | Should -Match 'HTTP 500'
            $fake.LastUri | Should -Match '/runtime/invocation/req-1/error$'
        }

        It 'logs where the handler failed, not just the message' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', '_SendRuntimeApiRequest.ps1'))
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Send-FunctionHandlerError.ps1'))
            $client = [System.Net.Http.HttpClient]::new([LambdaPSTests.Fakes.FixedStatusHandler]::new(202, ''))
            $handler = Join-Path $TestDrive "throws.ps1"
            Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)', '', 'throw "boom"'
            try { & $handler @{} @{} } catch { $record = $_ }

            $output = Send-FunctionHandlerError $client $record 6>&1 | Out-String
            $output | Should -Match ([regex]::Escape("throws.ps1"))
            $output | Should -Match 'throws\.ps1:3'
        }
    }

    Context 'getting the next invocation' {
        BeforeEach {
            $env:AWS_LAMBDA_RUNTIME_API = '127.0.0.1:9001'
            $env:POWERSHELL_VERSION = $PSVersionTable.PSVersion.ToString()
        }

        It 'returns the event on success' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Get-LambdaNextInvocation.ps1'))
            $client = [System.Net.Http.HttpClient]::new([LambdaPSTests.Fakes.FixedStatusHandler]::new(200, '{"key":"value"}'))

            $next = Get-LambdaNextInvocation $client
            $next.incomingEvent | Should -Be '{"key":"value"}'
        }

        It 'throws, naming the status, when the Runtime API answers with an error' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Get-LambdaNextInvocation.ps1'))
            $client = [System.Net.Http.HttpClient]::new([LambdaPSTests.Fakes.FixedStatusHandler]::new(500, '{"errorType":"Runtime.ExitError"}'))

            # The event loop exits 1 on this throw, as Lambda asks of a runtime after a /next error.
            $caught = $null
            try { $null = Get-LambdaNextInvocation $client } catch { $caught = $_ }
            $caught.Exception.Message | Should -BeLike '*HTTP 500*Runtime.ExitError*'
        }

        It 'returns nothing when the Runtime API cannot be reached, so the loop asks again' {
            . ([System.IO.Path]::Combine($script:Modules, 'Private', 'Get-LambdaNextInvocation.ps1'))
            $client = [System.Net.Http.HttpClient]::new([LambdaPSTests.Fakes.UnreachableHandler]::new())

            $next = Get-LambdaNextInvocation $client 6>$null
            $next | Should -BeNullOrEmpty
        }
    }

    # Set-PSModulePath derives the runtime tier from where it is installed, so run it from a copy of
    # the modules directory at a known path rather than from the template tree.
    #
    # Skipped on Windows: this is Lambda-side code that only ever runs on Linux. It separates the
    # tiers with ':' and builds them with [System.IO.Path]::Combine, so on Windows the result is
    # '/var/task\modules' joined by a character that also follows every drive letter -- the value is
    # correct for the host it is read on, just not the one these assertions describe.
    Context 'Set-PSModulePath' -Skip:$IsWindows {
        BeforeAll {
            $script:installRoot = Join-Path $TestDrive 'runtime'
            $null = New-Item -ItemType Directory -Path $script:installRoot -Force
            Copy-Item -LiteralPath $script:Modules -Destination $script:installRoot -Recurse
            $script:installedModules = [System.IO.Path]::Combine($script:installRoot, 'modules')
        }

        BeforeEach {
            $script:savedModulePath = $env:PSModulePath
            $script:savedTaskRoot = $env:LAMBDA_TASK_ROOT
        }

        AfterEach {
            $env:PSModulePath = $script:savedModulePath
            $env:LAMBDA_TASK_ROOT = $script:savedTaskRoot
        }

        It 'puts the function, layer, runtime and pwsh tiers in that order' {
            # The image targets: the runtime's modules are in their own directory (/var/runtime/modules).
            $env:LAMBDA_TASK_ROOT = '/var/task'
            . ([System.IO.Path]::Combine($script:installedModules, 'Private', 'Set-PSModulePath.ps1'))
            Set-PSModulePath

            $env:PSModulePath -split ':' | Should -Be @(
                '/var/task/modules'
                '/opt/modules'
                $script:installedModules
                [System.IO.Path]::Combine($PSHOME, 'Modules')
            )
        }

        It 'lists the runtime tier once when it is also the function tier' {
            # The self-contained zip: the runtime ships inside the function package, so its modules
            # directory is LAMBDA_TASK_ROOT/modules. The layer target dedupes against /opt/modules the same way.
            $env:LAMBDA_TASK_ROOT = $script:installRoot
            . ([System.IO.Path]::Combine($script:installedModules, 'Private', 'Set-PSModulePath.ps1'))
            Set-PSModulePath

            $env:PSModulePath -split ':' | Should -Be @(
                $script:installedModules
                '/opt/modules'
                [System.IO.Path]::Combine($PSHOME, 'Modules')
            )
        }
    }

    # 'Set-PSDebug -Strict' is process-wide, so in the runtime module it would break customer handlers
    # that read a never-set variable. Run in a child process so strict mode cannot leak into Pester.
    Context 'strict mode is not imposed on handlers' {
        It 'lets handler code read an unset variable after the runtime module loads' {
            $manifest = [System.IO.Path]::Combine($script:Modules, 'pwsh-runtime.psd1')
            $command = "Import-Module '$manifest'; if (`$neverSet) { 'set' } else { 'ok' }"
            $pwshPath = [System.IO.Path]::Combine($PSHOME, 'pwsh')
            $result = & $pwshPath -NoProfile -NonInteractive -Command $command 2>&1

            ($result | Out-String).Trim() | Should -Be 'ok'
        }

        It 'does not call Set-PSDebug -Strict' {
            $code = Get-Content -LiteralPath ([System.IO.Path]::Combine($script:Modules, 'pwsh-runtime.psm1')) |
                Where-Object { $_ -notmatch '^\s*#' }
            ($code -join "`n") | Should -Not -Match 'Set-PSDebug'
        }
    }
}

# runtime.ps1 itself, run in a child pwsh against a fake Runtime API: the handler forms, the
# per-invocation loop, the error route, and the handler's own variables. It runs from
# Templates/Shared, which has no precompiled PowerShellLambdaContext.dll, so every run also takes the
# fallback that compiles PowerShellLambdaContext.cs at start-up.
Describe 'runtime.ps1 event loop' -Tag 'Component' -Skip:$IsWindows {
    BeforeAll {
        $script:taskRoot = [System.IO.Path]::Combine($TestDrive, 'task')
        $null = New-Item -ItemType Directory -Path $script:taskRoot -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'script.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '@{ Name = $LambdaInput.name; RequestId = $LambdaContext.AwsRequestId }'
        # A handler script whose own $httpClient and $handlerArray the runtime must leave alone.
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'fn.ps1')) -Value @(
            '$httpClient = ''mine'''
            '$handlerArray = ''mine too'''
            'function Invoke { param($LambdaInput, $LambdaContext) @{ HttpClient = $httpClient; HandlerArray = $handlerArray; N = $LambdaInput.n } }'
            'function Fail { param($LambdaInput, $LambdaContext) throw ''boom'' }'
            'function Forge { param($LambdaInput, $LambdaContext) throw "bad name: $($LambdaInput.name)" }'
            'filter Handle-Filter { param($LambdaInput, $LambdaContext) @{ Kind = ''filter''; N = $LambdaInput.n } }'
            'Set-Alias -Name Handle-Alias -Value Handle-Filter'
            'Set-Alias -Name Chain-Alias -Value Handle-Alias'
            'Set-Alias -Name Dangling-Alias -Value Invoke-Typo'
            'Set-Alias -Name AutoLoad-Alias -Value Get-AutoThing'
            'Set-Alias -Name Script-Alias -Value ([System.IO.Path]::Combine($PSScriptRoot, ''other.ps1''))'
            'Set-Alias -Name Runtime-Alias -Value Set-LambdaContext'
            'Set-Alias -Name Exe-Alias -Value true'
            'function Break-Out { param($LambdaInput, $LambdaContext) $r = 1..3 | ForEach-Object { if ($_ -eq 2) { break }; $_ }; "result:$r" }'
            'function Continue-Out { param($LambdaInput, $LambdaContext) 1..3 | ForEach-Object { if ($_ -eq 2) { continue } }; ''never'' }'
            'function Labelled-Out { param($LambdaInput, $LambdaContext) 1..3 | ForEach-Object { if ($_ -eq 2) { break nowhere } }; ''never'' }'
            'function Exit-Out { param($LambdaInput, $LambdaContext) exit 0 }'
            'function Labelled-Own { param($LambdaInput, $LambdaContext) $n = 0; :outer foreach ($i in 1..3) { foreach ($j in 1..3) { if ($j -eq 2) { continue outer }; if ($i -eq 3) { break outer }; $n += $i } }; $n }'
            'function Loop-Own { param($LambdaInput, $LambdaContext) $n = 0; foreach ($i in 1..3) { if ($i -eq 2) { continue }; if ($i -eq 3) { break }; $n += $i }; $n }'
            'function Warn { param($LambdaInput, $LambdaContext) Write-Warning ''careful''; Write-Error ''soft'' -ErrorAction Continue; ''ok'' }'
        )
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'context.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '@{ Arn = $LambdaContext.InvokedFunctionArn; Left = $LambdaContext.GetRemainingTimeInMillis(); Temp = $env:TEMP; TmpDir = $env:TMPDIR }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'chatty.ps1')) -Value 'param($LambdaInput, $LambdaContext)', 'Write-Verbose ''HANDLER-VERBOSE''', '@{ Name = $LambdaInput.name }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'slow.ps1')) -Value 'param($LambdaInput, $LambdaContext)', 'Start-Sleep -Seconds 60'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'breakscript.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '1..3 | ForEach-Object { if ($_ -eq 2) { break } }', '''never'''
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'quiet.ps1')) -Value '$ErrorActionPreference = ''SilentlyContinue''', 'function Handle { param($LambdaInput, $LambdaContext) ''ok'' }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'exitscript.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '''done''', 'exit 0'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'topbreak.ps1')) -Value '1..3 | ForEach-Object { if ($_ -eq 2) { break } }', 'function Handle { param($LambdaInput, $LambdaContext) ''never'' }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'toplabel.ps1')) -Value '1..3 | ForEach-Object { if ($_ -eq 2) { break nowhere } }', 'function Handle { param($LambdaInput, $LambdaContext) ''never'' }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'toploop.ps1')) -Value '$script:seen = foreach ($i in 1..3) { if ($i -eq 2) { break }; $i }', 'function Handle { param($LambdaInput, $LambdaContext) "loaded:$script:seen" }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'other.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '@{ Kind = ''script file''; N = $LambdaInput.n }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'nested.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '@{ a = @{ b = @{ c = @{ d = @{ e = 5 } } } } }'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'trace.ps1')) -Value 'param($LambdaInput, $LambdaContext)', '[string]$env:_X_AMZN_TRACE_ID'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'syntax.ps1')) -Value 'function Handle {'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'initthrows.ps1')) -Value 'throw ''init failed''', 'function Handle { ''unreachable'' }'
        $moduleRoot = [System.IO.Path]::Combine($script:taskRoot, 'modules', 'MyMod')
        $null = New-Item -ItemType Directory -Path $moduleRoot -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($moduleRoot, 'MyMod.psm1')) -Value 'function Invoke-My { param($LambdaInput, $LambdaContext) @{ From = ''module''; N = $LambdaInput.n } }'
        foreach ($module in @(
                @{ Name = 'BreakMod'; Code = '1..3 | ForEach-Object { if ($_ -eq 2) { break } }', 'function Handle { param($LambdaInput, $LambdaContext) ''never'' }' }
                @{ Name = 'LabelMod'; Code = '1..3 | ForEach-Object { if ($_ -eq 2) { break nowhere } }', 'function Handle { param($LambdaInput, $LambdaContext) ''never'' }' }
                @{ Name = 'LoopMod'; Code = '$script:seen = foreach ($i in 1..3) { if ($i -eq 2) { break }; $i }', 'function Handle { param($LambdaInput, $LambdaContext) "loopmod:$script:seen" }' }
            )) {
            $root = [System.IO.Path]::Combine($script:taskRoot, 'modules', $module.Name)
            $null = New-Item -ItemType Directory -Path $root -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($root, "$($module.Name).psm1")) -Value $module.Code
        }
        # A module no handler imports: its function loads on first use.
        $autoModuleRoot = [System.IO.Path]::Combine($script:taskRoot, 'modules', 'AutoLoad')
        $null = New-Item -ItemType Directory -Path $autoModuleRoot -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($autoModuleRoot, 'AutoLoad.psm1')) -Value 'function Get-AutoThing { param($LambdaInput, $LambdaContext) @{ Kind = ''auto-loaded''; N = $LambdaInput.n } }'
        $aliasModuleRoot = [System.IO.Path]::Combine($script:taskRoot, 'modules', 'AliasMod')
        $null = New-Item -ItemType Directory -Path $aliasModuleRoot -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($aliasModuleRoot, 'AliasMod.psm1')) -Value @(
            'function Invoke-Real { param($LambdaInput, $LambdaContext) @{ Kind = ''module alias''; N = $LambdaInput.n } }'
            'Set-Alias -Name Handle -Value Invoke-Real'
            'Export-ModuleMember -Function Invoke-Real -Alias Handle'
        )
        # A binary module: its handler is a compiled cmdlet.
        $binaryModuleRoot = [System.IO.Path]::Combine($script:taskRoot, 'modules', 'MyBin')
        $null = New-Item -ItemType Directory -Path $binaryModuleRoot -Force
        Add-Type -OutputAssembly ([System.IO.Path]::Combine($binaryModuleRoot, 'MyBin.dll')) -TypeDefinition @'
using System.Management.Automation;
[Cmdlet("Invoke", "Bin")]
public class InvokeBin : PSCmdlet {
    [Parameter(Position = 0)] public object LambdaInput;
    [Parameter(Position = 1)] public object LambdaContext;
    protected override void ProcessRecord() { WriteObject("cmdlet"); }
}
'@
    }

    It 'answers each event of a Script handler on its own /response, with that invocation''s context' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{"name":"a"}', '{"name":"b"}')

        $posts.Count | Should -Be 2
        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $posts[1].Path | Should -Be '/2018-06-01/runtime/invocation/req-2/response'
        ($posts[0].Body | ConvertFrom-Json).Name | Should -Be 'a'
        ($posts[1].Body | ConvertFrom-Json).RequestId | Should -Be 'req-2'
    }

    It 'runs a Function handler and leaves the handler script''s own variables alone' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'fn.ps1::Invoke' -EventBody '{"n":1}', '{"n":2}')

        $posts.Count | Should -Be 2
        $result = $posts[1].Body | ConvertFrom-Json
        $result.HttpClient | Should -Be 'mine'
        $result.HandlerArray | Should -Be 'mine too'
        $result.N | Should -Be 2
    }

    It 'gives the handler the invocation''s ARN, time left and /tmp' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'context.ps1' -EventBody '{}')

        $result = $posts[0].Body | ConvertFrom-Json
        $result.Arn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:function:fn'
        # The fake Runtime API sets the deadline five minutes after it serves the event.
        $result.Left | Should -BeGreaterThan 0
        $result.Left | Should -BeLessOrEqual 300000
        $result.Temp | Should -Be '/tmp'
        $result.TmpDir | Should -Be '/tmp'
    }

    # The handler is called with '&', so every command kind '&' runs is a handler, not only a function.
    It 'runs a <Kind> handler' -ForEach @(
        @{ Kind = 'filter'; Handler = 'fn.ps1::Handle-Filter'; Expected = '*"Kind":"filter"*' }
        @{ Kind = 'script alias'; Handler = 'fn.ps1::Handle-Alias'; Expected = '*"Kind":"filter"*' }
        @{ Kind = 'alias to an alias'; Handler = 'fn.ps1::Chain-Alias'; Expected = '*"Kind":"filter"*' }
        @{ Kind = 'auto-loaded module function'; Handler = 'fn.ps1::Get-AutoThing'; Expected = '*"Kind":"auto-loaded"*' }
        @{ Kind = 'alias to an auto-loaded module function'; Handler = 'fn.ps1::AutoLoad-Alias'; Expected = '*"Kind":"auto-loaded"*' }
        @{ Kind = 'alias to a script file'; Handler = 'fn.ps1::Script-Alias'; Expected = '*"Kind":"script file"*' }
        @{ Kind = 'module alias'; Handler = 'Module::AliasMod::Handle'; Expected = '*"Kind":"module alias"*' }
        @{ Kind = 'cmdlet'; Handler = 'Module::MyBin::Invoke-Bin'; Expected = 'cmdlet' }
    ) {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler $Handler -EventBody '{"n":3}' -TimeoutSeconds 30)

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $posts[0].Body | Should -BeLike $Expected
    }

    It 'runs a Module handler from the function''s modules directory' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'Module::MyMod::Invoke-My' -EventBody '{"n":7}')

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $result = $posts[0].Body | ConvertFrom-Json
        $result.From | Should -Be 'module'
        $result.N | Should -Be 7
    }

    # Lambda then reports the cause on the invoke, instead of a handler that is 'not recognized' on
    # every event or a runtime that exits with nothing said.
    It 'reports a start-up failure (<Case>) to /runtime/init/error' -ForEach @(
        @{ Case = 'missing script'; Handler = 'missing.ps1::Handle'; Message = '*Handler script not found*missing.ps1*'; Type = 'FileNotFoundException'; Stack = $null }
        @{ Case = 'missing Script handler'; Handler = 'missing.ps1'; Message = '*Handler script not found*missing.ps1*'; Type = 'FileNotFoundException'; Stack = $null }
        @{ Case = 'syntax error'; Handler = 'syntax.ps1::Handle'; Message = '*Missing closing*'; Type = 'ParseException'; Stack = $null }
        @{ Case = 'syntax error in a Script handler'; Handler = 'syntax.ps1'; Message = '*Missing closing*'; Type = 'ParseException'; Stack = $null }
        @{ Case = 'script throws'; Handler = 'initthrows.ps1::Handle'; Message = 'init failed'; Type = 'RuntimeException'; Stack = '*initthrows.ps1*' }
        @{ Case = 'invalid _HANDLER'; Handler = 'nonsense'; Message = "Invalid Lambda Handler: _HANDLER is 'nonsense'. Use one of <script>.ps1, <script>.ps1::<function> or Module::<module>::<function>."; Type = 'RuntimeException'; Stack = $null }
        @{ Case = 'missing module'; Handler = 'Module::NoSuchMod::Handle'; Message = '*NoSuchMod*'; Type = 'FileNotFoundException'; Stack = $null }
        @{ Case = 'misspelled function'; Handler = 'fn.ps1::Invok'; Message = 'Handler function not found: Invok*'; Type = 'CommandNotFoundException'; Stack = $null }
        @{ Case = 'misspelled module function'; Handler = 'Module::MyMod::Invoke-Typo'; Message = 'Handler function not found: Invoke-Typo*'; Type = 'CommandNotFoundException'; Stack = $null }
        @{ Case = 'whitespace _HANDLER'; Handler = ' '; Message = "Invalid Lambda Handler: _HANDLER is ' '. Use one of <script>.ps1, <script>.ps1::<function> or Module::<module>::<function>."; Type = 'RuntimeException'; Stack = $null }
        @{ Case = 'empty function name'; Handler = 'fn.ps1::'; Message = "Invalid Lambda Handler: _HANDLER is 'fn.ps1::'. Use one of <script>.ps1, <script>.ps1::<function> or Module::<module>::<function>."; Type = 'RuntimeException'; Stack = $null }
        @{ Case = 'empty module function name'; Handler = 'Module::MyMod::'; Message = "Invalid Lambda Handler: _HANDLER is 'Module::MyMod::'. Use one of <script>.ps1, <script>.ps1::<function> or Module::<module>::<function>."; Type = 'RuntimeException'; Stack = $null }
        @{ Case = 'wildcard function name'; Handler = 'fn.ps1::Inv*'; Message = 'Handler function not found: Inv*'; Type = 'CommandNotFoundException'; Stack = $null }
        @{ Case = 'runtime function as handler'; Handler = 'fn.ps1::Set-LambdaContext'; Message = 'Handler function not found: Set-LambdaContext*'; Type = 'CommandNotFoundException'; Stack = $null }
        @{ Case = 'break in top-level code'; Handler = 'topbreak.ps1::Handle'; Message = "The handler script's top-level code used break or continue outside a loop of its own*topbreak.ps1"; Type = 'InvalidOperationException'; Stack = $null }
        @{ Case = 'labelled jump in top-level code'; Handler = 'toplabel.ps1::Handle'; Message = "The handler's start-up code ended the runtime: it used break or continue with a label that matches no loop of its own."; Type = 'InvalidOperationException'; Stack = $null }
        @{ Case = 'break in a module''s top-level code'; Handler = 'Module::BreakMod::Handle'; Message = "The handler module's top-level code used break or continue outside a loop of its own*BreakMod"; Type = 'InvalidOperationException'; Stack = $null }
        @{ Case = 'labelled jump in a module''s top-level code'; Handler = 'Module::LabelMod::Handle'; Message = "The handler's start-up code ended the runtime: it used break or continue with a label that matches no loop of its own."; Type = 'InvalidOperationException'; Stack = $null }
        @{ Case = 'alias to nothing'; Handler = 'fn.ps1::Dangling-Alias'; Message = "Handler alias Dangling-Alias resolves to 'Invoke-Typo', which was not found (_HANDLER fn.ps1::Dangling-Alias)"; Type = 'CommandNotFoundException'; Stack = $null }
        @{ Case = 'alias to a runtime function'; Handler = 'fn.ps1::Runtime-Alias'; Message = "Handler alias Runtime-Alias resolves to 'Set-LambdaContext', one of the runtime's own functions (_HANDLER fn.ps1::Runtime-Alias)"; Type = 'CommandNotFoundException'; Stack = $null }
        @{ Case = 'alias to an executable'; Handler = 'fn.ps1::Exe-Alias'; Message = "Handler alias Exe-Alias resolves to 'true' (Application), not a function, filter, cmdlet or script file (_HANDLER fn.ps1::Exe-Alias)"; Type = 'CommandNotFoundException'; Stack = $null }
    ) {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler $Handler -EventBody '{}' -TimeoutSeconds 30 -ExpectExit)

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/init/error'
        $errorBody = $posts[0].Body | ConvertFrom-Json
        $errorBody.errorMessage | Should -BeLike $Message
        $errorBody.errorType | Should -Be $Type
        # An error the runtime raised itself has only the runtime's frames, so none are sent; an error
        # from the handler's own code keeps its stack.
        if ($Stack) { $errorBody.stackTrace[0] | Should -BeLike $Stack }
        else { @($errorBody.stackTrace).Count | Should -Be 0 }
    }

    # The helper's own contract, which needs a real runtime: with -ExpectExit and no -NextStatus it
    # leaves /next unanswered, so a runtime that keeps running fails on the timeout, not on a bad status.
    It 'fails a loop run with -ExpectExit when the runtime keeps running' {
        { Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{}' -ExpectExit -TimeoutSeconds 8 } |
            Should -Throw '*expected to exit, but was still running after 8 seconds*'
    }

    # Without it, a run that ends at the timeout would return only the posts so far, and a test that
    # counted them would fail on a count rather than say the runtime never finished.
    It 'fails a loop run, saying how many events are still open, when the runtime has not finished them by the timeout' {
        { Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'slow.ps1' -EventBody '{}' -TimeoutSeconds 5 } |
            Should -Throw '*still had 1 of 1 event(s) open after 5 seconds*'
    }

    # A timed-out run is diagnosed from what the runtime printed, so both timeouts carry it.
    It 'puts the runtime''s output in the timeout error and in -LogVariable (<Name>)' -ForEach @(
        @{ Name = 'events open'; ExpectExit = $false; Message = '*still had 1 of 1 event(s) open after 5 seconds*' }
        @{ Name = 'expected to exit'; ExpectExit = $true; Message = '*expected to exit, but was still running after 5 seconds*' }
    ) {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'noisy-slow.ps1')) -Value 'param($LambdaInput, $LambdaContext)', 'Write-Host ''NOISY-SLOW-STARTED''', 'Start-Sleep -Seconds 60'
        $log = $null
        $err = $null
        try { $null = Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'noisy-slow.ps1' -EventBody '{}' -TimeoutSeconds 5 -ExpectExit:$ExpectExit -LogVariable log }
        catch { $err = $_ }

        $err.Exception.Message | Should -BeLike $Message
        $err.Exception.Message | Should -BeLike '*NOISY-SLOW-STARTED*'
        $log | Should -BeLike '*NOISY-SLOW-STARTED*'
    }

    # In a self-contained zip the runtime's folder is the task root, so its modules/Private folder is also
    # in the function's module path. A customer module named Private keeps its stack frames there; an
    # error the runtime raises itself still sends none.
    It 'in a self-contained zip layout, sends a stack only for the handler''s own code (<Case>)' -ForEach @(
        @{ Case = 'a module named Private that throws'; Handler = 'Module::Private::Handle'; Stack = '*Private.psm1*' }
        @{ Case = 'a missing script (runtime-raised)'; Handler = 'missing.ps1::Handle'; Stack = $null }
    ) {
        $copy = [System.IO.Path]::Combine($TestDrive, "zip-layout-$($Handler -replace '\W', '-')")
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($copy, 'Templates')), ([System.IO.Path]::Combine($copy, 'Tests')) -Force
        Copy-Item -LiteralPath ([System.IO.Path]::Combine($script:templatesRoot, 'Shared')) -Destination ([System.IO.Path]::Combine($copy, 'Templates')) -Recurse
        Copy-Item -LiteralPath ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers')) -Destination ([System.IO.Path]::Combine($copy, 'Tests')) -Recurse
        $zipRoot = [System.IO.Path]::Combine($copy, 'Templates', 'Shared')
        Set-Content -LiteralPath ([System.IO.Path]::Combine($zipRoot, 'modules', 'Private', 'Private.psm1')) -Value 'throw ''private module failed''', 'function Handle { ''never'' }'
        . ([System.IO.Path]::Combine($copy, 'Tests', 'Helpers', 'FakeRuntimeApi.ps1'))

        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $zipRoot -Handler $Handler -EventBody '{}' -TimeoutSeconds 30 -ExpectExit)

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/init/error'
        $errorBody = $posts[0].Body | ConvertFrom-Json
        if ($Stack) { @($errorBody.stackTrace).Where({ $_ -like $Stack }).Count | Should -BeGreaterThan 0 }
        else { @($errorBody.stackTrace).Count | Should -Be 0 }
    }

    It 'reports a runtime that cannot load its LambdaContext type to /runtime/init/error' {
        # A copy of the runtime with no PowerShellLambdaContext.dll or .cs, laid out as the helper
        # expects (Tests/Helpers beside Templates/Shared); the copied helper runs the copied runtime.
        $copy = [System.IO.Path]::Combine($TestDrive, 'no-context')
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($copy, 'Templates')), ([System.IO.Path]::Combine($copy, 'Tests')) -Force
        Copy-Item -LiteralPath ([System.IO.Path]::Combine($script:templatesRoot, 'Shared')) -Destination ([System.IO.Path]::Combine($copy, 'Templates')) -Recurse
        Copy-Item -LiteralPath ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers')) -Destination ([System.IO.Path]::Combine($copy, 'Tests')) -Recurse
        Remove-Item -LiteralPath ([System.IO.Path]::Combine($copy, 'Templates', 'Shared', 'PowerShellLambdaContext.cs'))
        . ([System.IO.Path]::Combine($copy, 'Tests', 'Helpers', 'FakeRuntimeApi.ps1'))

        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{}' -TimeoutSeconds 30 -ExpectExit)

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/init/error'
        $errorBody = $posts[0].Body | ConvertFrom-Json
        $errorBody.errorMessage | Should -BeLike 'The runtime could not load its LambdaContext type. PowerShellLambdaContext.dll: *PowerShellLambdaContext.cs: *Could not find file*'
        $errorBody.errorType | Should -Be 'InvalidOperationException'
        @($errorBody.stackTrace).Count | Should -Be 0
    }

    # PowerShell sends a break or continue that is outside the handler's own loops up the call stack;
    # the runtime must report it, not let it end the invocation early with nothing (or nothing useful) said.
    It 'reports a handler that used <Keyword> outside a loop of its own (<Handler>) on /error, then goes on' -ForEach @(
        @{ Keyword = 'break'; Handler = 'fn.ps1::Break-Out' }
        @{ Keyword = 'continue'; Handler = 'fn.ps1::Continue-Out' }
        @{ Keyword = 'break'; Handler = 'breakscript.ps1' }
    ) {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler $Handler -EventBody '{}', '{}')

        $posts.Path | Should -Be @('/2018-06-01/runtime/invocation/req-1/error', '/2018-06-01/runtime/invocation/req-2/error')
        $errorBody = $posts[0].Body | ConvertFrom-Json
        $errorBody.errorMessage | Should -BeLike '*used break or continue outside a loop of its own*'
        $errorBody.errorType | Should -Be 'InvalidOperationException'
        # The runtime raised this itself, so its own frames are not sent.
        @($errorBody.stackTrace).Count | Should -Be 0
    }

    # exit, or a labelled jump that matches none of the handler's loops, goes past Invoke-FunctionHandler's
    # guard and ends the runtime; Lambda must still be told why.
    It 'reports a handler that ended the runtime (<Handler>) on /error, then exits' -ForEach @(
        @{ Handler = 'fn.ps1::Labelled-Out' }
        @{ Handler = 'fn.ps1::Exit-Out' }
    ) {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler $Handler -EventBody '{}' -ExpectExit -TimeoutSeconds 30)

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/error'
        $errorBody = $posts[0].Body | ConvertFrom-Json
        $errorBody.errorMessage | Should -BeLike 'The handler ended the runtime*'
        $errorBody.errorType | Should -Be 'InvalidOperationException'
    }

    It 'leaves a labelled break and continue that match the handler''s own loops alone' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'fn.ps1::Labelled-Own' -EventBody '{}', '{}')

        $posts.Path | Should -Be @('/2018-06-01/runtime/invocation/req-1/response', '/2018-06-01/runtime/invocation/req-2/response')
        $posts[0].Body | Should -Be '3'
    }

    It 'lets a Script handler exit, which ends only its own script' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'exitscript.ps1' -EventBody '{}', '{}')

        $posts.Path | Should -Be @('/2018-06-01/runtime/invocation/req-1/response', '/2018-06-01/runtime/invocation/req-2/response')
        $posts[0].Body | Should -Be 'done'
    }

    It 'loads a handler module whose top-level code breaks out of a loop of its own' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'Module::LoopMod::Handle' -EventBody '{}')

        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $posts[0].Body | Should -Be 'loopmod:1'
    }

    It 'loads a handler script whose top-level code breaks out of a loop of its own' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'toploop.ps1::Handle' -EventBody '{}')

        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $posts[0].Body | Should -Be 'loaded:1'
    }

    It 'reports an event it cannot convert from JSON in its own words, without its own stack frames' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{"id":1,"ID":2}', '{"name":"b"}')

        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/error'
        $errorBody = $posts[0].Body | ConvertFrom-Json
        $errorBody.errorMessage | Should -BeLike 'The event could not be converted from JSON, so the handler was not called: *different casing*'
        $errorBody.errorMessage | Should -Not -BeLike '*AsHashTable*'
        $errorBody.errorType | Should -Be 'ArgumentException'
        @($errorBody.stackTrace).Count | Should -Be 0
        # The next event, which converts, is answered as usual.
        $posts[1].Path | Should -Be '/2018-06-01/runtime/invocation/req-2/response'
    }

    It 'leaves break and continue inside the handler''s own loop alone' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'fn.ps1::Loop-Own' -EventBody '{}')

        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $posts[0].Body | Should -Be '1'
    }

    It 'writes warnings and errors to the function log as plain text, with no ANSI colour codes' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'fn.ps1::Warn' -EventBody '{}' -LogVariable log)

        $posts.Count | Should -Be 1
        $log | Should -BeLike '*careful*'
        $log | Should -BeLike '*soft*'
        $log.Contains([char]27) | Should -BeFalse
    }

    It 'posts a handler''s exception to /error and goes on to the next event' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'fn.ps1::Fail' -EventBody '{}', '{}')

        $posts.Count | Should -Be 2
        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/error'
        $errorBody = $posts[0].Body | ConvertFrom-Json
        $errorBody.errorMessage | Should -Be 'boom'
        $errorBody.errorType | Should -Be 'RuntimeException'
        $errorBody.stackTrace | Should -Not -BeNullOrEmpty
        $errorBody.stackTrace[0] | Should -BeLike 'at Fail, *fn.ps1: line *'
        $posts[1].Path | Should -Be '/2018-06-01/runtime/invocation/req-2/error'
    }

    It 'logs a handler error as one line, so its text cannot add lines to the function log' {
        $forged = 'x' + "`n" + 'END RequestId: req-1' + "`n" + '[RUNTIME]forged line'
        $eventBody = ConvertTo-Json -Compress -InputObject @{ name = $forged }
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'fn.ps1::Forge' -EventBody $eventBody -LogVariable log)

        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/error'
        $lines = $log -split '\n'
        $lines | Where-Object { $_ -like 'END RequestId*' -or $_ -like '`[RUNTIME`]forged*' } | Should -BeNullOrEmpty
        # The message, where it was thrown and the stack, on the one line.
        $errorLine = @($lines | Where-Object { $_ -like 'bad name: x*' })
        $errorLine.Count | Should -Be 1
        $errorLine[0] | Should -BeLike "*`rEND RequestId: req-1`r``[RUNTIME``]forged line*fn.ps1*"
    }

    It 'reports a rejected /response on /error and goes on to the next event' {
        # 413: the response is over Lambda's payload limit. Unreported, the invoke would end as a timeout.
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{"name":"a"}', '{"name":"b"}' -ResponseStatus 413)

        $posts.Path | Should -Be @(
            '/2018-06-01/runtime/invocation/req-1/response'
            '/2018-06-01/runtime/invocation/req-1/error'
            '/2018-06-01/runtime/invocation/req-2/response'
        )
        ($posts[1].Body | ConvertFrom-Json).errorMessage | Should -BeLike '*HTTP 413*'
        ($posts[2].Body | ConvertFrom-Json).Name | Should -Be 'b'
    }

    It 'exits when the Runtime API answers /next with an error status' {
        # The helper waits for the runtime to exit by itself, and throws if it does not.
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{"name":"a"}' -NextStatus 500 -TimeoutSeconds 30 -LogVariable log -ExitCodeVariable exitCode)

        $posts.Count | Should -Be 1
        $log | Should -BeLike '*answered GET /runtime/invocation/next with HTTP 500*'
        # 1, not 0: Lambda then reports the runtime as failed and starts it again.
        $exitCode | Should -Be 1
    }

    It 'exits on a /next error status even when the handler script set $ErrorActionPreference to SilentlyContinue' {
        # The script is dot-sourced into the runtime's scope, so its preference is the runtime's too.
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'quiet.ps1::Handle' -EventBody '{}' -NextStatus 500 -TimeoutSeconds 30 -LogVariable log -ExitCodeVariable exitCode)

        $posts.Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
        $log | Should -BeLike '*answered GET /runtime/invocation/next with HTTP 500*'
        $exitCode | Should -Be 1
    }

    # An invalid setting falls back to 100 and says so once in the function log, not on every invocation.
    It 'with POWERSHELL_RUNTIME_JSON_DEPTH <Case>, serializes at depth <Depth> and warns <Warnings> time(s)' -ForEach @(
        @{ Case = '''abc'''; Value = 'abc'; Depth = 100; Warnings = 1 }
        @{ Case = '''0'''; Value = '0'; Depth = 100; Warnings = 1 }
        @{ Case = '''200'''; Value = '200'; Depth = 100; Warnings = 1 }
        @{ Case = '''2'' (valid)'; Value = '2'; Depth = 2; Warnings = 0 }
        @{ Case = 'unset'; Value = $null; Depth = 100; Warnings = 0 }
    ) {
        $savedDepth = $env:POWERSHELL_RUNTIME_JSON_DEPTH
        $env:POWERSHELL_RUNTIME_JSON_DEPTH = $Value
        try {
            $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'nested.ps1' -EventBody '{}', '{}' -LogVariable log)
        }
        finally { $env:POWERSHELL_RUNTIME_JSON_DEPTH = $savedDepth }

        $posts.Count | Should -Be 2
        foreach ($post in $posts) {
            $result = $post.Body | ConvertFrom-Json
            if ($Depth -eq 100) { $result.a.b.c.d.e | Should -Be 5 }
            else { $result.a.b | Should -Match 'System\.Collections\.Hashtable' }
        }
        $warning = "[RUNTIME-Invoke-FunctionHandler]POWERSHELL_RUNTIME_JSON_DEPTH is '$Value', which is not a whole number from 1 to 100; using 100."
        ([regex]::Matches($log, [regex]::Escape($warning))).Count | Should -Be $Warnings
        ([regex]::Matches($log, 'POWERSHELL_RUNTIME_JSON_DEPTH is ')).Count | Should -Be $Warnings
    }

    It 'sets _X_AMZN_TRACE_ID for its own invocation only' {
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'trace.ps1' -EventBody '{}', '{}' -TraceId 'Root=1-5759e988-bd862e3fe1be46a994272793', '')

        $posts[0].Body | Should -BeExactly 'Root=1-5759e988-bd862e3fe1be46a994272793'
        $posts[1].Body | Should -BeNullOrEmpty
    }

    # The first event leaves something behind; the second reports what it finds, which must be what
    # the first found.
    It 'clears what a handler leaves behind (<Case>) before the next invocation' -ForEach @(
        @{ Case = 'a running thread job'; Handler = 'leave-job.ps1'; Expected = 'jobs:0', 'jobs:0'; Code = @(
                'param($LambdaInput, $LambdaContext)'
                '$jobs = @(Get-Job).Count'
                'if ($LambdaInput.leave) { $null = Start-ThreadJob -ScriptBlock { Start-Sleep -Seconds 60 } }'
                '"jobs:$jobs"') }
        @{ Case = 'a running thread job, then an error'; Handler = 'leave-job-fail.ps1'; Expected = $null, 'jobs:0'; Code = @(
                'param($LambdaInput, $LambdaContext)'
                '$jobs = @(Get-Job).Count'
                'if ($LambdaInput.leave) { $null = Start-ThreadJob -ScriptBlock { Start-Sleep -Seconds 60 }; throw ''left a job'' }'
                '"jobs:$jobs"') }
        @{ Case = 'a thread job, with Get-Job redefined'; Handler = 'shadow-job.ps1::Handle'; Expected = 'jobs:0', 'jobs:0'; Code = @(
                'function Get-Job { throw ''shadowed'' }'
                'function Handle { param($LambdaInput, $LambdaContext) $jobs = @(Microsoft.PowerShell.Core\Get-Job).Count; if ($LambdaInput.leave) { $null = Start-ThreadJob -ScriptBlock { Start-Sleep -Seconds 60 } }; "jobs:$jobs" }') }
        @{ Case = 'a changed PSModulePath'; Handler = 'leave-path.ps1'; Expected = 'same path', 'same path'; Code = @(
                'param($LambdaInput, $LambdaContext)'
                'if ($null -eq $global:firstPath) { $global:firstPath = $env:PSModulePath }'
                '$same = $env:PSModulePath -eq $global:firstPath'
                'if ($LambdaInput.leave) { $env:PSModulePath = ''/leaked/modules:'' + $env:PSModulePath }'
                'if ($same) { ''same path'' } else { "changed path: $env:PSModulePath" }') }
        @{ Case = 'a native command''s exit code'; Handler = 'leave-exitcode.ps1'; Expected = 'code:', 'code:'; Code = @(
                'param($LambdaInput, $LambdaContext)'
                '$code = $global:LASTEXITCODE'
                'if ($LambdaInput.leave) { /bin/sh -c ''exit 7'' }'
                '"code:$code"') }
    ) {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, ($Handler -split '::')[0])) -Value $Code
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler $Handler -EventBody '{"leave":true}', '{}' -TimeoutSeconds 30)

        $posts.Count | Should -Be 2
        if ($null -eq $Expected[0]) {
            $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/error'
            ($posts[0].Body | ConvertFrom-Json).errorMessage | Should -Be 'left a job'
        }
        else { $posts[0].Body | Should -BeExactly $Expected[0] }
        $posts[1].Path | Should -Be '/2018-06-01/runtime/invocation/req-2/response'
        $posts[1].Body | Should -BeExactly $Expected[1]
    }

    It 'keeps the jobs and -Action subscribers that start-up created, and still removes a per-invocation job' {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:taskRoot, 'keep-startup.ps1')) -Value @(
            '$global:Worker = Start-ThreadJob -ScriptBlock { Start-Sleep -Seconds 120 }'
            '$global:Timer = [System.Timers.Timer]::new(1000)'
            '$null = Register-ObjectEvent -InputObject $global:Timer -EventName Elapsed -SourceIdentifier StartupTick -Action { $global:Ticks++ }'
            'function Handle { param($LambdaInput, $LambdaContext)'
            # Alive: still in the job table and not finished. A thread job can still be NotStarted when the
            # handler reads it (ThreadJob starts it on a later tick), so "Running" alone would be a race.
            '    $worker = Get-Job -Id $global:Worker.Id -ErrorAction Ignore'
            '    $alive = $null -ne $worker -and $worker.State -notin ''Completed'', ''Stopped'', ''Failed'''
            '    $state = "worker alive:$alive;subscribers:$(@(Get-EventSubscriber -SourceIdentifier StartupTick -ErrorAction Ignore).Count);jobs:$(@(Get-Job).Count)"'
            '    if ($LambdaInput.leave) { $null = Start-ThreadJob -ScriptBlock { Start-Sleep -Seconds 60 } }'
            '    $state'
            '}'
        )
        $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'keep-startup.ps1::Handle' -EventBody '{"leave":true}', '{}' -TimeoutSeconds 30)

        # Two start-up jobs (the worker and the subscriber's event job); the per-invocation job is gone.
        $posts.Body | Should -Be @('worker alive:True;subscribers:1;jobs:2', 'worker alive:True;subscribers:1;jobs:2')
    }

    It 'with POWERSHELL_RUNTIME_VERBOSE=TRUE, answers as usual and logs only readable [RUNTIME] lines' {
        $savedVerbose = $env:POWERSHELL_RUNTIME_VERBOSE
        $env:POWERSHELL_RUNTIME_VERBOSE = 'TRUE'
        try {
            $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'chatty.ps1' -EventBody '{"name":"a"}' -LogVariable log)
        }
        finally { $env:POWERSHELL_RUNTIME_VERBOSE = $savedVerbose }

        $posts.Count | Should -Be 1
        ($posts[0].Body | ConvertFrom-Json).Name | Should -Be 'a'
        $log | Should -BeLike '*`[RUNTIME`]Start event loop*'
        $log | Should -BeLike '*Received headers: *Lambda-Runtime-Aws-Request-Id*'
        $log | Should -BeLike '*Sending POST http://*/invocation/req-1/response (* characters)*'
        $log | Should -Not -BeLike '*System.Collections.Hashtable*'
        # The setting covers the runtime's own progress only: not the handler's (or its modules')
        # Write-Verbose, and not the caller's client context or identity.
        $log | Should -Not -BeLike '*HANDLER-VERBOSE*'
        $log | Should -Not -BeLike '*VERBOSE:*'
        $log | Should -BeLike '*Return LambdaContext: {*'
        $log | Should -Not -BeLike '*"ClientContext"*'
        $log | Should -Not -BeLike '*"Identity"*'
    }

    It 'reaches the Runtime API directly when the function sets HTTP_PROXY' {
        # A dead proxy: a runtime that sent its Runtime API traffic through it would get no event.
        # NO_PROXY is cleared so a runner's own loopback exemption cannot mask the bug.
        # Both spellings: .NET on Linux reads http_proxy before HTTP_PROXY.
        $savedProxy = $env:HTTP_PROXY, $env:http_proxy, $env:NO_PROXY, $env:no_proxy
        $env:HTTP_PROXY = $env:http_proxy = 'http://127.0.0.1:1'
        $env:NO_PROXY = $env:no_proxy = $null
        try {
            $posts = @(Invoke-LambdaPSRuntimeLoop -TaskRoot $script:taskRoot -Handler 'script.ps1' -EventBody '{"name":"a"}' -TimeoutSeconds 30)
        }
        finally { $env:HTTP_PROXY, $env:http_proxy, $env:NO_PROXY, $env:no_proxy = $savedProxy }

        $posts.Count | Should -Be 1
        $posts[0].Path | Should -Be '/2018-06-01/runtime/invocation/req-1/response'
    }
}

Describe 'PowerShellLambdaContext.cs' -Tag 'Component' {
    It 'compiles and reports the time left before the deadline' {
        # Compiled in a child process: a type added with Add-Type cannot be unloaded from this one.
        $source = [System.IO.Path]::Combine($script:templatesRoot, 'Shared', 'PowerShellLambdaContext.cs')
        $command = @"
Add-Type -TypeDefinition ([System.IO.File]::ReadAllText('$source'))
`$deadline = [DateTimeOffset]::UtcNow.AddSeconds(5).ToUnixTimeMilliseconds()
`$context = [Amazon.Lambda.PowerShell.Internal.LambdaContext]::new('fn', '1', 'arn:fn', 512, 'req-1', 'group', 'stream', '', '', `$deadline)
'{0}|{1}|{2}' -f `$context.FunctionName, `$context.MemoryLimitInMB, [int]`$context.GetRemainingTimeInMillis()
"@
        $pwshPath = [System.IO.Path]::Combine($PSHOME, $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }))
        $name, $memory, $remaining = ((& $pwshPath -NoProfile -NonInteractive -Command $command 2>&1) | Out-String).Trim() -split '\|'

        $name | Should -Be 'fn'
        $memory | Should -Be '512'
        [int]$remaining | Should -BeGreaterThan 3000
        [int]$remaining | Should -BeLessOrEqual 5000
    }
}

# The kernel executes the bootstrap directly, so CRLF would make the shebang '/bin/sh\r'.
Describe 'bootstrap line endings' -Tag 'Component' {
    It 'ships the <Template> bootstrap with LF line endings' -ForEach $script:Bootstraps {
        [System.IO.File]::ReadAllText($Bootstrap).Contains("`r") | Should -BeFalse
    }

    It 'pins LF for Templates/ in .gitattributes, so a Windows clone cannot turn them CRLF' {
        $repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
        $attributes = [System.IO.Path]::Combine($repoRoot, '.gitattributes')
        $attributes | Should -Exist
        Get-Content -LiteralPath $attributes | Where-Object { $_ -notmatch '^\s*#' } |
            Should -Match 'Templates/\*\*\s+.*eol=lf'
    }
}
