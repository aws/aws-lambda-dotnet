#!/usr/bin/env pwsh
# Discovers and runs every unit-test project under Libraries/test, so new test projects are picked up
# automatically instead of having to be added to a hardcoded list in build.proj (which is how projects
# such as Amazon.Lambda.AspNetCoreServer.Test, Amazon.Lambda.Core.Tests and EventsTests.NET8 silently
# stopped being run in CI).
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
# The script exits non-zero if any project fails, listing which ones.

param(
    [string]$Configuration = "Release",
    # Directory to search for unit-test projects (defaults to the Libraries/test tree).
    [string]$TestRoot = (Join-Path $PSScriptRoot ".." "Libraries" "test")
)

$ErrorActionPreference = 'Stop'

# Discover unit-test projects: reference the test SDK / IsTestProject, but are not integration tests.
$projects = Get-ChildItem -Path $TestRoot -Recurse -Filter "*.csproj" |
    Where-Object { $_.Name -notlike "*.IntegrationTests.csproj" } |
    Where-Object {
        $content = Get-Content -Raw -LiteralPath $_.FullName
        ($content -match 'Microsoft\.NET\.Test\.Sdk') -or
        ($content -match '<IsTestProject>\s*true\s*</IsTestProject>')
    } |
    Select-Object -ExpandProperty FullName |
    Sort-Object

if (-not $projects)
{
    Write-Host "No unit-test projects found under '$TestRoot'."
    exit 0
}

Write-Host "Running $($projects.Count) unit-test project(s):"
$projects | ForEach-Object { Write-Host "  - $_" }

$failed = @()
foreach ($project in $projects)
{
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host ""
    Write-Host "==================== $name ===================="
    dotnet test -c $Configuration $project
    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Tests failed for $name (exit $LASTEXITCODE)."
        $failed += $name
    }
}

if ($failed)
{
    Write-Host ""
    Write-Host "The following unit-test project(s) failed:"
    $failed | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host ""
Write-Host "All unit-test projects passed."
