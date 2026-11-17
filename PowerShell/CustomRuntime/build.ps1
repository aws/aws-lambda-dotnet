#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Reproducible test / lint entrypoint for the AWS.ToolExtras.LambdaPSRuntime module.
.DESCRIPTION
    Runs the Pester test suite (offline; all AWS/container seams are mocked) with code
    coverage over Public/ and Private/, and optionally PSScriptAnalyzer. The suite leaves out the tests
    tagged 'Integration', which deploy to AWS; only -Task Integration runs them. The suite needs an
    AWS.Tools.LambdaCore that exports the Lambda customer-managed runtime cmdlets; set
    LAMBDAPS_AWSTOOLS_MODULE_PATH to use one that is not on PSModulePath. This is the canonical
    way to reproduce the suite locally and in CI so a reviewer sees the same pass/coverage result.

    Test results are written to testResults.xml (NUnit) and coverage to coverage.xml (JaCoCo),
    both git-ignored.
.PARAMETER Task
    Test  - run the Pester suite (default).
    Lint  - run PSScriptAnalyzer: strict over the module source, relaxed over Tests/ and Templates/.
    All   - Lint then Test.
    Package - stage the module to publish, without Tests/, plus the repository's LICENSE, into
            -OutputPath. The release pipeline signs and publishes what it stages.
    Library - build AWS.OCI.Builder (Libraries/src/AWS.OCI.Builder, net8.0, Release) with the .NET SDK and
            copy it into the module's lib/ folder, where the no-Docker image build loads it from. The
            Test task does not need it: the no-Docker seam is mocked.
    Integration - run only the integration suite (every test under Tests/ tagged 'Integration'),
            which deploys each target to AWS and removes it again. The suite's file header says
            what it creates and what it costs. It needs AWS credentials, docker, AWS.Tools.S3 and
            AWS.Tools.CloudWatchLogs, and the environment variables LAMBDAPS_INTEGRATION_REGION,
            LAMBDAPS_INTEGRATION_ACCOUNT and LAMBDAPS_INTEGRATION_ROLE_ARN (the region, the account
            it must run in, and the function role to deploy with).
.PARAMETER OutputPath
    The folder -Task Package stages the module folder into. Default: Deployment/LambdaPSRuntime at the
    repository root.
.PARAMETER SkipCoverage
    Run tests without collecting code coverage (faster).
.EXAMPLE
    pwsh ./build.ps1
    Runs the full Pester test suite with coverage.
.EXAMPLE
    pwsh ./build.ps1 -Task All
    Runs PSScriptAnalyzer, then the test suite.
.EXAMPLE
    pwsh ./build.ps1 -Task Package -OutputPath ./out
    Stages the module to publish into ./out/AWS.ToolExtras.LambdaPSRuntime.
.EXAMPLE
    pwsh ./build.ps1 -Task Integration
    Runs the integration suite against AWS.
#>
[CmdletBinding()]
param(
    [ValidateSet('Test', 'Lint', 'All', 'Integration', 'Package', 'Library')]
    [string]$Task = 'Test',

    [string]$OutputPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, '..', '..', 'Deployment', 'LambdaPSRuntime')),

    [switch]$SkipCoverage
)

$ErrorActionPreference = 'Stop'

