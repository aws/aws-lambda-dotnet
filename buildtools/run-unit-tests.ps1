#!/usr/bin/env pwsh
# Discovers and runs every unit-test project under Libraries/test in parallel, so new test projects
# are picked up automatically instead of having to be added to a hardcoded list in build.proj (which
# is how projects such as Amazon.Lambda.AspNetCoreServer.Test, Amazon.Lambda.Core.Tests and
# EventsTests.NET8 silently stopped being run in CI).
#
# A project is treated as a unit-test project when its .csproj references Microsoft.NET.Test.Sdk (or
# sets <IsTestProject>true</IsTestProject>). Naming is NOT used because the test projects in this repo
# follow no single convention (*.Test, *.Tests, EventsTests.NET8, PowerShellTests, ...). Non-test
# fixture projects (TestServerlessApp, TestWebApp, HandlerTest, ...) don't reference the test SDK, so
# this content-based filter cleanly excludes them.
#
# Integration test projects (*.IntegrationTests.csproj) are excluded here because they run in a
# separate phase via run-integ-tests-parallel.ps1 (they deploy real AWS resources).
#
# Mirrors run-integ-tests-parallel.ps1: each project's output is streamed live, prefixed with the
# project name so interleaved logs stay attributable, and failed projects get their full output
# reprinted as one clean block at the end. All projects run (it does not short-circuit) so every
# failure is surfaced in a single pass, then it exits non-zero listing which projects failed.

param(
    [string]$Configuration = "Release",
    # Directory to search for unit-test projects (defaults to the Libraries/test tree).
    [string]$TestRoot = (Join-Path $PSScriptRoot ".." "Libraries" "test"),
    # Upper bound on how many projects run at once.
    [int]$ThrottleLimit = 5
)

$ErrorActionPreference = 'Stop'

# Projects that must NOT run concurrently with the parallel pool. Amazon.Lambda.AspNetCoreServer.Test
# drives SnapStart initialization that invokes before-snapshot hooks accumulated in a process-global
# registry, each running in-process ASP.NET requests; under the CPU/host contention of the parallel
# pool that work deadlocked and hung CI until the credentials expired. It passes fine on its own, so
# it is run serially, after the parallel pool. Matched on the file name.
$serialProjectNames = @(
    'Amazon.Lambda.AspNetCoreServer.Test.csproj'
)

# Discover unit-test projects: reference the test SDK / IsTestProject, but are not integration tests.
$allProjects = Get-ChildItem -Path $TestRoot -Recurse -Filter "*.csproj" |
    Where-Object { $_.Name -notlike "*.IntegrationTests.csproj" } |
    Where-Object {
        $content = Get-Content -Raw -LiteralPath $_.FullName
        ($content -match 'Microsoft\.NET\.Test\.Sdk') -or
        ($content -match '<IsTestProject>\s*true\s*</IsTestProject>')
    } |
    Select-Object -ExpandProperty FullName |
    Sort-Object

if (-not $allProjects)
{
    Write-Host "No unit-test projects found under '$TestRoot'."
    exit 0
}

$serialProjects = $allProjects | Where-Object { $serialProjectNames -contains [System.IO.Path]::GetFileName($_) }
$parallelProjects = $allProjects | Where-Object { $serialProjectNames -notcontains [System.IO.Path]::GetFileName($_) }

Write-Host "Discovered $($allProjects.Count) unit-test project(s):"
Write-Host "  Parallel (throttle limit $ThrottleLimit):"
$parallelProjects | ForEach-Object { Write-Host "    - $_" }
if ($serialProjects)
{
    Write-Host "  Serial (run one at a time, after the parallel pool):"
    $serialProjects | ForEach-Object { Write-Host "    - $_" }
}

# Build all projects ONCE, up front, before the parallel phase. The test projects share
# ProjectReferences (e.g. TestWebApp, TestUtilities); running `dotnet test` on them concurrently
# each rebuilt those shared projects, racing on their build output (e.g. a .deps.json "being used by
# another process" / GenerateDepsFile failure). Building once here lets the runs use --no-build so
# they only execute tests, never rebuild shared output.
Write-Host "Building all unit-test projects once before running..."
foreach ($project in $allProjects)
{
    dotnet build -c $Configuration $project
    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Build failed for $project (exit $LASTEXITCODE)."
        exit 1
    }
}

$results = @()

if ($parallelProjects)
{
    $results += $parallelProjects | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
        $project = $_
        $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $lines = [System.Collections.Generic.List[string]]::new()
        # --no-build: everything was built serially above, so the runs only execute tests and never
        # rebuild shared project output. 2>&1 folds stderr into the stream; each line is emitted as it
        # arrives, prefixed with the project name, so progress is visible.
        dotnet test -c $using:Configuration --no-build --logger "console;verbosity=detailed" $project 2>&1 |
            ForEach-Object {
                $line = $_.ToString()
                $lines.Add($line)
                Write-Host "[$name] $line"
            }
        [PSCustomObject]@{
            Name     = $name
            Project  = $project
            ExitCode = $LASTEXITCODE
            Output   = ($lines -join [System.Environment]::NewLine)
        }
    }
}

# Run the serial projects one at a time, after the parallel pool has finished, so their process-global
# behavior can't interfere with (or be starved by) the concurrent runs.
foreach ($project in $serialProjects)
{
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host ""
    Write-Host "==================== $name (serial) ===================="
    dotnet test -c $Configuration --no-build --logger "console;verbosity=detailed" $project
    $results += [PSCustomObject]@{
        Name     = $name
        Project  = $project
        ExitCode = $LASTEXITCODE
        Output   = ""
    }
}

# Reprint each failed project's output as one clean, un-interleaved block for easier diagnosis.
$failed = $results | Where-Object { $_.ExitCode -ne 0 }
foreach ($result in $failed)
{
    if ($result.Output)
    {
        Write-Host ""
        Write-Host "==================== FAILED: $($result.Name) (exit $($result.ExitCode)) ===================="
        Write-Host $result.Output
    }
}

if ($failed)
{
    Write-Host ""
    Write-Host "The following unit-test project(s) failed:"
    $failed | ForEach-Object { Write-Host "  - $($_.Name)" }
    exit 1
}

Write-Host ""
Write-Host "All unit-test projects passed."
