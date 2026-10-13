BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    $script:buildScript = [System.IO.Path]::Combine((Split-Path -Parent (Split-Path -Parent $script:moduleRoot)), 'build.ps1')
}

AfterAll {
    Remove-LambdaPSTestModule
}

# PSScriptAnalyzer loads AWS.Tools assemblies while it resolves commands. In the process that then
# runs the tests, every later import of a different AWS.Tools build fails.
Describe 'build.ps1 -Task All' -Tag 'Component' {
    It 'runs Lint in its own pwsh process, not in the process that runs the tests' {
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($script:buildScript, [ref]$null, [ref]$null)
        $switch = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.SwitchStatementAst] -and $node.Condition.Extent.Text -eq '$Task' }, $true)
        $all = @($switch.Clauses | Where-Object { $_.Item1.Extent.Text -eq "'All'" })[0].Item2
        $commands = @($all.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true))

        @($commands | Where-Object { $_.GetCommandName() -eq 'Invoke-Lint' }) | Should -BeNullOrEmpty
        @($commands | Where-Object { $_.Extent.Text -like '*-File $PSCommandPath -Task Lint*' }) | Should -HaveCount 1
        @($commands | Where-Object { $_.GetCommandName() -eq 'Invoke-Test' }) | Should -HaveCount 1
    }
}

# A file that does not parse yields only ParseError records, so a lint that leaves that severity out
# passes it. Each case lints a small tree of its own with a copy of build.ps1 and the two settings
# files, in a child pwsh.
Describe 'build.ps1 -Task Lint and parse errors' -Tag 'Component' {
    BeforeAll {
        # Builds a tree with one clean Private/ file, plus $BrokenFile (relative to the module folder)
        # holding a script with a missing brace, and runs -Task Lint on it. Returns the exit code and output.
        function Invoke-LintOnTree {
            param([string]$Name, [string]$BrokenFile)
            $root = [System.IO.Path]::Combine($TestDrive, $Name)
            $module = [System.IO.Path]::Combine($root, 'src', 'AWS.ToolExtras.LambdaPSRuntime')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($module, 'Private')) -Force
            $repository = Split-Path -Parent $script:buildScript
            foreach ($file in 'build.ps1', 'PSScriptAnalyzerSettings.psd1', 'PSScriptAnalyzerSettings.Relaxed.psd1') {
                Copy-Item -LiteralPath ([System.IO.Path]::Combine($repository, $file)) -Destination $root
            }
            Set-Content -LiteralPath ([System.IO.Path]::Combine($module, 'Private', 'Get-LambdaPSClean.ps1')) -Value "function Get-LambdaPSClean {`n    [CmdletBinding()]`n    [OutputType([string])]`n    param()`n    'clean'`n}"
            if ($BrokenFile) {
                $broken = [System.IO.Path]::Combine($module, $BrokenFile)
                $null = New-Item -ItemType Directory -Path (Split-Path -Parent $broken) -Force
                Set-Content -LiteralPath $broken -Value "Describe 'broken' {`n    It 'never closes' {`n        1 | Should -Be 1`n"
            }
            $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -File ([System.IO.Path]::Combine($root, 'build.ps1')) -Task Lint 2>&1 | Out-String
            [PSCustomObject]@{ ExitCode = $LASTEXITCODE; Output = $output }
        }
    }

    It 'reports ParseError with each settings file' -ForEach @(
        @{ Settings = 'PSScriptAnalyzerSettings.psd1' }
        @{ Settings = 'PSScriptAnalyzerSettings.Relaxed.psd1' }
    ) {
        $fixture = [System.IO.Path]::Combine($TestDrive, "broken-$Settings.ps1")
        Set-Content -LiteralPath $fixture -Value "function Get-Broken {`n    'never closes'`n"
        $settingsPath = [System.IO.Path]::Combine((Split-Path -Parent $script:buildScript), $Settings)
        $command = "Import-Module PSScriptAnalyzer; @(Invoke-ScriptAnalyzer -Path '$fixture' -Settings '$settingsPath' | ForEach-Object { 'FINDING:' + `$_.Severity }) -join ','"

        $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command $command 2>&1 | Out-String

        $output | Should -BeLike '*FINDING:ParseError*'
    }

    It 'fails when a file in Tests/ does not parse' {
        $result = Invoke-LintOnTree -Name 'lint-broken-test' -BrokenFile ([System.IO.Path]::Combine('Tests', 'Broken.Tests.ps1'))

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -BeLike '*parse-error finding(s)*'
    }

    It 'fails when a file in the module source does not parse' {
        $result = Invoke-LintOnTree -Name 'lint-broken-source' -BrokenFile ([System.IO.Path]::Combine('Private', 'Get-LambdaPSBroken.ps1'))

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -BeLike '*finding(s) in the module source*'
    }

    It 'passes a tree whose files all parse' {
        $result = Invoke-LintOnTree -Name 'lint-clean'

        $result.ExitCode | Should -Be 0
        $result.Output | Should -BeLike '*PSScriptAnalyzer (strict): module source clean.*'
    }
}