# The test suite needs an AWS.Tools.LambdaCore that exports the Lambda customer-managed runtime cmdlets
# (New-LMCRuntime, New-LMCRuntimeVersion, ...). Contract.AwsCmdlet.Tests.ps1 checks every
# parameter the module passes against the REAL cmdlet metadata; without those cmdlets it would skip,
# leaving the one thing the mocked suite cannot verify unverified, so the suite fails up front
# instead. -Task Lint does not need it.
# To use AWS.Tools modules that are not on PSModulePath:
#
#     $env:LAMBDAPS_AWSTOOLS_MODULE_PATH = '/path/to/aws-tools-modules'
#
# The path is prepended HERE, at process scope before Pester starts, rather than inside a test file:
# several test files import AWS.Tools, the first import wins for the whole process, and a later import
# of a different build fails with "Could not load file or assembly 'AWSSDK.Core' ... Assembly with
# same name is already loaded." Prepending first makes every importer resolve the same build.
function Initialize-AWSToolsModulePath {
    if (-not [string]::IsNullOrWhiteSpace($env:LAMBDAPS_AWSTOOLS_MODULE_PATH)) {
        if (-not (Test-Path -LiteralPath $env:LAMBDAPS_AWSTOOLS_MODULE_PATH)) {
            throw "LAMBDAPS_AWSTOOLS_MODULE_PATH '$env:LAMBDAPS_AWSTOOLS_MODULE_PATH' does not exist."
        }
        $awsToolsPath = (Resolve-Path -LiteralPath $env:LAMBDAPS_AWSTOOLS_MODULE_PATH).Path
        # Moved to the front even when it is already on PSModulePath: a pwsh started from another pwsh
        # lists its own module folders before the inherited entries, so the path can be present but last.
        $otherPaths = @($env:PSModulePath -split [System.IO.Path]::PathSeparator) -ne $awsToolsPath
        $env:PSModulePath = (@($awsToolsPath) + $otherPaths) -join [System.IO.Path]::PathSeparator
        Write-Host "AWS.Tools module path prepended: $awsToolsPath"
    }

    # Read the manifests as data. Get-Module -ListAvailable can load a module's assemblies to list its
    # commands, and a loaded AWSSDK.Core from another AWS.Tools build breaks every later import.
    # Import-Module by name takes the first PSModulePath folder that has the module, at its highest
    # version, so only that manifest is checked.
    $lambdaCore = $null
    foreach ($root in ($env:PSModulePath -split [System.IO.Path]::PathSeparator)) {
        if ([string]::IsNullOrWhiteSpace($root)) { continue }
        $manifest = @(Get-ChildItem -Path ([System.IO.Path]::Combine($root, 'AWS.Tools.LambdaCore', '*', 'AWS.Tools.LambdaCore.psd1')) -ErrorAction SilentlyContinue |
                Sort-Object { $version = $null; if ([version]::TryParse($_.Directory.Name, [ref]$version)) { $version } else { [version]'0.0' } } -Descending) |
            Select-Object -First 1
        if (-not $manifest) { continue }
        $exports = @((Import-PowerShellDataFile -LiteralPath $manifest.FullName -ErrorAction SilentlyContinue).CmdletsToExport)
        if ($exports -contains 'New-LMCRuntime') { $lambdaCore = $manifest.FullName }
        break
    }
    if (-not $lambdaCore) {
        throw ('The test suite needs an AWS.Tools.LambdaCore that exports the Lambda customer-managed runtime ' +
            "cmdlets (New-LMCRuntime, ...): the parameter-contract tests check this module's call sites " +
            'against the real cmdlets. None was found on PSModulePath. Install one, or set ' +
            "LAMBDAPS_AWSTOOLS_MODULE_PATH to a directory containing AWS.Tools.*/<version>/, for example:`n" +
            "    `$env:LAMBDAPS_AWSTOOLS_MODULE_PATH = '/path/to/aws-tools-modules'")
    }
}

$moduleRoot = Join-Path $PSScriptRoot 'src/AWS.ToolExtras.LambdaPSRuntime'
$testsPath  = Join-Path $moduleRoot 'Tests'
# Only folders that exist: Pester fails the run on a coverage path that resolves to nothing.
$sourceGlob = @(
    foreach ($folder in 'Public', 'Private') {
        if (Test-Path -LiteralPath (Join-Path $moduleRoot $folder)) { Join-Path $moduleRoot $folder '*.ps1' }
    }
)

# PSScriptAnalyzer runs its rules on parallel threads, and one of them (AvoidReservedCharInCmdlet,
# resolving the .psm1's Export-ModuleMember) intermittently throws NullReferenceException inside the
# analyzer. That is a crash, not a finding, so the path is analyzed once more before it fails the task.
function Invoke-LintPath {
    param([string]$Path, [string]$Settings)
    # Findings are collected first, so a crash part-way through does not report them twice.
    try {
        $findings = @(Invoke-ScriptAnalyzer -Path $Path -Recurse -Settings $Settings -ErrorAction Stop)
        $findings
    }
    catch [System.NullReferenceException] {
        Write-Host "PSScriptAnalyzer crashed on '$Path'; analyzing it again."
        Invoke-ScriptAnalyzer -Path $Path -Recurse -Settings $Settings -ErrorAction Stop
    }
}

