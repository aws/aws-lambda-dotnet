#!/usr/bin/env pwsh
# Runs every integration test project concurrently. Each *.IntegrationTests.csproj deploys its own
# isolated CloudFormation stack (unique name + S3 bucket), so the projects have no shared state and
# can run in parallel. Running them serially was the dominant cost of the CI integ-test phase.
#
# Each project's output is streamed live, prefixed with the project name so the interleaved logs of
# the parallel runs stay attributable. Failed projects also get their full output reprinted as one
# clean block at the end (un-interleaved) for easier diagnosis. The script exits non-zero if any
# project fails, listing which ones.

param(
    [string]$Configuration = "Release",
    # Directory to search for integration test projects (defaults to the Libraries/test tree).
    [string]$TestRoot = (Join-Path $PSScriptRoot ".." "Libraries" "test"),
    # Upper bound on how many projects run at once.
    [int]$ThrottleLimit = 5
)

$ErrorActionPreference = 'Stop'

$projects = Get-ChildItem -Path $TestRoot -Recurse -Filter "*.IntegrationTests.csproj" |
    Select-Object -ExpandProperty FullName |
    Sort-Object

if (-not $projects)
{
    Write-Host "No integration test projects found under '$TestRoot'."
    exit 0
}

Write-Host "Running $($projects.Count) integration test project(s) in parallel (throttle limit $ThrottleLimit):"
$projects | ForEach-Object { Write-Host "  - $_" }

# Build all projects ONCE, up front, before the parallel phase. The integration test projects share
# the IntegrationTests.Helpers ProjectReference; running `dotnet test` on them concurrently each
# rebuilt that shared project, racing on its build output (e.g. IntegrationTests.Helpers.deps.json
# "being used by another process", GenerateDepsFile task failure). Building once here lets the
# parallel runs use --no-build so they only execute tests, never rebuild shared output.
Write-Host "Building all integration test projects once before running in parallel..."
foreach ($project in $projects)
{
    dotnet build -c $Configuration $project
    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Build failed for $project (exit $LASTEXITCODE)."
        exit 1
    }
}

$results = $projects | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
    $project = $_
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    $lines = [System.Collections.Generic.List[string]]::new()
    # --no-build: everything was built serially above, so the parallel runs only execute tests and
    # never rebuild the shared IntegrationTests.Helpers project. 2>&1 folds stderr into the stream;
    # each line is emitted as it arrives, prefixed with the project name, so progress is visible.
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

# Reprint each failed project's output as one clean, un-interleaved block for easier diagnosis.
$failed = $results | Where-Object { $_.ExitCode -ne 0 }
foreach ($result in $failed)
{
    Write-Host ""
    Write-Host "==================== FAILED: $($result.Name) (exit $($result.ExitCode)) ===================="
    Write-Host $result.Output
}

if ($failed)
{
    Write-Host ""
    Write-Host "The following integration test project(s) failed:"
    $failed | ForEach-Object { Write-Host "  - $($_.Name)" }
    exit 1
}

Write-Host ""
Write-Host "All integration test projects passed."