# The integration suite deploys to AWS, so only -Task Integration runs it. Invoke-Test is read out of build.ps1 and
# run here with Invoke-Pester and Import-Module stood in for, so nothing is re-imported or run.
Describe 'build.ps1 integration suite' -Tag 'Component' {
    BeforeAll {
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($script:buildScript, [ref]$null, [ref]$null)
        $definition = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-Test'
            }, $true)
        # Written to a file of its own, so $PSScriptRoot in it is a folder of this test's.
        $script:invokeTestFile = [System.IO.Path]::Combine($TestDrive, 'Invoke-Test.ps1')
        Set-Content -LiteralPath $script:invokeTestFile -Value $definition.Extent.Text
        $script:switch = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.SwitchStatementAst] -and $node.Condition.Extent.Text -eq '$Task' }, $true)
    }

    It 'leaves the Integration tag out of the suite, and runs only the integration suite, without coverage, for -Integration' {
        # Invoke-Test -Integration sets and then removes LAMBDAPS_INTEGRATION_TESTS: run it with the variable unset,
        # and put back whatever value the caller had.
        $savedIntegrationSetting = $env:LAMBDAPS_INTEGRATION_TESTS
        Remove-Item -Path Env:LAMBDAPS_INTEGRATION_TESTS -ErrorAction SilentlyContinue
        try {
            $configurations = [System.Collections.Generic.List[object]]::new()
            $integrationSetting = [System.Collections.Generic.List[object]]::new()
            Mock Import-Module { }
            function Invoke-Pester { param($Configuration) $configurations.Add($Configuration); $integrationSetting.Add($env:LAMBDAPS_INTEGRATION_TESTS) }
            $testsPath = [System.IO.Path]::Combine($TestDrive, 'Tests')
            # Read by the dot-sourced Invoke-Test, as build.ps1's script scope would provide them.
            Set-Variable -Name sourceGlob -Value @([System.IO.Path]::Combine($TestDrive, 'Public', '*.ps1'))
            Set-Variable -Name SkipCoverage -Value $false
            . $script:invokeTestFile

            Invoke-Test
            Invoke-Test -Integration

            $suite, $integration = $configurations
            $suite.Run.Path.Value | Should -Be $testsPath
            $suite.Filter.ExcludeTag.Value | Should -Be 'Integration'
            $suite.CodeCoverage.Enabled.Value | Should -BeTrue
            $integration.Run.Path.Value | Should -Be $testsPath
            $integration.Filter.Tag.Value | Should -Be 'Integration'
            @($integration.Filter.ExcludeTag.Value) | Should -BeNullOrEmpty
            $integration.CodeCoverage.Enabled.Value | Should -BeFalse
            # The suite checks LAMBDAPS_INTEGRATION_TESTS, so it is set for the integration run only, and removed after it.
            $integrationSetting[0] | Should -BeNullOrEmpty
            $integrationSetting[1] | Should -Be 'true'
            $env:LAMBDAPS_INTEGRATION_TESTS | Should -BeNullOrEmpty
        }
        finally {
            if ($null -ne $savedIntegrationSetting) { $env:LAMBDAPS_INTEGRATION_TESTS = $savedIntegrationSetting }
        }
    }

    It 'puts back a LAMBDAPS_INTEGRATION_TESTS value the caller had after the integration run' {
        $savedIntegrationSetting = $env:LAMBDAPS_INTEGRATION_TESTS
        $env:LAMBDAPS_INTEGRATION_TESTS = 'caller value'
        try {
            Mock Import-Module { }
            function Invoke-Pester { param($Configuration) }
            Set-Variable -Name testsPath -Value ([System.IO.Path]::Combine($TestDrive, 'Tests'))
            Set-Variable -Name sourceGlob -Value @([System.IO.Path]::Combine($TestDrive, 'Public', '*.ps1'))
            Set-Variable -Name SkipCoverage -Value $false
            . $script:invokeTestFile

            Invoke-Test -Integration

            $env:LAMBDAPS_INTEGRATION_TESTS | Should -Be 'caller value'
        }
        finally {
            $env:LAMBDAPS_INTEGRATION_TESTS = $savedIntegrationSetting
        }
    }

    # A copy of build.ps1 next to a Tests folder of the test's own, run for real in a child pwsh.
    It 'runs every Integration-tagged file for -Task Integration, and nothing in a tree that has none' {
        $marks = [System.IO.Path]::Combine($TestDrive, 'marks')
        $null = New-Item -ItemType Directory -Path $marks -Force
        $repo = [System.IO.Path]::Combine($TestDrive, 'integration-repo')
        $tests = [System.IO.Path]::Combine($repo, 'src', 'AWS.ToolExtras.LambdaPSRuntime', 'Tests')
        $null = New-Item -ItemType Directory -Path $tests -Force
        Copy-Item -LiteralPath $script:buildScript -Destination $repo
        foreach ($name in 'Integration.One', 'Integration.Two') {
            Set-Content -LiteralPath ([System.IO.Path]::Combine($tests, "$name.Tests.ps1")) -Value "Describe '$name' -Tag 'Integration' { It 'runs' { New-Item -ItemType File -Path '$([System.IO.Path]::Combine($marks, $name))' -Force } }"
        }
        Set-Content -LiteralPath ([System.IO.Path]::Combine($tests, 'Unit.Other.Tests.ps1')) -Value "Describe 'Other' -Tag 'Unit' { It 'runs' { New-Item -ItemType File -Path '$([System.IO.Path]::Combine($marks, 'Unit.Other'))' -Force } }"
        $empty = [System.IO.Path]::Combine($TestDrive, 'empty-repo')
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($empty, 'src', 'AWS.ToolExtras.LambdaPSRuntime', 'Tests')) -Force
        Copy-Item -LiteralPath $script:buildScript -Destination $empty
        Copy-Item -LiteralPath ([System.IO.Path]::Combine($tests, 'Unit.Other.Tests.ps1')) -Destination ([System.IO.Path]::Combine($empty, 'src', 'AWS.ToolExtras.LambdaPSRuntime', 'Tests'))
        $pwsh = (Get-Process -Id $PID).Path

        $null = & $pwsh -NoProfile -NonInteractive -File ([System.IO.Path]::Combine($repo, 'build.ps1')) -Task Integration 2>&1
        $withIntegration = $LASTEXITCODE
        $marked = @(Get-ChildItem -LiteralPath $marks -Name | Sort-Object)
        Remove-Item -Path ([System.IO.Path]::Combine($marks, '*'))
        $null = & $pwsh -NoProfile -NonInteractive -File ([System.IO.Path]::Combine($empty, 'build.ps1')) -Task Integration 2>&1
        $withoutIntegration = $LASTEXITCODE

        $withIntegration | Should -Be 0
        $marked | Should -Be @('Integration.One', 'Integration.Two')
        $withoutIntegration | Should -Be 0
        @(Get-ChildItem -LiteralPath $marks -Name) | Should -BeNullOrEmpty
    }

    # Where the integration suite runs comes from the caller, not from build.ps1.
    It 'documents the variables -Task Integration needs, and names no region or account' {
        $help = [regex]::Match((Get-Content -LiteralPath $script:buildScript -Raw), '(?s)\.PARAMETER Task\s(.*?)\.PARAMETER').Groups[1].Value
        $help | Should -Match 'Integration'

        foreach ($name in 'LAMBDAPS_INTEGRATION_REGION', 'LAMBDAPS_INTEGRATION_ACCOUNT', 'LAMBDAPS_INTEGRATION_ROLE_ARN') { $help | Should -Match $name }
        foreach ($module in 'AWS.Tools.S3', 'AWS.Tools.CloudWatchLogs') { $help | Should -Match ([regex]::Escape($module)) }
        $help | Should -Not -Match '\b[a-z]{2}(-gov)?-[a-z]+-\d\b'
        $help | Should -Not -Match '\b\d{12}\b'
    }

    It 'runs the integration suite for -Task Integration and for no other task' {
        $clauses = @($script:switch.Clauses | Where-Object {
                @($_.Item2.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] -and $node.GetCommandName() -eq 'Invoke-Test' }, $true) |
                        Where-Object { $_.Extent.Text -like '*-Integration*' }).Count -gt 0
            })
        @($clauses | ForEach-Object { $_.Item1.Extent.Text }) | Should -Be @("'Integration'")
    }
}