# Lint runs in two scopes with two different contracts.
#
#   SOURCE  (Public/, Private/, Config/, *.psm1, *.psd1) is held to the full default rule set with no
#           exclusions, and ANY finding fails the task. The source is kept at zero findings; the handful of
#           cases where a rule is statically wrong are suppressed at the site with a
#           SuppressMessageAttribute + Justification, so the gate stays meaningful instead of being
#           eroded by blanket exclusions. See PSScriptAnalyzerSettings.psd1.
#
#   RELAXED (Tests/, Templates/) is code with legitimately different patterns -- mockable stub
#           signatures, bootstrap console logging. Findings are reported for visibility but only
#           Error severity, and a file that does not parse, fail the task. See
#           PSScriptAnalyzerSettings.Relaxed.psd1.
function Invoke-Lint {
    $minPssa = [version]'1.21.0'
    $pssa = Get-Module -ListAvailable PSScriptAnalyzer |
        Where-Object Version -ge $minPssa | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $pssa) {
        throw "PSScriptAnalyzer >= $minPssa is required for the Lint task. Install with: Install-Module PSScriptAnalyzer -Scope CurrentUser"
    }
    Import-Module $pssa.Path -Force

    $strictSettings  = Join-Path $PSScriptRoot 'PSScriptAnalyzerSettings.psd1'
    $relaxedSettings = Join-Path $PSScriptRoot 'PSScriptAnalyzerSettings.Relaxed.psd1'
    foreach ($required in @($strictSettings, $relaxedSettings)) {
        if (-not (Test-Path -LiteralPath $required)) {
            throw "Required analyzer settings file '$required' is missing; the lint contract is not pinned."
        }
    }

    # --- Scope 1: module source. Any finding is a failure. ---
    $sourcePaths = @(
        (Join-Path $moduleRoot 'Public')
        (Join-Path $moduleRoot 'Private')
        (Join-Path $moduleRoot 'Config')
    ) + @(Get-ChildItem -LiteralPath $moduleRoot -File -Filter '*.ps*1' | Select-Object -ExpandProperty FullName)

    Write-Host 'Running PSScriptAnalyzer (strict) over the module source ...'
    # Invoke-ScriptAnalyzer -Path takes a single path, so each scope is analyzed one path at a time.
    $sourceFindings = @(
        $sourcePaths |
            Where-Object { Test-Path -LiteralPath $_ } |
            ForEach-Object { Invoke-LintPath -Path $_ -Settings $strictSettings }
    )

    if ($sourceFindings.Count -gt 0) {
        $sourceFindings |
            Format-Table @{ n = 'File'; e = { Split-Path -Leaf $_.ScriptPath } }, Line, Severity, RuleName, Message -Wrap |
            Out-String | Write-Host
        throw ("PSScriptAnalyzer reported $($sourceFindings.Count) finding(s) in the module source, " +
            'which is required to be clean. Fix the finding, or -- if the rule is wrong here -- ' +
            'suppress it at the site with [Diagnostics.CodeAnalysis.SuppressMessageAttribute] and a ' +
            'Justification explaining why.')
    }
    Write-Host 'PSScriptAnalyzer (strict): module source clean.'

    # --- Scope 2: tests and templates. Reported; only Error and ParseError severity fail. ---
    $relaxedPaths = @(
        (Join-Path $moduleRoot 'Tests')
        (Join-Path $moduleRoot 'Templates')
    ) | Where-Object { Test-Path -LiteralPath $_ }

    if ($relaxedPaths.Count -gt 0) {
        Write-Host 'Running PSScriptAnalyzer (relaxed) over Tests/ and Templates/ ...'
        $relaxedFindings = @(
            $relaxedPaths | ForEach-Object { Invoke-LintPath -Path $_ -Settings $relaxedSettings }
        )
        $relaxedErrors = @($relaxedFindings | Where-Object { [string]$_.Severity -in 'Error', 'ParseError' })

        if ($relaxedErrors.Count -gt 0) {
            $relaxedErrors |
                Format-Table @{ n = 'File'; e = { Split-Path -Leaf $_.ScriptPath } }, Line, RuleName, Message -Wrap |
                Out-String | Write-Host
            throw "PSScriptAnalyzer reported $($relaxedErrors.Count) error-severity or parse-error finding(s) in Tests/ or Templates/."
        }

        if ($relaxedFindings.Count -gt 0) {
            $relaxedFindings | Group-Object RuleName | Sort-Object Count -Descending |
                Format-Table Count, Name -AutoSize | Out-String | Write-Host
            Write-Host "PSScriptAnalyzer (relaxed): $($relaxedFindings.Count) non-error finding(s), none fatal."
        }
        else {
            Write-Host 'PSScriptAnalyzer (relaxed): clean.'
        }
    }
}

