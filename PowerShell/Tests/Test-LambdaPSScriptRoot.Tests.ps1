#Requires -Modules Pester

<#
.SYNOPSIS
    Pester 5 E2E tests for PowerShell Lambda runtime behavior.

.EXAMPLE
    $container = New-PesterContainer -Path ./Test-LambdaPSScriptRoot.Tests.ps1 -Data @{
        ProfileName = 'myprofile'; Region = 'us-west-2'; Runtime = 'dotnet10'
    }
    Invoke-Pester -Container $container -Output Detailed
#>

param(
    [Parameter(Mandatory)]
    [string]$ProfileName,

    [string]$Region = 'us-west-2',

    [ValidateSet('dotnet8', 'dotnet10')]
    [string]$Runtime = 'dotnet10'
)

BeforeAll {
    $script:ProfileName = $ProfileName
    $script:Region = $Region
    $script:Runtime = $Runtime
    $script:RepoRoot = (Resolve-Path "$PSScriptRoot/../../").Path
    $script:RunId = [System.Guid]::NewGuid().ToString('N').Substring(0, 8)
    $script:RoleName = "ps-lambda-test-$script:RunId"
    $script:PolicyArn = 'arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole'
    $script:DeployedFunctions = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
    $script:TrustPolicy = @'
{
    "Version": "2012-10-17",
    "Statement": [{
        "Effect": "Allow",
        "Principal": { "Service": "lambda.amazonaws.com" },
        "Action": "sts:AssumeRole"
    }]
}
'@

    . "$PSScriptRoot/LambdaTestHelpers.ps1"

    $script:RoleArn = New-TestRole
    Start-Sleep -Seconds 10
}

AfterAll {
    . "$PSScriptRoot/LambdaTestHelpers.ps1"
    Remove-TestResources
}