# build.ps1 puts LAMBDAPS_AWSTOOLS_MODULE_PATH first on PSModulePath, so every test that imports AWS.Tools
# loads that build. Its Initialize-AWSToolsModulePath is read out of the script and run here.
Describe 'build.ps1 AWS.Tools module path' -Tag 'Component' {
    BeforeAll {
        $buildScript = [System.IO.Path]::Combine((Split-Path -Parent (Split-Path -Parent $script:moduleRoot)), 'build.ps1')
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($buildScript, [ref]$null, [ref]$null)
        $definition = $ast.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Initialize-AWSToolsModulePath'
            }, $true)
        . ([scriptblock]::Create($definition.Extent.Text))

        # A stand-in AWS.Tools folder whose LambdaCore manifest exports a CMR cmdlet.
        $script:awsToolsPath = [System.IO.Path]::Combine($TestDrive, 'aws-tools')
        $manifestFolder = [System.IO.Path]::Combine($script:awsToolsPath, 'AWS.Tools.LambdaCore', '5.0.0')
        $null = New-Item -ItemType Directory -Path $manifestFolder
        Set-Content -LiteralPath ([System.IO.Path]::Combine($manifestFolder, 'AWS.Tools.LambdaCore.psd1')) -Value "@{ ModuleVersion = '5.0.0'; CmdletsToExport = @('New-LMCRuntime') }"
        $script:otherPath = [System.IO.Path]::Combine($TestDrive, 'other-modules')
    }

    BeforeEach {
        $script:savedModulePath = $env:PSModulePath
        $script:savedAwsToolsPath = $env:LAMBDAPS_AWSTOOLS_MODULE_PATH
    }

    AfterEach {
        $env:PSModulePath = $script:savedModulePath
        $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = $script:savedAwsToolsPath
    }

    # A pwsh started from another pwsh lists its own module folders before the inherited entries.
    It 'moves the path to the front when PSModulePath already holds it later' {
        $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = $script:awsToolsPath
        $env:PSModulePath = $script:otherPath + [System.IO.Path]::PathSeparator + $script:awsToolsPath

        $null = Initialize-AWSToolsModulePath 6>&1

        $env:PSModulePath -split [System.IO.Path]::PathSeparator | Should -Be @($script:awsToolsPath, $script:otherPath)
    }

    It 'prepends the path when PSModulePath does not hold it' {
        $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = $script:awsToolsPath
        $env:PSModulePath = $script:otherPath

        $null = Initialize-AWSToolsModulePath 6>&1

        $env:PSModulePath -split [System.IO.Path]::PathSeparator | Should -Be @($script:awsToolsPath, $script:otherPath)
    }
    # Import-Module by name takes the first PSModulePath folder that has the module, so only that one counts.
    It 'refuses when the first folder holding AWS.Tools.LambdaCore lacks the CMR cmdlets, even if a later one has them' {
        $oldTools = [System.IO.Path]::Combine($TestDrive, 'old-aws-tools')
        $oldFolder = [System.IO.Path]::Combine($oldTools, 'AWS.Tools.LambdaCore', '4.1.0')
        $null = New-Item -ItemType Directory -Path $oldFolder -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($oldFolder, 'AWS.Tools.LambdaCore.psd1')) -Value "@{ ModuleVersion = '4.1.0'; CmdletsToExport = @('Get-LMCOther') }"
        $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = $null
        $env:PSModulePath = $oldTools + [System.IO.Path]::PathSeparator + $script:awsToolsPath

        { Initialize-AWSToolsModulePath 6>&1 } | Should -Throw '*needs an AWS.Tools.LambdaCore that exports*'
    }

    It 'accepts the first folder holding AWS.Tools.LambdaCore when it has the CMR cmdlets' {
        $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = $null
        $env:PSModulePath = $script:otherPath + [System.IO.Path]::PathSeparator + $script:awsToolsPath

        { Initialize-AWSToolsModulePath 6>&1 } | Should -Not -Throw
    }
}