# -Integration runs only the integration suite, without coverage. LAMBDAPS_INTEGRATION_TESTS is what the suite checks
# before it runs, so a Pester run started any other way skips it.
function Invoke-Test {
    param([switch]$Integration)
    $minPester = [version]'5.5.0'
    $pester = Get-Module -ListAvailable Pester |
        Where-Object { $_.Version -ge $minPester -and $_.Version.Major -lt 6 } | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $pester) {
        throw "Pester 5.x ($minPester or later) is required. Install with: Install-PSResource Pester -Version '[$minPester,6.0)' -Scope CurrentUser"
    }
    Import-Module $pester.Path -Force

    $config = New-PesterConfiguration
    $config.Run.Path = $testsPath
    $config.Filter.ExcludeTag = 'Integration'
    if ($Integration) {
        # Every Integration-tagged file under Tests/, so the task runs nothing in a tree that has none.
        $config.Filter.ExcludeTag = @()
        $config.Filter.Tag = 'Integration'
    }
    $config.Run.Throw = $true
    $config.Output.Verbosity = 'Detailed'
    $config.TestResult.Enabled = $true
    $config.TestResult.OutputFormat = 'NUnitXml'
    $config.TestResult.OutputPath = Join-Path $PSScriptRoot 'testResults.xml'

    if (-not $SkipCoverage -and -not $Integration) {
        $config.CodeCoverage.Enabled = $true
        $config.CodeCoverage.Path = $sourceGlob
        $config.CodeCoverage.OutputFormat = 'JaCoCo'
        $config.CodeCoverage.OutputPath = Join-Path $PSScriptRoot 'coverage.xml'
    }

    if (-not $Integration) {
        Invoke-Pester -Configuration $config
        return
    }
    # Set for the integration run only; a value the caller had is put back afterwards.
    $callerSetting = $env:LAMBDAPS_INTEGRATION_TESTS
    $env:LAMBDAPS_INTEGRATION_TESTS = 'true'
    try { Invoke-Pester -Configuration $config }
    finally { $env:LAMBDAPS_INTEGRATION_TESTS = $callerSetting }
}

function Invoke-Package {
    $destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
        [System.IO.Path]::Combine($OutputPath, 'AWS.ToolExtras.LambdaPSRuntime'))
    $source = [System.IO.Path]::GetFullPath($moduleRoot)
    if ($destination -eq $source -or $destination.StartsWith($source + [System.IO.Path]::DirectorySeparatorChar)) {
        throw "-OutputPath '$OutputPath' would stage the module over its own source."
    }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $destination
    Get-ChildItem -LiteralPath $moduleRoot -Force | Where-Object Name -ne 'Tests' |
        Copy-Item -Destination $destination -Recurse -Force
    # bin/ and obj/ are left by local builds of a template project.
    Get-ChildItem -LiteralPath $destination -Directory -Recurse -Force | Where-Object Name -in 'bin', 'obj' | Remove-Item -Recurse -Force
    Copy-Item -LiteralPath ([System.IO.Path]::Combine($PSScriptRoot, '..', '..', 'LICENSE')) -Destination $destination
    Write-Host "Module staged in $destination"
}

# net8.0 loads on every supported pwsh (7.4 = .NET 8, 7.5 = .NET 9, 7.6 = .NET 10). The library is built
# from the same commit as the module, so there is no version to pin.
function Invoke-LibraryBuild {
    $dotnet = Get-Command -Name 'dotnet' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $dotnet) { throw 'The Library task needs the .NET SDK (8.0 or later) on PATH: https://dot.net/download.' }
    $project = Join-Path $PSScriptRoot '..' '..' 'Libraries' 'src' 'AWS.OCI.Builder' 'AWS.OCI.Builder.csproj'
    $output = Join-Path ([System.IO.Path]::GetTempPath()) "lambdaps-oci-builder-$([System.IO.Path]::GetRandomFileName())"
    try {
        & $dotnet.Source build $project -c Release -f net8.0 -o $output -nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet build of '$project' failed (exit code $LASTEXITCODE)." }
        $lib = Join-Path $moduleRoot 'lib'
        $null = New-Item -ItemType Directory -Path $lib -Force
        Copy-Item -LiteralPath (Join-Path $output 'AWS.OCI.Builder.dll') -Destination $lib -Force
        Write-Host "AWS.OCI.Builder $([System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $lib 'AWS.OCI.Builder.dll')).Version) copied to $lib"
    }
    finally { Remove-Item -LiteralPath $output -Recurse -Force -ErrorAction SilentlyContinue }
}

# Before anything else runs: whatever AWS.Tools build loads first is what the tests get.
if ($Task -in 'Test', 'All', 'Integration') {
    Initialize-AWSToolsModulePath
}

switch ($Task) {
    'Lint' { Invoke-Lint }
    'Test' { Invoke-Test }
    'Integration' { Invoke-Test -Integration }
    'Package' { Invoke-Package }
    'Library' { Invoke-LibraryBuild }
    'All'  {
        # Lint runs in its own pwsh: PSScriptAnalyzer loads AWS.Tools assemblies while it resolves
        # commands, and an assembly loaded into this process cannot be replaced by the build the tests
        # import.
        & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -File $PSCommandPath -Task Lint
        if ($LASTEXITCODE -ne 0) { throw "The Lint task failed (exit code $LASTEXITCODE)." }
        Invoke-Test
    }
}
