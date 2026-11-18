BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeTypes.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))


    # A lone handler script and a prepared folder to package.
    $script:handlerFile = Join-Path $TestDrive 'handler.ps1'
    Set-Content -LiteralPath $script:handlerFile -Value 'param($LambdaInput, $LambdaContext)'
    $script:folder = Join-Path $TestDrive 'fn'
    New-Item -ItemType Directory -Path $script:folder -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $script:folder 'script.ps1') -Value 'param($LambdaInput, $LambdaContext)'

    # A folder whose sole script is NOT named script.ps1, so a fixed default would bind Lambda to a
    # file that is not in the package, and every invoke would then fail with 'Handler script not found'.
    $script:folderOtherName = Join-Path $TestDrive 'fn-other'
    New-Item -ItemType Directory -Path $script:folderOtherName -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $script:folderOtherName 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'

    # A scaffolded-shape folder: several root scripts, disambiguated only by manifest.json.
    $script:folderManifest = Join-Path $TestDrive 'fn-manifest'
    New-Item -ItemType Directory -Path $script:folderManifest -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $script:folderManifest 'entry.ps1') -Value 'param($LambdaInput, $LambdaContext)'
    Set-Content -LiteralPath (Join-Path $script:folderManifest 'helper.ps1') -Value '# not the entry point'
    Set-Content -LiteralPath (Join-Path $script:folderManifest 'manifest.json') `
        -Value '{"HandlerString":"entry.ps1","ScriptFile":"entry.ps1"}'

    # Several root scripts and no manifest: genuinely ambiguous, so the deployer must ask rather than guess.
    $script:folderAmbiguous = Join-Path $TestDrive 'fn-ambiguous'
    New-Item -ItemType Directory -Path $script:folderAmbiguous -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $script:folderAmbiguous 'a.ps1') -Value '# a'
    Set-Content -LiteralPath (Join-Path $script:folderAmbiguous 'b.ps1') -Value '# b'
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'New-LambdaPSFunctionDeployment' -Tag 'Unit' {
    BeforeEach {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith {
            'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
        }
        # Default: Lambda reports x86_64 for the alias's runtime. Contexts that care override this.
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64') }
        # A new function unless a test says otherwise.
        Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith { $null }
        Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.FunctionPackage'; PackagePath = $OutFile; ModulesStaged = @() }
        }
        Mock 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{
                PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.PublishedFunction'
                FunctionName = $FunctionName; Arn = "arn:aws:lambda:us-east-1:111122223333:function:$FunctionName"
            }
        }
    }

    Context 'single-file source' {
        It 'defaults the handler to the script file name and forwards it to the create' {
            $splat = @{
                FunctionName = 'hello-powershell'
                ProjectPath  = $script:handlerFile
                RuntimeName  = 'my-ps'
                Role         = 'arn:aws:iam::111122223333:role/hello-role'
                Region       = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat

            $result.PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.FunctionDeployment'
            $result.Handler | Should -Be 'handler.ps1'
            $result.RuntimeArn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
            $result.RuntimeAlias | Should -Be 'default'
            $result.FunctionArn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:function:hello-powershell'
            $result.Updated | Should -BeFalse
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $HandlerScript -eq 'handler.ps1' }
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Handler -eq 'handler.ps1' }
        }

        It 'accepts -ScriptPath as an alias for -ProjectPath' {
            $splat = @{
                FunctionName = 'hello'; ScriptPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat
            $result.ProjectPath | Should -Be $script:handlerFile
        }
    }

    # The handler for a folder source is RESOLVED FROM THE FOLDER (shared with the packager via
    # Resolve-LambdaPSHandlerScript), not assumed to be 'script.ps1'. Getting this wrong does not fail
    # the deploy -- Lambda accepts a handler naming a missing script and every invoke then times out --
    # so these cases are the guard against a silent, expensive mismatch.
    Context 'folder source handler resolution' {
        It 'resolves the sole root script when it is named script.ps1' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folder
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat
            $result.Handler | Should -Be 'script.ps1'
        }

        It 'resolves the sole root script under any other name' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folderOtherName
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat

            # Not a fixed default such as 'script.ps1', which is not in the package.
            $result.Handler | Should -Be 'handler.ps1'
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Handler -eq 'handler.ps1' }
        }

        # A name starting with '.' is hidden on Linux and macOS, and found all the same.
        It 'packages a source whose name starts with ''.''' {
            $hidden = Join-Path $TestDrive '.hidden.ps1'
            Set-Content -LiteralPath $hidden -Value 'param($LambdaInput, $LambdaContext)'
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $hidden; Handler = '.hidden.ps1'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                PackagePath = (Join-Path $TestDrive 'hidden.zip')
            }

            $result = New-LambdaPSFunctionDeployment @splat

            $result.ProjectPath | Should -Be (Get-Item -LiteralPath $hidden -Force).FullName
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Path -eq $hidden }
        }

        It 'packages a folder whose name starts with ''.'', resolving its handler' {
            $hidden = Join-Path $TestDrive '.hidden-fn'
            $null = New-Item -ItemType Directory -Path $hidden -Force
            Set-Content -LiteralPath (Join-Path $hidden 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $hidden
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                PackagePath = (Join-Path $TestDrive 'hidden-folder.zip')
            }

            $result = New-LambdaPSFunctionDeployment @splat

            $result.Handler | Should -Be 'handler.ps1'
            $result.ProjectPath | Should -Be (Get-Item -LiteralPath $hidden -Force).FullName
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Path -eq $hidden }
        }

        It 'prefers the manifest.json ScriptFile when several root scripts exist' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folderManifest
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat
            $result.Handler | Should -Be 'entry.ps1'
        }

        It 'takes the runtime alias and architecture from a scaffolded project, unless they are passed' {
            $project = Join-Path $TestDrive 'scaffolded-arm'
            $null = New-LambdaPSFunctionProject -Path $project -Handler handler -RuntimeAlias staging -Architecture arm64
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64', 'arm64') }
            $splat = @{ FunctionName = 'hello'; ProjectPath = $project; RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1' }

            $result = New-LambdaPSFunctionDeployment @splat
            $result.RuntimeAlias | Should -Be 'staging'
            $result.Architecture | Should -Be 'arm64'

            $override = New-LambdaPSFunctionDeployment @splat -RuntimeAlias default -Architecture x86_64
            $override.RuntimeAlias | Should -Be 'default'
            $override.Architecture | Should -Be 'x86_64'
        }

        It 'treats the handler name as case-sensitive in a folder too' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folderOtherName; Handler = 'Handler.ps1'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,New-LambdaPSFunctionDeployment'
        }

        It 'treats the handler name as case-sensitive for a single file' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile; Handler = 'HANDLER.ps1'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,New-LambdaPSFunctionDeployment'
        }

        It 'throws HandlerNotResolved rather than guessing when the folder is ambiguous' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folderAmbiguous
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'HandlerNotResolved,New-LambdaPSFunctionDeployment'
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    # An explicit -Handler is honored verbatim, but it is still checked against the source: Lambda will
    # happily create a function whose handler script does not exist, and the failure mode is a timeout
    # with nothing in the log naming the cause.
    Context 'handler script existence check' {
        It 'throws HandlerScriptNotFound when an explicit handler names a script not in a folder source' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folder
                Handler = 'nope.ps1'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $err = { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,New-LambdaPSFunctionDeployment' -PassThru
            # The runtime reports a missing script at start-up, so that is what the message says invokes would do.
            $err.Exception.Message | Should -BeLike "*every invoke would fail with 'Handler script not found'*"
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'throws HandlerScriptNotFound when an explicit handler disagrees with a single-file source' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                Handler = 'other.ps1::Invoke'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,New-LambdaPSFunctionDeployment'
        }

        # The package holds only what is in the folder, so a handler outside it would deploy and then
        # fail on every invoke. The deployer says so in its own words: it has no -HandlerScript.
        It 'throws <ErrorId> for a handler outside a folder source: <Case>' -ForEach @(
            @{ Case = 'a relative path'; Handler = '../outside.ps1'; ErrorId = 'HandlerScriptNotFound'; Message = '*Fix -Handler*' }
            @{ Case = 'an absolute path'; Handler = '<absolute>'; ErrorId = 'InvalidHandlerPath'; Message = '*is an absolute path*' }
        ) {
            $parent = Join-Path $TestDrive 'fn-outside-case'
            $source = Join-Path $parent 'src'
            $null = New-Item -ItemType Directory -Path (Join-Path $source 'sub') -Force
            Set-Content -LiteralPath (Join-Path $source 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $source 'sub' 'inner.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $parent 'outside.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $name = if ($Handler -eq '<absolute>') { Join-Path $parent 'outside.ps1' } else { $Handler }
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $source; Handler = $name
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }

            $err = { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId "$ErrorId,New-LambdaPSFunctionDeployment" -PassThru
            $err.Exception.Message | Should -BeLike $Message
            $err.Exception.Message | Should -Not -BeLike '*-HandlerScript*'
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'throws InvalidHandler for a handler with whitespace, before packaging' {
            $source = Join-Path $TestDrive 'fn-space-case'
            $null = New-Item -ItemType Directory -Path (Join-Path $source 'sub dir') -Force
            Set-Content -LiteralPath (Join-Path $source 'sub dir' 'h.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $source; Handler = 'sub dir/h.ps1'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }

            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'InvalidHandler,New-LambdaPSFunctionDeployment' -ExpectedMessage '*contains whitespace*'
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # These scripts are in the source, but Lambda would not find them under the handler as written.
        It 'refuses a handler naming a script in a folder source as <Case>, saying what to write instead' -ForEach @(
            @{ Case = 'an absolute path'; Handler = '<absolute>'; Message = '*is an absolute path*relative to the function source*' }
            @{ Case = 'a path with a backslash'; Handler = 'sub\inner.ps1'; Message = "*uses '\'*Use '/' instead*" }
            # An absolute path that also contains '\' (any Windows path) is reported as absolute.
            @{ Case = 'an absolute path with a backslash'; Handler = '<absolute-backslash>'; Message = '*is an absolute path*relative to the function source*' }
        ) {
            $source = Join-Path $TestDrive 'fn-inside-case'
            $null = New-Item -ItemType Directory -Path (Join-Path $source 'sub') -Force
            Set-Content -LiteralPath (Join-Path $source 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $source 'sub' 'inner.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $name = switch ($Handler) {
                '<absolute>' { Join-Path $source 'handler.ps1' }
                '<absolute-backslash>' { $source + [System.IO.Path]::DirectorySeparatorChar + 'sub\inner.ps1' }
                default { $Handler }
            }
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $source; Handler = $name
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }

            $err = { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'InvalidHandlerPath,New-LambdaPSFunctionDeployment' -PassThru
            $err.CategoryInfo.Category | Should -Be ([System.Management.Automation.ErrorCategory]::InvalidArgument)
            $err.TargetObject | Should -Be $name
            $err.Exception.Message | Should -BeLike $Message
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'accepts a relative handler in a folder source: <Handler>' -ForEach @(
            @{ Handler = 'handler.ps1' }, @{ Handler = './handler.ps1' }, @{ Handler = 'sub/inner.ps1' }
            @{ Handler = 'sub/inner.ps1::Invoke' }, @{ Handler = 'sub/../handler.ps1' }
        ) {
            $source = Join-Path $TestDrive 'fn-inside-ok'
            $null = New-Item -ItemType Directory -Path (Join-Path $source 'sub') -Force
            Set-Content -LiteralPath (Join-Path $source 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $source 'sub' 'inner.ps1') -Value 'param($LambdaInput, $LambdaContext)'

            $result = New-LambdaPSFunctionDeployment -FunctionName hello -ProjectPath $source -Handler $Handler `
                -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r' -Region us-east-1 -PackagePath (Join-Path $TestDrive 'ok.zip')
            $result.Handler | Should -Be $Handler
        }

        It 'throws HandlerScriptNotFound for a handler reached through a folder link that leaves the source' -Skip:$IsWindows {
            $outside = Join-Path $TestDrive 'fn-link-outside'
            $null = New-Item -ItemType Directory -Path $outside -Force
            Set-Content -LiteralPath (Join-Path $outside 'h.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $source = Join-Path $TestDrive 'fn-link-src'
            $null = New-Item -ItemType Directory -Path $source -Force
            Set-Content -LiteralPath (Join-Path $source 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $null = New-Item -ItemType SymbolicLink -Path (Join-Path $source 'lib') -Target $outside
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $source; Handler = 'lib/h.ps1'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }

            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'HandlerScriptNotFound,New-LambdaPSFunctionDeployment'
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'does not check existence for the Module:: handler form, which names no script' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folder
                Handler = 'Module::Handlers::Invoke'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            { New-LambdaPSFunctionDeployment @splat } | Should -Not -Throw
        }
    }

    # Lambda's own defaults (3s / 128MB) are not survivable for a PowerShell function: a warm invoke
    # can take about 2s at 128MB, and pwsh uses about 115-130MB of it. The cmdlet must therefore
    # default to something workable rather than inherit them. The workable values live in
    # Config/function.psd1, so the test reads them from there rather than repeat them.
    Context 'timeout and memory' {
        BeforeAll {
            $script:functionConfig = Import-PowerShellDataFile -Path ([System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'Config', 'function.psd1'))
        }

        It 'configures defaults above Lambda''s 3s / 128MB' {
            $script:functionConfig.Timeout | Should -BeGreaterThan 3
            $script:functionConfig.MemorySize | Should -BeGreaterThan 128
        }

        It 'defaults to the Config/function.psd1 timeout and memory, and forwards both to the create' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat

            $result.Timeout | Should -Be $script:functionConfig.Timeout
            $result.MemorySize | Should -Be $script:functionConfig.MemorySize
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter {
                    $Timeout -eq $script:functionConfig.Timeout -and $MemorySize -eq $script:functionConfig.MemorySize
                }
        }

        It 'forwards explicit -Timeout and -MemorySize' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                Timeout = 120; MemorySize = 2048
            }
            $result = New-LambdaPSFunctionDeployment @splat

            $result.Timeout | Should -Be 120
            $result.MemorySize | Should -Be 2048
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Timeout -eq 120 -and $MemorySize -eq 2048 }
        }

        It 'rejects a timeout above the Lambda maximum' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                Timeout = 901
            }
            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ExceptionType ([System.Management.Automation.ParameterBindingException])
        }
    }

    Context 'explicit handler forms' {
        It 'passes an explicit handler through verbatim and reads #Requires from its script token' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                Handler = 'handler.ps1::Invoke'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat

            $result.Handler | Should -Be 'handler.ps1::Invoke'
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Handler -eq 'handler.ps1::Invoke' }
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $HandlerScript -eq 'handler.ps1' }
        }

        It 'identifies no script for the module handler form' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:folder
                Handler = 'Module::Handlers::Invoke'
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $null = New-LambdaPSFunctionDeployment @splat
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { [string]::IsNullOrEmpty($HandlerScript) }
        }
    }

    Context 'module forwarding' {
        # ModulesStaged reports what the packager actually staged, including '#Requires' modules
        # and dependencies that -IncludeModule does not list.
        It 'reports ModulesStaged from what the packager staged, not from -IncludeModule' {
            Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -MockWith {
                # The packager's real shape: StagedModule objects.
                [PSCustomObject]@{
                    PackagePath   = $OutFile
                    ModulesStaged = @('AWS.Tools.Common', 'AWS.Tools.DynamoDBv2', 'AWS.Tools.S3' | ForEach-Object {
                            [PSCustomObject]@{ PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.StagedModule'; Name = $_; Version = [version]'5.0.103' }
                        })
                }
            }
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            $result = New-LambdaPSFunctionDeployment @splat

            # DynamoDBv2 stands in for a '#Requires' harvest and Common for a transitive dependency.
            $result.ModulesStaged.Name | Should -Be @('AWS.Tools.Common', 'AWS.Tools.DynamoDBv2', 'AWS.Tools.S3')
            $result.ModulesStaged[0].PSTypeNames | Should -Contain 'AWS.ToolExtras.LambdaPSRuntime.StagedModule'
            $result.ModulesStaged[0].Version | Should -BeOfType [version]
        }

        It 'reports no ModulesStaged under -WhatIf, because nothing was packaged' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            (New-LambdaPSFunctionDeployment @splat -WhatIf).ModulesStaged | Should -BeNullOrEmpty
        }

        It 'forwards -MissingModuleAction to the packager, defaulting to Save' -ForEach @(
            @{ Given = $null; Expected = 'Save' }
            @{ Given = 'Install'; Expected = 'Install' }
            @{ Given = 'Stop'; Expected = 'Stop' }
        ) {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                IncludeModule = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            if ($Given) { $splat.MissingModuleAction = $Given }
            $null = New-LambdaPSFunctionDeployment @splat

            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $MissingModuleAction -eq $Expected }
        }

        It 'forwards -Module (alias) to the packager' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                Module = @{ ModuleName = 'AWS.Tools.S3'; RequiredVersion = '5.0.103' }
            }
            $result = New-LambdaPSFunctionDeployment @splat

            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $IncludeModule[0].Name -eq 'AWS.Tools.S3' -and $IncludeModule[0].RequiredVersion -eq '5.0.103' }
        }
    }

    # Lambda now rejects a function whose architecture its customer-managed runtime does not support
    # when the function is created. The deployer checks first, so the error names the runtime alias:
    # it reads the architecture Lambda
    # reports for the alias and either adopts it (-Architecture omitted) or refuses a mismatch (stated).
    Context 'architecture' {
        BeforeEach {
            $script:archSplat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
        }

        It 'reads the architecture from the alias the function binds to' {
            $null = New-LambdaPSFunctionDeployment @script:archSplat -RuntimeAlias staging

            Should -Invoke 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $RuntimeName -eq 'my-ps' -and $RuntimeAlias -eq 'staging' -and $Region -eq 'us-east-1' }
        }

        It 'adopts the runtime architecture when -Architecture is omitted' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('arm64') }

            $result = New-LambdaPSFunctionDeployment @script:archSplat

            # Not a blind x86_64 default: that is the mismatch this check exists to stop.
            $result.Architecture | Should -Be 'arm64'
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Architecture -eq 'arm64' }
        }

        It 'threads a stated architecture that matches the runtime through to the create' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('arm64') }

            $result = New-LambdaPSFunctionDeployment @script:archSplat -Architecture arm64

            $result.Architecture | Should -Be 'arm64'
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Architecture -eq 'arm64' }
        }

        It 'throws ArchitectureMismatch before packaging or creating anything' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('arm64') }

            { New-LambdaPSFunctionDeployment @script:archSplat -Architecture x86_64 } |
                Should -Throw -ErrorId 'ArchitectureMismatch,New-LambdaPSFunctionDeployment'
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'checks an architecture taken from manifest.json like a stated one' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64') }
            $project = Join-Path $TestDrive 'manifest-arm'
            $null = New-Item -ItemType Directory -Path $project -Force
            Set-Content -LiteralPath (Join-Path $project 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $project 'manifest.json') `
                -Value '{"HandlerString":"handler.ps1","ScriptFile":"handler.ps1","Architecture":"arm64"}'
            $splat = @{ FunctionName = 'hello'; ProjectPath = $project; RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1' }

            { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'ArchitectureMismatch,New-LambdaPSFunctionDeployment'
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'prefers x86_64 when the runtime reports both' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('arm64', 'x86_64') }

            (New-LambdaPSFunctionDeployment @script:archSplat).Architecture | Should -Be 'x86_64'
        }

        It 'accepts either architecture against a multi-architecture runtime' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('arm64', 'x86_64') }

            (New-LambdaPSFunctionDeployment @script:archSplat -Architecture arm64).Architecture | Should -Be 'arm64'
        }

        It 'falls back to x86_64 with a warning when the runtime reports no architecture' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $warnings = @()
            $result = New-LambdaPSFunctionDeployment @script:archSplat -WarningVariable warnings -WarningAction SilentlyContinue

            $result.Architecture | Should -Be 'x86_64'
            ($warnings -join ' ') | Should -Match 'cannot be verified'
        }

        It 'still warns, but honours a stated architecture, when the runtime reports none' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@() }

            $warnings = @()
            $result = New-LambdaPSFunctionDeployment @script:archSplat -Architecture arm64 -WarningVariable warnings -WarningAction SilentlyContinue

            $result.Architecture | Should -Be 'arm64'
            $warnings.Count | Should -BeGreaterThan 0
        }

        It 'checks the architecture under -WhatIf, so a preview reports the mismatch too' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('arm64') }

            { New-LambdaPSFunctionDeployment @script:archSplat -Architecture x86_64 -WhatIf } |
                Should -Throw -ErrorId 'ArchitectureMismatch,New-LambdaPSFunctionDeployment'
        }
    }

    Context 'error handling' {
        It 'throws ProjectNotFound when the source path does not exist' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = (Join-Path $TestDrive 'missing.ps1')
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $err = { New-LambdaPSFunctionDeployment @splat } |
                Should -Throw -ErrorId 'ProjectNotFound,New-LambdaPSFunctionDeployment' -PassThru
            $err.Exception.Message | Should -BeLike '*was not found. Pass a .ps1 or a folder of handler code, or scaffold one with New-LambdaPSFunctionProject -Path <dir> -Handler handler.'
        }

        # Packaging replaces the file at -PackagePath, so only a .zip may be there; anything else is the
        # user's file, and is refused before packaging, under -WhatIf too.
        It 'refuses a -PackagePath that is <Case>, and leaves it unchanged (WhatIf <WhatIf>)' -ForEach @(
            @{ Case = 'the single-file source'; Folder = $false; WhatIf = $false }
            @{ Case = 'the single-file source'; Folder = $false; WhatIf = $true }
            @{ Case = 'another file in a folder source'; Folder = $true; WhatIf = $false }
            @{ Case = 'another file in a folder source'; Folder = $true; WhatIf = $true }
        ) {
            $root = Join-Path $TestDrive "package-path-$([guid]::NewGuid().ToString('n'))"
            $null = New-Item -ItemType Directory -Path $root
            $handler = Join-Path $root 'handler.ps1'
            Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)'
            $library = Join-Path $root 'lib.ps1'
            Set-Content -LiteralPath $library -Value 'function Get-Value { 1 }'
            $splat = @{
                FunctionName = 'hello'; RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
                ProjectPath = if ($Folder) { $root } else { $handler }; Handler = 'handler.ps1'
                PackagePath = if ($Folder) { $library } else { $handler }
            }
            $before = Get-FileHash -LiteralPath $splat.PackagePath

            $err = { New-LambdaPSFunctionDeployment @splat -WhatIf:$WhatIf } |
                Should -Throw -ErrorId 'PackagePathNotZip,New-LambdaPSFunctionDeployment' -PassThru

            $err.Exception.Message | Should -BeLike "*packaging would overwrite it*"
            (Get-FileHash -LiteralPath $splat.PackagePath).Hash | Should -Be $before.Hash
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # An earlier package in the source folder is the documented 'cd fn; -ProjectPath .' case, and is replaced.
        It 'accepts a -PackagePath that is an existing .zip inside the folder source' {
            $root = Join-Path $TestDrive 'package-path-zip'
            $null = New-Item -ItemType Directory -Path $root
            Set-Content -LiteralPath (Join-Path $root 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            $zip = Join-Path $root 'hello.zip'
            Set-Content -LiteralPath $zip -Value 'an earlier package'
            Push-Location $root
            try { $result = New-LambdaPSFunctionDeployment -FunctionName hello -ProjectPath . -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r' -Region us-east-1 }
            finally { Pop-Location }

            $result.PackagePath | Should -Be $zip
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $OutFile -eq $zip -and $Force }
        }

        # Only the runtime read maps a refusal to RuntimeServiceAccessDenied (Get-LambdaPSRuntimeArn); the
        # alias seam raises RuntimeAliasCheckFailed, so the help must say which is which.
        It 'documents RuntimeServiceAccessDenied for the runtime read and RuntimeAliasCheckFailed for the alias read, as the seams raise them' {
            $description = (Get-Help New-LambdaPSFunctionDeployment -Full).Description.Text -join ' ' -replace '\s+', ' '
            $description | Should -Match 'A refused runtime read is RuntimeServiceAccessDenied'
            $description | Should -Match 'a refused or otherwise failed alias read is RuntimeAliasCheckFailed'
        }

        It 'stops with RuntimeAliasCheckFailed before packaging when the alias read is refused' {
            Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith {
                $PSCmdlet.ThrowTerminatingError([System.Management.Automation.ErrorRecord]::new(
                        [System.InvalidOperationException]::new('Could not read alias'), 'RuntimeAliasCheckFailed',
                        [System.Management.Automation.ErrorCategory]::ReadError, 'my-ps/default'))
            }
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            { New-LambdaPSFunctionDeployment @splat } | Should -Throw -ErrorId 'RuntimeAliasCheckFailed,New-LambdaPSFunctionDeployment'
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'confirmation' {
        It 'asks once per stage: every call inside a stage runs with -Confirm:$false' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $null = New-LambdaPSFunctionDeployment @splat

            $noConfirm = { $Confirm -eq $false }
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $noConfirm
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $noConfirm
        }
    }

    Context 'credential parameters' {
        It 'passes -ProfileName to every seam that calls AWS' {
            # A credential file of the test's own that defines the profile.
            $credentialFile = [System.IO.Path]::Combine($TestDrive, 'credentials')
            Set-Content -LiteralPath $credentialFile -Value "[dev]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'; ProfileName = 'dev'
                ProfileLocation = $credentialFile
            }
            $null = New-LambdaPSFunctionDeployment @splat

            $hasProfile = { $CredentialParameter.ProfileName -eq 'dev' }
            Should -Invoke 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
            Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
            Should -Invoke 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter $hasProfile
        }
    }

    Context '-EnvironmentVariable' {
        It 'passes -EnvironmentVariable to the create or update' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $null = New-LambdaPSFunctionDeployment @splat -EnvironmentVariable @{ POWERSHELL_RUNTIME_JSON_DEPTH = 10 }

            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                @($EnvironmentVariable.GetEnumerator()).Count -eq 1 -and $EnvironmentVariable['POWERSHELL_RUNTIME_JSON_DEPTH'] -eq 10
            }
        }

        It 'passes no environment when -EnvironmentVariable is omitted, so a redeploy leaves it unchanged' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $null = New-LambdaPSFunctionDeployment @splat

            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                -not $PSBoundParameters.ContainsKey('EnvironmentVariable')
            }
        }
    }

    Context '-TenantIsolationMode' {
        BeforeEach {
            $script:tenantSplat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
        }

        It 'passes -TenantIsolationMode to the create' {
            $null = New-LambdaPSFunctionDeployment @script:tenantSplat -TenantIsolationMode PER_TENANT

            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $TenantIsolationMode -eq 'PER_TENANT' }
        }

        It 'redeploys a PER_TENANT function given <Case>' -ForEach @(
            @{ Case = 'a lower-case mode'; Given = @{ TenantIsolationMode = 'per_tenant' } }
            @{ Case = 'no mode'; Given = @{} }
        ) {
            Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Handler = 'handler.ps1'; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
                    RuntimeAlias = 'default'; Architecture = 'x86_64'; TenantIsolationMode = 'PER_TENANT' }
            }

            $null = New-LambdaPSFunctionDeployment @script:tenantSplat @Given

            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly
        }

        It 'refuses, before packaging, a redeploy that gives a mode the function does not have' {
            Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Handler = 'handler.ps1'; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
                    RuntimeAlias = 'default'; Architecture = 'x86_64'; TenantIsolationMode = '' }
            }

            { New-LambdaPSFunctionDeployment @script:tenantSplat -TenantIsolationMode PER_TENANT } |
                Should -Throw -ErrorId 'TenantIsolationModeMismatch,New-LambdaPSFunctionDeployment' -ExpectedMessage "Function 'hello' has no tenant isolation, and Lambda sets the mode only when it creates a function*"

            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    # Behaviour that depends on how Lambda answers.
    Context 'service behaviour' {
        It 'reports the configuration Lambda returns after an update, not the requested one' {
            Mock 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{
                    Updated = $true; Handler = 'handler.ps1'; Timeout = 30; MemorySize = 512; Role = 'arn:old-role'; Arn = 'arn:f'
                    Architecture = 'x86_64'; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/old-rt'; RuntimeAlias = 'stable'
                }
            }
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile; Timeout = 60; MemorySize = 1024
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat

            $result.Timeout | Should -Be 30
            $result.MemorySize | Should -Be 512
            $result.Role | Should -Be 'arn:old-role'
            $result.Architecture | Should -Be 'x86_64'
            $result.RuntimeArn | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/old-rt'
            $result.RuntimeAlias | Should -Be 'stable'
            $result.RuntimeName | Should -Be 'old-rt'
            $result.Updated | Should -BeTrue
            $result.FunctionArn | Should -Be 'arn:f'
        }

        # A runtime ARN with no alias is a binding Lambda can report. Reporting it must not fail after
        # the function was updated.
        It 'reports a function bound with no runtime alias, rather than failing after the update' {
            Mock 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{
                    Updated = $true; Handler = 'handler.ps1'; Timeout = 30; MemorySize = 512; Role = 'arn:old-role'; Arn = 'arn:f'
                    Architecture = 'x86_64'; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = ''
                }
            }
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $result = New-LambdaPSFunctionDeployment @splat

            $result.Updated | Should -BeTrue
            $result.RuntimeAlias | Should -BeNullOrEmpty
            $result.RuntimeName | Should -Be 'my-ps'
            $result.FunctionArn | Should -Be 'arn:f'
        }

        It 'reports full paths for a relative -ProjectPath and -PackagePath' {
            Push-Location $TestDrive
            try {
                Set-Content -LiteralPath (Join-Path $TestDrive 'rel.ps1') -Value 'param($LambdaInput, $LambdaContext)'
                $result = New-LambdaPSFunctionDeployment -FunctionName hello -ProjectPath ./rel.ps1 -PackagePath ./out/hello.zip `
                    -RuntimeName my-ps -Role arn:aws:iam::111122223333:role/r -Region us-east-1
            }
            finally { Pop-Location }
            $result.ProjectPath | Should -Be ([System.IO.Path]::Combine($TestDrive, 'rel.ps1'))
            $result.PackagePath | Should -Be ([System.IO.Path]::Combine($TestDrive, 'out', 'hello.zip'))
        }

        It 'rejects a project folder that carries a lambdaps-runtime.json marker (<Target>)' -ForEach @(
            @{ Target = 'Container' }, @{ Target = 'SelfContainedZip' }, @{ Target = 'CMR' }
        ) {
            $dir = Join-Path $TestDrive "marker-$Target"
            $null = New-Item -ItemType Directory -Force -Path $dir
            Set-Content -LiteralPath (Join-Path $dir 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
            Set-Content -LiteralPath (Join-Path $dir 'lambdaps-runtime.json') -Value (@{ Target = $Target; PowerShellVersion = '7.4' } | ConvertTo-Json)
            $splat = @{ FunctionName = 'hello'; ProjectPath = $dir; RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1' }

            $err = { New-LambdaPSFunctionDeployment @splat } | Should -Throw -ErrorId 'NotAFunctionProject,New-LambdaPSFunctionDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "*is a $Target project*"
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'module repository' {
        It 'forwards -ModuleRepository to the packager, and nothing when it is omitted' {
            $splat = @{ FunctionName = 'hello'; ProjectPath = $script:handlerFile; RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1' }
            $null = New-LambdaPSFunctionDeployment @splat -ModuleRepository Internal
            $null = New-LambdaPSFunctionDeployment @splat
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $ModuleRepository -eq 'Internal' }
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { -not $ModuleRepository }
        }
    }

    Context 'region' {
        It 'uses the region AWS.Tools would use when -Region is omitted' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { 'ap-southeast-2' }
            $result = New-LambdaPSFunctionDeployment -FunctionName hello -ProjectPath $script:handlerFile -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r'

            $result.Region | Should -Be 'ap-southeast-2'
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'ap-southeast-2' }
        }

        It 'uses the region of a function ARN when -Region is omitted, and names the default package after the function' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { 'ap-southeast-2' }
            $arn = 'arn:aws:lambda:eu-west-1:111122223333:function:hello'
            Push-Location $TestDrive
            try {
                $result = New-LambdaPSFunctionDeployment -FunctionName $arn -ProjectPath $script:handlerFile -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r'
            }
            finally { Pop-Location }

            $result.Region | Should -Be 'eu-west-1'
            $result.PackagePath | Should -Be ([System.IO.Path]::Combine($TestDrive, 'hello.zip'))
            Should -Invoke 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -Times 0 -Exactly
            Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'eu-west-1' }
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'eu-west-1' -and $FunctionName -eq $arn }
        }

        It 'names the default package after the function in a partial ARN, and resolves the region as usual' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { 'ap-southeast-2' }
            Push-Location $TestDrive
            try {
                $result = New-LambdaPSFunctionDeployment -FunctionName '111122223333:function:hello' -ProjectPath $script:handlerFile -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r'
            }
            finally { Pop-Location }

            $result.PackagePath | Should -Be ([System.IO.Path]::Combine($TestDrive, 'hello.zip'))
            $result.Region | Should -Be 'ap-southeast-2'
        }

        It 'refuses a -FunctionName with a qualifier, before anything is read or packaged: <Value>' -ForEach @(
            @{ Value = 'hello:prod'; Qualifier = 'prod' }
            @{ Value = 'arn:aws:lambda:eu-west-1:111122223333:function:hello:live'; Qualifier = 'live' }
            @{ Value = '111122223333:function:hello:3'; Qualifier = '3' }
        ) {
            $err = { New-LambdaPSFunctionDeployment -FunctionName $Value -ProjectPath $script:handlerFile -RuntimeName my-ps `
                    -Role 'arn:aws:iam::111122223333:role/r' -Region eu-west-1 } |
                Should -Throw -ErrorId 'FunctionNameQualified,New-LambdaPSFunctionDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "*names the version or alias '$Qualifier' of function 'hello'*without the qualifier*"
            Should -Invoke 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'refuses a -Region that differs from the region in a function ARN, before anything is read or packaged' {
            $err = { New-LambdaPSFunctionDeployment -FunctionName 'arn:aws:lambda:eu-west-1:111122223333:function:hello' -ProjectPath $script:handlerFile `
                    -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r' -Region us-west-2 -PackagePath (Join-Path $TestDrive 'explicit.zip') } |
                Should -Throw -ErrorId 'FunctionRegionMismatch,New-LambdaPSFunctionDeployment' -PassThru
            $err.Exception.Message | Should -BeLike "*region 'eu-west-1', but -Region is 'us-west-2'. Omit -Region, or pass -Region eu-west-1."
            Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'accepts a -Region equal to the region in a function ARN' {
            $result = New-LambdaPSFunctionDeployment -FunctionName 'arn:aws:lambda:eu-west-1:111122223333:function:hello' -ProjectPath $script:handlerFile `
                -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r' -Region eu-west-1 -PackagePath (Join-Path $TestDrive 'explicit.zip')

            $result.Region | Should -Be 'eu-west-1'
        }

        # Region names are matched without regard to case, so the same region in capitals is no mismatch.
        It 'accepts a -Region that differs from the ARN''s only in case' {
            $result = New-LambdaPSFunctionDeployment -FunctionName 'arn:aws:lambda:eu-west-1:111122223333:function:hello' -ProjectPath $script:handlerFile `
                -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r' -Region EU-WEST-1 -PackagePath (Join-Path $TestDrive 'explicit.zip')

            $result.FunctionName | Should -Be 'arn:aws:lambda:eu-west-1:111122223333:function:hello'
        }

        It 'reads the region and name of a function ARN whose region is in capitals' {
            Mock 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -MockWith { 'ap-southeast-2' }
            Push-Location $TestDrive
            try {
                $result = New-LambdaPSFunctionDeployment -FunctionName 'arn:aws:lambda:EU-WEST-1:111122223333:function:hello' -ProjectPath $script:handlerFile `
                    -RuntimeName my-ps -Role 'arn:aws:iam::111122223333:role/r'
            }
            finally { Pop-Location }

            $result.Region | Should -Be 'EU-WEST-1'
            $result.PackagePath | Should -Be ([System.IO.Path]::Combine($TestDrive, 'hello.zip'))
            Should -Invoke 'Resolve-LambdaPSRegion' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }

    Context 'WhatIf' {
        It 'packages and creates nothing under -WhatIf' {
            $splat = @{
                FunctionName = 'hello'; ProjectPath = $script:handlerFile
                RuntimeName = 'my-ps'; Role = 'arn:aws:iam::111122223333:role/r'; Region = 'us-east-1'
            }
            $null = New-LambdaPSFunctionDeployment @splat -WhatIf

            # The package is previewed (so its checks run) but never written.
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $WhatIf }
            Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly -ParameterFilter { -not $WhatIf }
            Should -Invoke 'Publish-LambdaPSFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }
    }
}

# Lambda's own rules for runtime and alias names, checked at binding so a bad name never reaches AWS.
Describe 'New-LambdaPSFunctionDeployment -RuntimeName and -RuntimeAlias' -Tag 'Unit' {
    BeforeAll {
        $script:nameSource = Join-Path $TestDrive 'names.ps1'
        Set-Content -LiteralPath $script:nameSource -Value 'param($LambdaInput, $LambdaContext)'
    }

    It 'refuses the runtime name <Name>' -ForEach @(
        @{ Name = 'My-PS' }, @{ Name = 'a' }, @{ Name = 'rt-' }, @{ Name = '-rt' }, @{ Name = 'my ps' }
        @{ Name = "rt`n" }, @{ Name = 'r' + ('x' * 127) + 'z' }
    ) {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { throw 'must not read the runtime' }
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $script:nameSource -RuntimeName $Name -Role arn:aws:iam::111122223333:role/r -Region us-east-1 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionDeployment' -ExpectedMessage '*is not a valid runtime name*'
        Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'refuses the runtime alias <Alias>' -ForEach @(
        @{ Alias = '-prod' }, @{ Alias = 'prod.' }, @{ Alias = 'pro d' }, @{ Alias = "prod`n" }, @{ Alias = 'a' * 129 }
    ) {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { throw 'must not read the runtime' }
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $script:nameSource -RuntimeName rt -RuntimeAlias $Alias -Role arn:aws:iam::111122223333:role/r -Region us-east-1 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionDeployment' -ExpectedMessage '*is not a valid runtime alias name*'
        Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'accepts the boundary name <Value> for -<Parameter>' -ForEach @(
        @{ Parameter = 'RuntimeName'; Value = 'a1' }, @{ Parameter = 'RuntimeName'; Value = 'my_ps.rt-2' }
        @{ Parameter = 'RuntimeName'; Value = 'r' + ('x' * 126) + 'z' }
        @{ Parameter = 'RuntimeAlias'; Value = 'a' }, @{ Parameter = 'RuntimeAlias'; Value = '_Prod.v1-2_' }
        @{ Parameter = 'RuntimeAlias'; Value = 'a' * 128 }
    ) {
        $attribute = (Get-Command New-LambdaPSFunctionDeployment).Parameters[$Parameter].Attributes |
            Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }
        [regex]::IsMatch($Value, $attribute.RegexPattern, $attribute.Options) | Should -BeTrue
    }

    # Matched case-sensitively: ignoring case, U+212A KELVIN SIGN matches 'k' and binds.
    It 'refuses a runtime alias with a Kelvin sign' {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { throw 'must not read the runtime' }
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $script:nameSource -RuntimeName rt -RuntimeAlias "prod$([char]0x212A)" -Role arn:aws:iam::111122223333:role/r -Region us-east-1 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionDeployment' -ExpectedMessage '*is not a valid runtime alias name*'
    }

    # The same wording as New-LambdaPSRuntimeDeployment, which creates the runtime.
    It 'describes a valid runtime name as the runtime deploy does' {
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $script:nameSource -RuntimeName My-PS -Role arn:aws:iam::111122223333:role/r -Region us-east-1 } |
            Should -Throw -ExpectedMessage "*'My-PS' is not a valid runtime name (lowercase letters, digits, '_', '.' and '-', 2 to 128 characters, starting and ending with a letter or digit).*"
    }

    # The alias is read from manifest.json after binding, so it is checked against the same pattern there.
    It 'throws InvalidProjectManifest, naming manifest.json, for a manifest RuntimeAlias the pattern refuses: <Alias>' -ForEach @(
        @{ Alias = 'prod-' }, @{ Alias = "prod$([char]0x212A)" }
    ) {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { throw 'must not read the runtime' }
        $project = Join-Path $TestDrive "manifest-alias-$([int][char]$Alias[-1])"
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Encoding utf8 `
            -Value (@{ HandlerString = 'handler.ps1'; ScriptFile = 'handler.ps1'; RuntimeAlias = $Alias } | ConvertTo-Json)

        $err = { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $project -RuntimeName rt -Role arn:aws:iam::111122223333:role/r -Region us-east-1 } |
            Should -Throw -ErrorId 'InvalidProjectManifest,New-LambdaPSFunctionDeployment' -PassThru
        $err.Exception.Message | Should -BeLike "*$([System.IO.Path]::Combine($project, 'manifest.json'))*RuntimeAlias*not a valid runtime alias name*"
        Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    # -Architecture's ValidateSet refuses such a value, so one from manifest.json is refused too, not dropped
    # for the alias's architecture.
    It 'throws InvalidProjectManifest, naming manifest.json, for a manifest Architecture that is not x86_64 or arm64: <Architecture>' -ForEach @(
        @{ Architecture = 'amr64' }, @{ Architecture = 'x86-64' }
    ) {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { throw 'must not read the runtime' }
        $project = Join-Path $TestDrive "manifest-arch-$Architecture"
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Encoding utf8 `
            -Value (@{ HandlerString = 'handler.ps1'; ScriptFile = 'handler.ps1'; Architecture = $Architecture } | ConvertTo-Json)

        $err = { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $project -RuntimeName rt -Role arn:aws:iam::111122223333:role/r -Region us-east-1 } |
            Should -Throw -ErrorId 'InvalidProjectManifest,New-LambdaPSFunctionDeployment' -PassThru
        $err.Exception.Message | Should -BeLike "*$([System.IO.Path]::Combine($project, 'manifest.json'))*Architecture '$Architecture'*not x86_64 or arm64*"
        Should -Invoke 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -Times 0 -Exactly
    }
}

Describe 'New-LambdaPSFunctionDeployment -Role' -Tag 'Unit' {
    It 'refuses <Role> before anything is packaged' -ForEach @(
        @{ Role = 'hello-role' }
        @{ Role = 'arn:aws:iam::111122223333:user/someone' }
        # '$' would match before a final newline; the pattern ends in \z so the whole value is checked.
        @{ Role = "arn:aws:iam::111122223333:role/r`n" }
    ) {
        Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -MockWith { throw 'must not package' }
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $TestDrive -RuntimeName rt -Role $Role -Region us-east-1 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionDeployment' -ExpectedMessage '*is not an IAM role ARN*'
        Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'accepts a role ARN in the <Partition> partition' -ForEach @(
        @{ Partition = 'aws'; Role = 'arn:aws:iam::111122223333:role/r' }
        @{ Partition = 'aws-cn'; Role = 'arn:aws-cn:iam::111122223333:role/r' }
        @{ Partition = 'aws-us-gov'; Role = 'arn:aws-us-gov:iam::111122223333:role/service-role/r' }
    ) {
        $attribute = (Get-Command New-LambdaPSFunctionDeployment).Parameters['Role'].Attributes |
            Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }
        $Role | Should -Match $attribute.RegexPattern
    }

    It 'refuses a role ARN whose fixed parts are in capitals' {
        Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -MockWith { throw 'must not package' }
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $TestDrive -RuntimeName rt -Role 'ARN:AWS:IAM::111122223333:ROLE/r' -Region us-east-1 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionDeployment' -ExpectedMessage '*is not an IAM role ARN*'
    }

    It 'refuses to create a function without -Role, before anything is packaged, under -WhatIf too' -ForEach @(
        @{ WhatIf = $false }, @{ WhatIf = $true }
    ) {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { 'arn:aws:lambda:us-east-1:111122223333:runtime/rt' }
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64') }
        Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith { $null }
        Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -MockWith { throw 'must not package' }
        $err = { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $script:handlerFile -RuntimeName rt -Region us-east-1 -WhatIf:$WhatIf } |
            Should -Throw -ErrorId 'RoleRequired,New-LambdaPSFunctionDeployment' -PassThru
        $err.Exception.Message | Should -BeLike "Function 'fn' does not exist in region 'us-east-1'*Pass -Role*or -Region*"
    }

    It 'leaves out the -Region hint when the region comes from a function ARN' {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { 'arn:aws:lambda:us-west-2:111122223333:runtime/rt' }
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64') }
        Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith { $null }
        $err = { New-LambdaPSFunctionDeployment -FunctionName arn:aws:lambda:us-west-2:111122223333:function:fn -ProjectPath $script:handlerFile -RuntimeName rt } |
            Should -Throw -ErrorId 'RoleRequired,New-LambdaPSFunctionDeployment' -PassThru
        $err.Exception.Message | Should -BeLike "*in region 'us-west-2'*to create it."
        $err.Exception.Message | Should -Not -BeLike '*-Region*'
    }

    # \d also matches other scripts' digits, such as Arabic-Indic ones; an account ID is ASCII digits only.
    It 'refuses a role ARN whose account is in non-ASCII digits' {
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $script:moduleName -MockWith { throw 'must not read the runtime' }
        Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -MockWith { throw 'must not package' }
        { New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath $TestDrive -RuntimeName rt -Role "arn:aws:iam::11112222333$([char]0x0663):role/r" -Region us-east-1 } |
            Should -Throw -ErrorId 'ParameterArgumentValidationError,New-LambdaPSFunctionDeployment' -ExpectedMessage '*is not an IAM role ARN*'
    }
}

# A redeploy applies what the caller gave and keeps the rest of an existing function's configuration.
# The real seam runs; only the AWS cmdlets are stubbed, each answering as Lambda does.
Describe 'New-LambdaPSFunctionDeployment redeploy of a tuned function' -Tag 'Unit' {
    BeforeAll {
        $script:tunedSource = Join-Path $TestDrive 'tuned'
        $null = New-Item -ItemType Directory -Path $script:tunedSource -Force
        Set-Content -LiteralPath (Join-Path $script:tunedSource 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
    }

    BeforeEach {
        $m = $script:moduleName
        Mock 'Get-LambdaPSRuntimeArn' -ModuleName $m -MockWith { "arn:aws:lambda:us-east-1:111122223333:runtime/$RuntimeName" }
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $m -MockWith { , [string[]]@('x86_64') }
        # A real .zip holding the source's scripts, so the seam can see what the package contains.
        Mock 'Compress-LambdaPSFunctionPackage' -ModuleName $m -MockWith {
            Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
            if (Test-Path -LiteralPath $OutFile) { Remove-Item -LiteralPath $OutFile }
            $zip = [System.IO.Compression.ZipFile]::Open($OutFile, [System.IO.Compression.ZipArchiveMode]::Create)
            try {
                $files = if (Test-Path -LiteralPath $Path -PathType Leaf) { Get-Item -LiteralPath $Path } else { Get-ChildItem -LiteralPath $Path -File }
                foreach ($file in $files) { $null = [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $file.Name) }
            }
            finally { $zip.Dispose() }
            [PSCustomObject]@{ PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.FunctionPackage'; PackagePath = $OutFile; ModulesStaged = @() }
        }
        Mock 'Publish-LMFunction' -ModuleName $m -MockWith { throw [LambdaPSTests.Fakes.ResourceConflictException]::new('Function already exists') }
        # The existing function, tuned outside this module.
        $script:tuned = @{
            Handler = 'handler.ps1::Invoke-Handler'; Timeout = 300; MemorySize = 2048; Role = 'arn:aws:iam::111122223333:role/r'
            RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; RuntimeAlias = 'staging'
            Architecture = 'x86_64'; Runtime = $null; PackageType = 'Zip'
        }
        # A function not bound to a customer-managed runtime has no RuntimeConfig.
        Mock 'Get-LMFunctionConfiguration' -ModuleName $m -MockWith {
            [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; Handler = $script:tuned.Handler
                Timeout = $script:tuned.Timeout; MemorySize = $script:tuned.MemorySize; Role = $script:tuned.Role
                Architectures = @($script:tuned.Architecture); Runtime = $script:tuned.Runtime; PackageType = $script:tuned.PackageType
                RuntimeConfig = if ($script:tuned.RuntimeArn) { [PSCustomObject]@{ RuntimeArn = $script:tuned.RuntimeArn; RuntimeAlias = $script:tuned.RuntimeAlias } } }
        }
        # Lambda applies what was sent and returns the whole configuration, from both updates.
        $script:liveConfiguration = {
            [PSCustomObject]@{ FunctionArn = 'arn:f'; Handler = $script:tuned.Handler; Timeout = $script:tuned.Timeout
                MemorySize = $script:tuned.MemorySize; Role = $script:tuned.Role; Architectures = @($script:tuned.Architecture)
                RuntimeConfig = [PSCustomObject]@{ RuntimeArn = $script:tuned.RuntimeArn; RuntimeAlias = $script:tuned.RuntimeAlias } }
        }
        Mock 'Update-LMFunctionCode' -ModuleName $m -MockWith {
            if ($Architecture) { $script:tuned.Architecture = $Architecture }
            & $script:liveConfiguration
        }
        Mock 'Update-LMFunctionConfiguration' -ModuleName $m -MockWith {
            if ($Handler) { $script:tuned.Handler = $Handler }
            if ($Timeout) { $script:tuned.Timeout = $Timeout }
            if ($MemorySize) { $script:tuned.MemorySize = $MemorySize }
            if ($Role) { $script:tuned.Role = $Role }
            if ($RuntimeConfig_RuntimeAlias) {
                $script:tuned.RuntimeArn = $RuntimeConfig_RuntimeArn
                $script:tuned.RuntimeAlias = $RuntimeConfig_RuntimeAlias
            }
            & $script:liveConfiguration
        }
        # A redeploy needs no -Role: the function keeps its own.
        $script:redeploy = @{
            FunctionName = 'hello'; ProjectPath = $script:tunedSource; RuntimeName = 'my-ps'
            Region = 'us-east-1'; PackagePath = (Join-Path $TestDrive 'tuned.zip')
        }
    }

    It 'keeps the alias, timeout, memory and handler that were not given, when the handler''s script ships' {
        $result = New-LambdaPSFunctionDeployment @script:redeploy

        $result.Updated | Should -BeTrue
        $result.RuntimeAlias | Should -Be 'staging'
        $result.Timeout | Should -Be 300
        $result.MemorySize | Should -Be 2048
        # handler.ps1 is in the package, so the function keeps its 'handler.ps1::Invoke-Handler'.
        $result.Handler | Should -Be 'handler.ps1::Invoke-Handler'
        $result.Role | Should -Be 'arn:aws:iam::111122223333:role/r'
        # Nothing to change, so only the code is updated, with no create tried.
        Should -Invoke 'Publish-LMFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        Should -Invoke 'Update-LMFunctionConfiguration' -ModuleName $script:moduleName -Times 0 -Exactly
        # Checked against the alias the function stays on, whose architecture it keeps.
        Should -Invoke 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $RuntimeAlias -eq 'staging' }
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'x86_64' }
    }

    It 'applies the settings that were given, and keeps the rest' {
        $result = New-LambdaPSFunctionDeployment @script:redeploy -Timeout 60 -RuntimeAlias live -Handler 'handler.ps1'

        $result.Timeout | Should -Be 60
        $result.RuntimeAlias | Should -Be 'live'
        $result.Handler | Should -Be 'handler.ps1'
        $result.MemorySize | Should -Be 2048
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'x86_64' }
    }

    It 'changes the role to a -Role that differs from the function''s, and sends nothing else' {
        $result = New-LambdaPSFunctionDeployment @script:redeploy -Role 'arn:aws:iam::111122223333:role/other'

        $result.Role | Should -Be 'arn:aws:iam::111122223333:role/other'
        Should -Invoke 'Update-LMFunctionConfiguration' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
            $Role -eq 'arn:aws:iam::111122223333:role/other' -and $null -eq $Handler -and $null -eq $Timeout -and
            $null -eq $MemorySize -and $null -eq $RuntimeConfig_RuntimeAlias -and $null -eq $Environment_IsVariablesSet
        }
    }

    It 'skips the configuration update when the settings given match the function''s' {
        $result = New-LambdaPSFunctionDeployment @script:redeploy -Role 'arn:aws:iam::111122223333:role/r' -Timeout 300 -MemorySize 2048 -RuntimeAlias staging

        $result.Updated | Should -BeTrue
        $result.FunctionArn | Should -Be 'arn:f'
        $result.Timeout | Should -Be 300
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly
        Should -Invoke 'Update-LMFunctionConfiguration' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'sends only the setting that differs' {
        $result = New-LambdaPSFunctionDeployment @script:redeploy -Timeout 60 -MemorySize 2048

        $result.Timeout | Should -Be 60
        Should -Invoke 'Update-LMFunctionConfiguration' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
            $Timeout -eq 60 -and $null -eq $MemorySize -and $null -eq $Role
        }
    }

    It 'reports the function''s role under -WhatIf when -Role is omitted' {
        $result = New-LambdaPSFunctionDeployment @script:redeploy -WhatIf

        $result.Role | Should -Be 'arn:aws:iam::111122223333:role/r'
    }

    It 'applies the script of a renamed single-file source, so the function never keeps a handler the package lacks' {
        $script:tuned.Handler = 'old.ps1'
        $single = Join-Path $TestDrive 'renamed.ps1'
        Set-Content -LiteralPath $single -Value 'param($LambdaInput, $LambdaContext)'
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $single

        $result = New-LambdaPSFunctionDeployment @splat

        $result.Handler | Should -Be 'renamed.ps1'
        $result.Timeout | Should -Be 300
    }

    It 'checks the architecture against the alias the function keeps, which need not be the default' {
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith {
            if ($RuntimeAlias -ne 'staging') { throw 'only the staging alias exists' }
            , [string[]]@('x86_64')
        }

        $result = New-LambdaPSFunctionDeployment @script:redeploy

        $result.RuntimeAlias | Should -Be 'staging'
        Should -Invoke 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $RuntimeAlias -eq 'staging' }
    }

    It 'keeps an arm64 function arm64 when the kept alias reports no architecture' {
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@() }
        Mock 'Get-LMFunctionConfiguration' -ModuleName $script:moduleName -MockWith {
            [PSCustomObject]@{ State = 'Active'; LastUpdateStatus = 'Successful'; Handler = $script:tuned.Handler
                Timeout = $script:tuned.Timeout; MemorySize = $script:tuned.MemorySize; Role = $script:tuned.Role; Architectures = @('arm64')
                RuntimeConfig = [PSCustomObject]@{ RuntimeArn = $script:tuned.RuntimeArn; RuntimeAlias = $script:tuned.RuntimeAlias } }
        }

        $null = New-LambdaPSFunctionDeployment @script:redeploy -WarningAction SilentlyContinue

        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
    }

    It 'applies a stated -Architecture that the kept alias supports' {
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64', 'arm64') }

        $null = New-LambdaPSFunctionDeployment @script:redeploy -Architecture arm64

        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
    }

    It 'refuses a stated -Architecture the kept alias does not support, naming that alias and -RuntimeAlias' {
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64') }

        $err = { New-LambdaPSFunctionDeployment @script:redeploy -Architecture arm64 } |
            Should -Throw -ErrorId 'ArchitectureMismatch,New-LambdaPSFunctionDeployment' -PassThru
        $err.Exception.Message | Should -BeLike "*alias 'staging'*stays bound to alias 'staging'*pass -RuntimeAlias to bind it to another alias."
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    It 'checks the alias given with -RuntimeAlias, with -Handler and -Architecture given too' {
        $null = New-LambdaPSFunctionDeployment @script:redeploy -RuntimeAlias live -Handler handler.ps1 -Architecture x86_64

        Should -Invoke 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $RuntimeAlias -eq 'live' }
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly
    }

    # Naming every setting the live function would supply does not skip the check that it can run this code.
    It 'refuses <Case> before packaging when -Handler, -RuntimeAlias and -Architecture are all given (WhatIf <WhatIf>)' -ForEach @(
        @{ Case = 'a python function'; Runtime = 'python3.12'; PackageType = 'Zip'; WhatIf = $false }
        @{ Case = 'a python function'; Runtime = 'python3.12'; PackageType = 'Zip'; WhatIf = $true }
        @{ Case = 'a container-image function'; Runtime = $null; PackageType = 'Image'; WhatIf = $false }
        @{ Case = 'a container-image function'; Runtime = $null; PackageType = 'Image'; WhatIf = $true }
    ) {
        $script:tuned.RuntimeArn = ''
        $script:tuned.Handler = 'lambda_function.lambda_handler'
        $script:tuned.Runtime = $Runtime
        $script:tuned.PackageType = $PackageType

        { New-LambdaPSFunctionDeployment @script:redeploy -RuntimeAlias live -Handler handler.ps1 -Architecture x86_64 -WhatIf:$WhatIf } |
            Should -Throw -ErrorId 'FunctionNotRuntimeBound,New-LambdaPSFunctionDeployment'

        Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    # A setting not given keeps the function's value: its architecture too, when the alias it will be
    # on supports it, whichever alias or runtime that is.
    It 'keeps an arm64 function''s architecture on a redeploy that names <Case>' -ForEach @(
        @{ Case = 'its own alias'; Extra = @{ RuntimeAlias = 'staging' } }
        @{ Case = 'another alias'; Extra = @{ RuntimeAlias = 'live' } }
        @{ Case = 'another alias and -Handler'; Extra = @{ RuntimeAlias = 'live'; Handler = 'handler.ps1' } }
        @{ Case = 'another runtime'; Extra = @{ RuntimeName = 'other-ps' } }
    ) {
        $script:tuned.Architecture = 'arm64'
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@('x86_64', 'arm64') }
        $splat = $script:redeploy.Clone()
        foreach ($key in $Extra.Keys) { $splat[$key] = $Extra[$key] }

        $result = New-LambdaPSFunctionDeployment @splat

        $result.Architecture | Should -Be 'arm64'
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
    }

    # With nothing reported to check against, the function's own architecture is all there is to go on.
    It 'keeps an arm64 function''s architecture when the alias it is bound to by <Case> reports none' -ForEach @(
        @{ Case = 'default'; Extra = @{} }
        @{ Case = '-RuntimeAlias'; Extra = @{ RuntimeAlias = 'live' } }
        @{ Case = '-RuntimeName'; Extra = @{ RuntimeName = 'other-ps' } }
    ) {
        $script:tuned.Architecture = 'arm64'
        Mock 'Get-LambdaPSRuntimeAliasArchitecture' -ModuleName $script:moduleName -MockWith { , [string[]]@() }
        $splat = $script:redeploy.Clone()
        foreach ($key in $Extra.Keys) { $splat[$key] = $Extra[$key] }

        $result = New-LambdaPSFunctionDeployment @splat -WarningAction SilentlyContinue

        $result.Architecture | Should -Be 'arm64'
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'arm64' }
    }

    It 'moves an arm64 function to the x86_64 the alias it is bound to supports, when that alias has no arm64' {
        $script:tuned.Architecture = 'arm64'

        $result = New-LambdaPSFunctionDeployment @script:redeploy -RuntimeAlias live

        $result.Architecture | Should -Be 'x86_64'
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Architecture -eq 'x86_64' }
    }

    # Only a function bound to a customer-managed runtime can run this code, so any other is refused
    # before anything is packaged, and the preview says so too.
    It 'refuses <Case> before packaging (WhatIf <WhatIf>)' -ForEach @(
        @{ Case = 'a python function'; Runtime = 'python3.12'; PackageType = 'Zip'; Says = "already exists on runtime 'python3.12'"; WhatIf = $false }
        @{ Case = 'a python function'; Runtime = 'python3.12'; PackageType = 'Zip'; Says = "already exists on runtime 'python3.12'"; WhatIf = $true }
        @{ Case = 'a container-image function'; Runtime = $null; PackageType = 'Image'; Says = 'is a container-image function'; WhatIf = $false }
    ) {
        $script:tuned.RuntimeArn = ''
        $script:tuned.Handler = 'lambda_function.lambda_handler'
        $script:tuned.Runtime = $Runtime
        $script:tuned.PackageType = $PackageType

        $err = { New-LambdaPSFunctionDeployment @script:redeploy -WhatIf:$WhatIf } |
            Should -Throw -ErrorId 'FunctionNotRuntimeBound,New-LambdaPSFunctionDeployment' -PassThru

        $err.Exception.Message | Should -BeLike "Function 'hello' $Says and is not bound to a customer-managed runtime, so nothing was packaged or deployed.*"
        Should -Invoke 'Compress-LambdaPSFunctionPackage' -ModuleName $script:moduleName -Times 0 -Exactly
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 0 -Exactly
    }

    # A handler written for another runtime names no script of this package, so the package's own
    # handler replaces it; the Module:: form names none either, and is kept.
    It 'replaces the live handler <Live> of a function moved from another runtime with <Expected>' -ForEach @(
        @{ Live = 'app.handler'; Expected = 'handler.ps1' }
        @{ Live = 'Module::Handlers::Invoke'; Expected = 'Module::Handlers::Invoke' }
    ) {
        $script:tuned.RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/other'
        $script:tuned.Handler = $Live

        $result = New-LambdaPSFunctionDeployment @script:redeploy

        $result.Handler | Should -Be $Expected
        $script:tuned.Handler | Should -Be $Expected
    }

    It 'warns, and keeps the architecture, when the function is bound with no alias and -Architecture is stated' {
        $script:tuned.RuntimeAlias = ''

        $null = New-LambdaPSFunctionDeployment @script:redeploy -Architecture arm64 -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -BeLike '*no alias*-Architecture arm64*was not applied*'
        Should -Invoke 'Update-LMFunctionCode' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $null -eq $Architecture }
    }

    # The live function wins over manifest.json and the folder: they may predate a -Handler or
    # -RuntimeAlias the function was deployed with.
    It 'keeps a tuned handler and alias that manifest.json disagrees with, and says so' {
        $project = Join-Path $TestDrive 'tuned-live'
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'tuned.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Value '{"HandlerString":"handler.ps1","ScriptFile":"handler.ps1","RuntimeAlias":"default"}'
        $script:tuned.Handler = 'tuned.ps1'
        $script:tuned.RuntimeAlias = 'second'
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $project

        $result = New-LambdaPSFunctionDeployment @splat -WarningVariable warnings -WarningAction SilentlyContinue

        $result.Handler | Should -Be 'tuned.ps1'
        $result.RuntimeAlias | Should -Be 'second'
        ($warnings -join ' ') | Should -BeLike "*ScriptFile 'handler.ps1'*keeps its handler*"
        ($warnings -join ' ') | Should -BeLike "*RuntimeAlias 'default'*keeps that alias*"
    }

    It 'says the function is bound with no alias when manifest.json names one' {
        $project = Join-Path $TestDrive 'tuned-no-alias'
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'handler.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Value '{"HandlerString":"handler.ps1","ScriptFile":"handler.ps1","RuntimeAlias":"default"}'
        $script:tuned.Handler = 'handler.ps1'
        $script:tuned.RuntimeAlias = ''
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $project

        $null = New-LambdaPSFunctionDeployment @splat -WarningVariable warnings -WarningAction SilentlyContinue

        ($warnings -join ' ') | Should -BeLike "*RuntimeAlias 'default', but function 'hello' is bound to the runtime with no alias, so it stays that way. Pass -RuntimeAlias to change it.*"
    }

    It 'keeps the live handler when the folder has several root scripts and no manifest' {
        $project = Join-Path $TestDrive 'tuned-two-scripts'
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'main.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'alt.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        $script:tuned.Handler = 'alt.ps1::Handle'
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $project

        $result = New-LambdaPSFunctionDeployment @splat

        $result.Handler | Should -Be 'alt.ps1::Handle'
    }

    It 'falls back to manifest.json when the live handler''s script is gone from the source' {
        $project = Join-Path $TestDrive 'tuned-script-gone'
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'entry.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'other.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Value '{"HandlerString":"entry.ps1","ScriptFile":"entry.ps1"}'
        $script:tuned.Handler = 'removed.ps1'
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $project

        $result = New-LambdaPSFunctionDeployment @splat

        $result.Handler | Should -Be 'entry.ps1'
    }

    It 'uses manifest.json for a function that does not exist yet' {
        Mock 'Get-LambdaPSFunctionBinding' -ModuleName $script:moduleName -MockWith { $null }
        Mock 'Publish-LMFunction' -ModuleName $script:moduleName -MockWith { [PSCustomObject]@{ FunctionArn = 'arn:new' } }
        $project = Join-Path $TestDrive 'new-from-manifest'
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'entry.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Value '{"HandlerString":"entry.ps1","ScriptFile":"entry.ps1","RuntimeAlias":"canary"}'
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $project
        $splat.Role = 'arn:aws:iam::111122223333:role/r'

        $result = New-LambdaPSFunctionDeployment @splat

        $result.Handler | Should -Be 'entry.ps1'
        $result.RuntimeAlias | Should -Be 'canary'
        Should -Invoke 'Publish-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Handler -eq 'entry.ps1' -and $RuntimeConfig_RuntimeAlias -eq 'canary' }
    }

    It 'keeps the live alias over manifest.json''s, and takes the manifest handler when the live script is gone' {
        $project = Join-Path $TestDrive 'tuned-manifest'
        $null = New-Item -ItemType Directory -Path $project -Force
        Set-Content -LiteralPath (Join-Path $project 'entry.ps1') -Value 'param($LambdaInput, $LambdaContext)'
        Set-Content -LiteralPath (Join-Path $project 'manifest.json') -Value '{"HandlerString":"entry.ps1","ScriptFile":"entry.ps1","RuntimeAlias":"canary"}'
        $splat = $script:redeploy.Clone()
        $splat.ProjectPath = $project

        $result = New-LambdaPSFunctionDeployment @splat -WarningAction SilentlyContinue

        $result.RuntimeAlias | Should -Be 'staging'
        $result.Handler | Should -Be 'entry.ps1'
        $result.Timeout | Should -Be 300
    }
}