Describe 'Automatic Variables' {
    It 'populates $PSScriptRoot, $PSCommandPath, and $MyInvocation correctly' {
        $fnName = "ps-test-auto-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
$result = [ordered]@{
    PSScriptRoot             = $PSScriptRoot
    PSCommandPath            = $PSCommandPath
    MyInvocation_Path        = $MyInvocation.MyCommand.Path
    MyInvocation_Name        = $MyInvocation.MyCommand.Name
    MyInvocation_CommandType = [string]$MyInvocation.MyCommand.CommandType
    MyInvocation_Source      = $MyInvocation.MyCommand.Source
    LambdaTaskRoot           = $env:LAMBDA_TASK_ROOT
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName

            $r.PSScriptRoot | Should -Be '/var/task'
            $r.PSCommandPath | Should -Be '/var/task/handler.ps1'
            $r.MyInvocation_Path | Should -Be '/var/task/handler.ps1'
            $r.MyInvocation_Name | Should -Be 'handler.ps1'
            $r.MyInvocation_CommandType | Should -Be 'ExternalScript'
            $r.MyInvocation_Source | Should -Be '/var/task/handler.ps1'
            $r.LambdaTaskRoot | Should -Be '/var/task'
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Injected Variables' {
    It '$LambdaInput, $LambdaContext, and $LambdaInputString are accessible' {
        $fnName = "ps-test-inj-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
$result = [ordered]@{
    LambdaInput_Exists         = $null -ne $LambdaInput
    LambdaInput_TestKey        = $LambdaInput.testKey
    LambdaContext_Exists       = $null -ne $LambdaContext
    LambdaContext_FunctionName = if ($null -ne $LambdaContext) { $LambdaContext.FunctionName } else { 'null' }
    LambdaInputString_Exists   = $null -ne $LambdaInputString
    LambdaInputString_Type     = if ($null -ne $LambdaInputString) { $LambdaInputString.GetType().Name } else { 'null' }
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'

            $r.LambdaInput_Exists | Should -BeTrue
            $r.LambdaInput_TestKey | Should -Be 'hello-lambda'
            $r.LambdaContext_Exists | Should -BeTrue
            $r.LambdaContext_FunctionName | Should -Be $fnName
            $r.LambdaInputString_Exists | Should -BeTrue
            $r.LambdaInputString_Type | Should -Be 'String'
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Environment Normalization' {
    It '$env:TEMP, TMP, TMPDIR, HOME are set and writable' {
        $fnName = "ps-test-env-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
$testFile = Join-Path $env:TEMP "write-test-$(Get-Random).txt"
$writable = $false
try {
    Set-Content -Path $testFile -Value "test"
    if (Test-Path $testFile) { $writable = $true; Remove-Item $testFile -ErrorAction SilentlyContinue }
} catch {}

$result = [ordered]@{
    TEMP             = $env:TEMP
    TMP              = $env:TMP
    TMPDIR           = $env:TMPDIR
    HOME             = $env:HOME
    LAMBDA_TASK_ROOT = $env:LAMBDA_TASK_ROOT
    TempWritable     = $writable
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName

            $r.TEMP | Should -Be '/tmp'
            $r.TMP | Should -Be '/tmp'
            $r.TMPDIR | Should -Be '/tmp'
            $r.HOME | Should -Be '/tmp/home'
            $r.LAMBDA_TASK_ROOT | Should -Be '/var/task'
            $r.TempWritable | Should -BeTrue
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'PowerShellFunctionName' {
    It 'calls the named function with $LambdaInput and $LambdaContext' {
        $fnName = "ps-test-fn-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
function Invoke-LambdaHandler {
    param($LambdaInput, $LambdaContext)
    $result = [ordered]@{
        FunctionCalled      = $true
        ReceivedInput       = $null -ne $LambdaInput
        ReceivedContext     = $null -ne $LambdaContext
        InputTestKey        = $LambdaInput.testKey
        ContextFunctionName = if ($null -ne $LambdaContext) { $LambdaContext.FunctionName } else { 'null' }
    }
    [pscustomobject]$result
}
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn `
                -EnvironmentVariables @{ AWS_POWERSHELL_FUNCTION_HANDLER = 'Invoke-LambdaHandler' }
            $r = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'

            $r.FunctionCalled | Should -BeTrue
            $r.ReceivedInput | Should -BeTrue
            $r.ReceivedContext | Should -BeTrue
            $r.InputTestKey | Should -Be 'hello-lambda'
            $r.ContextFunctionName | Should -Be $fnName
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Dynamic Script Block (LoadScript override)' {
    It 'executes with empty automatic variables and working injected variables' {
        $fnName = "ps-test-dyn-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $zip = New-DynamicScriptPackage -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn `
                -HandlerOverride 'DynamicScriptTest::DynamicScriptTest.Bootstrap::ExecuteFunction'
            $r = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'

            $r.DynamicMode | Should -BeTrue
            $r.PSScriptRoot | Should -BeNullOrEmpty
            $r.PSCommandPath | Should -BeNullOrEmpty
            $r.MyInvocation_CommandType | Should -Be 'Script'
            $r.LambdaInput_Exists | Should -BeTrue
            $r.LambdaInput_TestKey | Should -Be 'hello-lambda'
            $r.LambdaContext_Exists | Should -BeTrue
            $r.LambdaInputString_Exists | Should -BeTrue
            $r.TEMP | Should -Be '/tmp'
            $r.HOME | Should -Be '/tmp/home'
            $r.LAMBDA_TASK_ROOT | Should -Be '/var/task'
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'LoadScript Override With File On Disk (Issue A regression)' {
    It 'runs the override (not the file) when LoadScript is overridden and a file exists' {
        $fnName = "ps-test-ovr-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $zip = New-LoadScriptOverrideWithFilePackage -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName

            $r.Source | Should -Be 'FromOverride'
            $r.PSScriptRoot | Should -BeNullOrEmpty
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'ForEach-Object -Parallel' {
    It 'propagates $PSScriptRoot and $LambdaInput via $using:' {
        $fnName = "ps-test-par-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
$mainPSScriptRoot = $PSScriptRoot
$mainLambdaInput = $LambdaInput

$parallelResults = 1..3 | ForEach-Object -Parallel {
    [pscustomobject]@{
        Using_PSScriptRoot  = $using:mainPSScriptRoot
        LambdaInput_Exists  = $null -ne $using:mainLambdaInput
        LambdaInput_TestKey = $using:mainLambdaInput.testKey
    }
}

$result = [ordered]@{
    MainPSScriptRoot            = $mainPSScriptRoot
    ParallelCount               = $parallelResults.Count
    Parallel_Using_PSScriptRoot = ($parallelResults | ForEach-Object { $_.Using_PSScriptRoot }) -join ','
    Parallel_LambdaInput        = ($parallelResults | ForEach-Object { $_.LambdaInput_Exists }) -join ','
    Parallel_TestKey            = ($parallelResults | ForEach-Object { $_.LambdaInput_TestKey }) -join ','
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'

            $r.ParallelCount | Should -Be 3
            $r.MainPSScriptRoot | Should -Be '/var/task'
            ($r.Parallel_Using_PSScriptRoot -split ',')[0] | Should -Be '/var/task'
            ($r.Parallel_LambdaInput -split ',')[0] | Should -Be 'True'
            ($r.Parallel_TestKey -split ',')[0] | Should -Be 'hello-lambda'
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Warm Invocation' {
    It 'maintains consistent behavior across two calls on same container' {
        $fnName = "ps-test-warm-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
$result = [ordered]@{
    PSScriptRoot               = $PSScriptRoot
    PSCommandPath              = $PSCommandPath
    LambdaInput_Exists         = $null -ne $LambdaInput
    LambdaInput_TestKey        = $LambdaInput.testKey
    LambdaContext_FunctionName = if ($null -ne $LambdaContext) { $LambdaContext.FunctionName } else { 'null' }
    TEMP                       = $env:TEMP
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn

            $r1 = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'
            Start-Sleep -Seconds 2
            $r2 = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'

            $r1.LambdaInput_Exists | Should -BeTrue
            $r2.LambdaInput_Exists | Should -BeTrue
            $r2.PSScriptRoot | Should -Be $r1.PSScriptRoot
            $r2.PSCommandPath | Should -Be $r1.PSCommandPath
            $r2.LambdaInput_TestKey | Should -Be 'hello-lambda'
            $r2.LambdaContext_FunctionName | Should -Be $fnName
            $r2.TEMP | Should -Be '/tmp'
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'Module Resolution via $PSScriptRoot' {
    It 'uses $PSScriptRoot for path resolution without fallback' {
        $fnName = "ps-test-mod-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
$basePath = if ($PSScriptRoot) { $PSScriptRoot } else { $env:LAMBDA_TASK_ROOT }

$result = [ordered]@{
    BasePath         = $basePath
    PSScriptRoot     = $PSScriptRoot
    UsedPSScriptRoot = [bool]$PSScriptRoot
    UsedFallback     = -not [bool]$PSScriptRoot
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName

            $r.BasePath | Should -Be '/var/task'
            $r.PSScriptRoot | Should -Be '/var/task'
            $r.UsedPSScriptRoot | Should -BeTrue
            $r.UsedFallback | Should -BeFalse
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Describe 'User Param() Block' {
    It 'allows scripts with their own Param() without conflict' {
        $fnName = "ps-test-prm-$script:RunId"
        $stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) $fnName
        $null = New-Item -ItemType Directory -Path $stagingDir -Force

        try {
            $handler = @'
Param(
    [string]$CustomParam = 'default-value'
)

$result = [ordered]@{
    CustomParam_Value        = $CustomParam
    LambdaInput_Exists       = $null -ne $LambdaInput
    LambdaInput_TestKey      = $LambdaInput.testKey
    LambdaContext_Exists     = $null -ne $LambdaContext
    LambdaInputString_Exists = $null -ne $LambdaInputString
    PSScriptRoot             = $PSScriptRoot
}
[pscustomobject]$result
'@
            $zip = New-LambdaPackage -HandlerScript $handler -StagingDir $stagingDir
            Deploy-TestFunction -FunctionName $fnName -ZipPath $zip -RoleArn $script:RoleArn
            $r = Invoke-TestFunction -FunctionName $fnName -Payload '{"testKey":"hello-lambda"}'

            $r.CustomParam_Value | Should -Be 'default-value'
            $r.LambdaInput_Exists | Should -BeTrue
            $r.LambdaInput_TestKey | Should -Be 'hello-lambda'
            $r.LambdaContext_Exists | Should -BeTrue
            $r.LambdaInputString_Exists | Should -BeTrue
            $r.PSScriptRoot | Should -Be '/var/task'
        } finally {
            Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
