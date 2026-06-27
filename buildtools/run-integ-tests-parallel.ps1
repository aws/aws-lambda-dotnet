#!/usr/bin/env pwsh
# Runs every integration test project concurrently. Each *.IntegrationTests.csproj deploys its own
# isolated CloudFormation stack (unique name + S3 bucket), so the projects have no shared state and
# can run in parallel. Running them serially was the dominant cost of the CI integ-test phase.
#
# Output from each project is captured and printed as a labeled block after that project finishes,
# so the interleaved logs of parallel runs stay readable. The script exits non-zero if any project
# fails, listing which ones.

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

$results = $projects | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
    $project = $_
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    # 2>&1 folds stderr into the captured stream so warnings/errors appear in the labeled block.
    $output = dotnet test -c $using:Configuration --logger "console;verbosity=detailed" $project 2>&1 | Out-String
    [PSCustomObject]@{
        Name     = $name
        Project  = $project
        ExitCode = $LASTEXITCODE
        Output   = $output
    }
}

foreach ($result in $results)
{
    Write-Host ""
    Write-Host "==================== $($result.Name) (exit $($result.ExitCode)) ===================="
    Write-Host $result.Output
}

$failed = $results | Where-Object { $_.ExitCode -ne 0 }
if ($failed)
{
    Write-Host ""
    Write-Host "The following integration test project(s) failed:"
    $failed | ForEach-Object { Write-Host "  - $($_.Name)" }
    exit 1
}

Write-Host ""
Write-Host "All integration test projects passed."