# Set-DefaultAWSRegion and Set-AWSCredential without -Scope set the region and credentials in the scope
# they run in, such as a deploy script, where the module's own code cannot see them. The deploy reads
# them through its caller's session state. Run in a child pwsh with an empty credential store.
Describe 'New-LambdaPSFunctionDeployment from a script that set its own region and credentials' -Tag 'Unit' {
    It 'deploys into the region and with the credentials the script set' {
        $handler = [System.IO.Path]::Combine($TestDrive, 'scoped-handler.ps1')
        Set-Content -LiteralPath $handler -Value 'param($LambdaInput, $LambdaContext)'
        $emptyStore = [System.IO.Path]::Combine($TestDrive, 'scoped-empty-store')
        Set-Content -LiteralPath $emptyStore -Value ''
        $deployScript = [System.IO.Path]::Combine($TestDrive, 'scoped-deploy.ps1')
        Set-Content -LiteralPath $deployScript -Value @"
Set-DefaultAWSRegion -Region eu-north-1
Set-AWSCredential -AccessKey AKIASCRIPTSCOPE -SecretKey example
New-LambdaPSFunctionDeployment -FunctionName fn -ProjectPath '$handler' -RuntimeName rt -Role arn:aws:iam::111122223333:role/r -PackagePath '$TestDrive/scoped.zip' -Confirm:`$false
"@
        $stub = {
            function script:Get-LambdaPSRuntimeArn { 'arn:aws:lambda:eu-north-1:111122223333:runtime/rt' }
            function script:Get-LambdaPSRuntimeAliasArchitecture { , [string[]]@('x86_64') }
            function script:Get-LambdaPSFunctionBinding { $null }
            function script:Compress-LambdaPSFunctionPackage { [PSCustomObject]@{ PackagePath = $OutFile; ModulesStaged = @() } }
            function script:Publish-LambdaPSFunction {
                param($FunctionName, $Region, $CredentialParameter)
                $key = if ($CredentialParameter.Credential) { $CredentialParameter.Credential.GetCredentials().AccessKey } else { 'none' }
                $script:LambdaPSCalls.Add("Publish $Region $key")
                [PSCustomObject]@{ FunctionName = $FunctionName; Arn = 'arn:aws:lambda:eu-north-1:111122223333:function:fn' }
            }
        }
        # AWS_REGION names another region, so a deploy that misses the script's region goes there instead.
        $command = "`$env:AWS_SHARED_CREDENTIALS_FILE = `$env:AWS_CONFIG_FILE = '$emptyStore'; `$env:AWS_REGION = 'ap-south-1'; `$null = & '$deployScript'"

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command

        $result.Output | Should -Not -BeLike '*ERROR:*'
        $result.Calls | Should -Be @('Publish eu-north-1 AKIASCRIPTSCOPE')
    }
}
