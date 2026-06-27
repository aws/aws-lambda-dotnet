# Helper functions for Lambda E2E tests.
# Dot-source this file in Pester BeforeAll blocks.

function Invoke-AwsCli {
    param([string[]]$Arguments)
    $allArgs = $Arguments + @('--profile', $script:ProfileName, '--region', $script:Region, '--output', 'json')
    $result = & aws @allArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "AWS CLI failed: $result"
    }
    if ($result) { $result | ConvertFrom-Json }
}

function New-TestRole {
    $trustPolicyFile = Join-Path ([System.IO.Path]::GetTempPath()) "trust-policy-$script:RunId.json"
    Set-Content -Path $trustPolicyFile -Value $script:TrustPolicy

    try {
        $null = Invoke-AwsCli @('iam', 'create-role',
            '--role-name', $script:RoleName,
            '--assume-role-policy-document', "file://$trustPolicyFile")
    } catch {
        if ($_ -match 'EntityAlreadyExists') { }
        else { throw }
    }

    try {
        $null = Invoke-AwsCli @('iam', 'attach-role-policy',
            '--role-name', $script:RoleName,
            '--policy-arn', $script:PolicyArn)
    } catch {
        if ($_ -match 'already been attached') { }
        else { throw }
    }

    Remove-Item $trustPolicyFile -ErrorAction SilentlyContinue

    $roleInfo = Invoke-AwsCli @('iam', 'get-role', '--role-name', $script:RoleName)
    return $roleInfo.Role.Arn
}

function Remove-TestResources {
    foreach ($fn in $script:DeployedFunctions) {
        try { $null = Invoke-AwsCli @('lambda', 'delete-function', '--function-name', $fn) } catch {}
        try { $null = Invoke-AwsCli @('logs', 'delete-log-group', '--log-group-name', "/aws/lambda/$fn") } catch {}
    }

    try { $null = Invoke-AwsCli @('iam', 'detach-role-policy', '--role-name', $script:RoleName, '--policy-arn', $script:PolicyArn) } catch {}
    try { $null = Invoke-AwsCli @('iam', 'delete-role', '--role-name', $script:RoleName) } catch {}
}

function New-LambdaPackage {
    param(
        [string]$HandlerScript,
        [string]$StagingDir
    )

    $projectDir = Join-Path $StagingDir 'project'
    $null = New-Item -ItemType Directory -Path $projectDir -Force

    Set-Content -Path (Join-Path $projectDir 'handler.ps1') -Value $HandlerScript

    $bootstrapCs = @'
using Amazon.Lambda.PowerShellHost;

namespace handler
{
    public class Bootstrap : PowerShellFunctionHost
    {
        public Bootstrap() : base("handler.ps1")
        {
        }
    }
}
'@
    Set-Content -Path (Join-Path $projectDir 'Bootstrap.cs') -Value $bootstrapCs

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    </PropertyGroup>
    <ItemGroup>
        <Content Include="handler.ps1">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </Content>
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.PowerShell.SDK" Version="7.6.0" />
        <ProjectReference Include="$script:RepoRoot/Libraries/src/Amazon.Lambda.Core/Amazon.Lambda.Core.csproj" />
        <ProjectReference Include="$script:RepoRoot/Libraries/src/Amazon.Lambda.PowerShellHost/Amazon.Lambda.PowerShellHost.csproj" />
    </ItemGroup>
</Project>
"@
    Set-Content -Path (Join-Path $projectDir 'handler.csproj') -Value $csproj

    $publishDir = Join-Path $StagingDir 'publish'
    $publishOutput = & dotnet publish $projectDir -c Release -o $publishDir --framework net10.0 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed: $publishOutput"
    }

    $outputZip = Join-Path $StagingDir 'package.zip'
    Compress-Archive -Path "$publishDir/*" -DestinationPath $outputZip -Force
    return $outputZip
}

function New-DynamicScriptPackage {
    param([string]$StagingDir)

    $projectDir = Join-Path $StagingDir 'project'
    $null = New-Item -ItemType Directory -Path $projectDir -Force

    $bootstrapCs = @'
using Amazon.Lambda.Core;
using Amazon.Lambda.PowerShellHost;

namespace DynamicScriptTest
{
    public class Bootstrap : PowerShellFunctionHost
    {
        public Bootstrap() : base()
        {
        }

        protected override string LoadScript(string input, ILambdaContext context)
        {
            return @"
$result = [ordered]@{
    PSScriptRoot              = $PSScriptRoot
    PSCommandPath             = $PSCommandPath
    MyInvocation_Path         = $MyInvocation.MyCommand.Path
    MyInvocation_CommandType  = [string]$MyInvocation.MyCommand.CommandType
    LambdaInput_Exists        = $null -ne $LambdaInput
    LambdaInput_TestKey       = $LambdaInput.testKey
    LambdaContext_Exists      = $null -ne $LambdaContext
    LambdaInputString_Exists  = $null -ne $LambdaInputString
    TEMP                      = $env:TEMP
    HOME                      = $env:HOME
    LAMBDA_TASK_ROOT          = $env:LAMBDA_TASK_ROOT
    DynamicMode               = $true
}

[pscustomobject]$result
";
        }
    }
}
'@

    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.PowerShell.SDK" Version="7.6.0" />
        <ProjectReference Include="$script:RepoRoot/Libraries/src/Amazon.Lambda.Core/Amazon.Lambda.Core.csproj" />
        <ProjectReference Include="$script:RepoRoot/Libraries/src/Amazon.Lambda.PowerShellHost/Amazon.Lambda.PowerShellHost.csproj" />
    </ItemGroup>
</Project>
"@

    Set-Content -Path (Join-Path $projectDir 'DynamicScriptTest.csproj') -Value $csproj
    Set-Content -Path (Join-Path $projectDir 'Bootstrap.cs') -Value $bootstrapCs

    $publishDir = Join-Path $StagingDir 'publish'
    $publishOutput = & dotnet publish $projectDir -c Release -o $publishDir --framework net10.0 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed: $publishOutput"
    }

    $outputZip = Join-Path $StagingDir 'package.zip'
    Compress-Archive -Path "$publishDir/*" -DestinationPath $outputZip -Force
    return $outputZip
}

function New-LoadScriptOverrideWithFilePackage {
    # Issue A regression test: subclass overrides LoadScript() AND a file exists on disk.
    # Pre-PR: override always ran. Post-PR without mitigation: file path silently ignores
    # the override. Post-PR with mitigation: override runs (text from override is executed).
    param([string]$StagingDir)

    $projectDir = Join-Path $StagingDir 'project'
    $null = New-Item -ItemType Directory -Path $projectDir -Force

    # The on-disk file says "FromFile" and would expose $PSScriptRoot if it ran. The override
    # returns "FromOverride" with empty $PSScriptRoot. The result tells us which one ran.
    $originalScript = @'
[pscustomobject]@{ Source = 'FromFile'; PSScriptRoot = $PSScriptRoot }
'@
    Set-Content -Path (Join-Path $projectDir 'handler.ps1') -Value $originalScript

    $bootstrapCs = @'
using Amazon.Lambda.Core;
using Amazon.Lambda.PowerShellHost;

namespace handler
{
    public class Bootstrap : PowerShellFunctionHost
    {
        public Bootstrap() : base("handler.ps1")
        {
        }

        protected override string LoadScript(string input, ILambdaContext context)
        {
            return @"[pscustomobject]@{ Source = 'FromOverride'; PSScriptRoot = $PSScriptRoot }";
        }
    }
}
'@
    Set-Content -Path (Join-Path $projectDir 'Bootstrap.cs') -Value $bootstrapCs

    $csproj = @"
<Project Sdk=`"Microsoft.NET.Sdk`">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    </PropertyGroup>
    <ItemGroup>
        <Content Include=`"handler.ps1`"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include=`"Microsoft.PowerShell.SDK`" Version=`"7.6.0`" />
        <ProjectReference Include=`"$script:RepoRoot/Libraries/src/Amazon.Lambda.Core/Amazon.Lambda.Core.csproj`" />
        <ProjectReference Include=`"$script:RepoRoot/Libraries/src/Amazon.Lambda.PowerShellHost/Amazon.Lambda.PowerShellHost.csproj`" />
    </ItemGroup>
</Project>
"@
    Set-Content -Path (Join-Path $projectDir 'handler.csproj') -Value $csproj

    $publishDir = Join-Path $StagingDir 'publish'
    $publishOutput = & dotnet publish $projectDir -c Release -o $publishDir --framework net10.0 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed: $publishOutput"
    }

    $outputZip = Join-Path $StagingDir 'package.zip'
    Compress-Archive -Path "$publishDir/*" -DestinationPath $outputZip -Force
    return $outputZip
}

function Deploy-TestFunction {
    param(
        [string]$FunctionName,
        [string]$ZipPath,
        [string]$RoleArn,
        [string]$HandlerOverride,
        [hashtable]$EnvironmentVariables
    )

    try {
        $null = Invoke-AwsCli @('lambda', 'delete-function', '--function-name', $FunctionName)
    } catch {
        if ($_ -notmatch 'ResourceNotFoundException') { throw }
    }

    $handler = if ($HandlerOverride) { $HandlerOverride } else { 'handler::handler.Bootstrap::ExecuteFunction' }

    $createArgs = @('lambda', 'create-function',
        '--function-name', $FunctionName,
        '--runtime', $script:Runtime,
        '--role', $RoleArn,
        '--handler', $handler,
        '--zip-file', "fileb://$ZipPath",
        '--timeout', '120',
        '--memory-size', '512')

    if ($EnvironmentVariables -and $EnvironmentVariables.Count -gt 0) {
        $envJson = ($EnvironmentVariables | ConvertTo-Json -Compress)
        $createArgs += @('--environment', "{`"Variables`":$envJson}")
    }

    $maxRetries = 3
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            $null = Invoke-AwsCli $createArgs
            break
        } catch {
            if ($_ -match 'AccessDeniedException|cannot be assumed' -and $attempt -lt $maxRetries) {
                Start-Sleep -Seconds 10
            } else {
                throw
            }
        }
    }

    $null = & aws lambda wait function-active-v2 --function-name $FunctionName --profile $script:ProfileName --region $script:Region 2>&1
    if ($LASTEXITCODE -ne 0) {
        $null = & aws lambda wait function-active --function-name $FunctionName --profile $script:ProfileName --region $script:Region 2>&1
    }

    $script:DeployedFunctions.Add($FunctionName)
}

function Invoke-TestFunction {
    param(
        [string]$FunctionName,
        [string]$Payload = '{}'
    )

    $outputFile = Join-Path ([System.IO.Path]::GetTempPath()) "$FunctionName-$(Get-Random).json"
    $payloadFile = Join-Path ([System.IO.Path]::GetTempPath()) "$FunctionName-payload-$(Get-Random).json"
    Set-Content -Path $payloadFile -Value $Payload

    $invokeResult = & aws lambda invoke `
        --function-name $FunctionName `
        --profile $script:ProfileName `
        --region $script:Region `
        --cli-binary-format raw-in-base64-out `
        --payload "file://$payloadFile" `
        --log-type Tail `
        --output json `
        $outputFile 2>&1

    Remove-Item $payloadFile -ErrorAction SilentlyContinue

    if ($LASTEXITCODE -ne 0) {
        throw "Lambda invoke failed: $invokeResult"
    }

    $meta = $invokeResult | ConvertFrom-Json
    $response = Get-Content $outputFile -Raw | ConvertFrom-Json
    Remove-Item $outputFile -ErrorAction SilentlyContinue

    if ($meta.FunctionError) {
        throw "Lambda function error: $($response | ConvertTo-Json -Compress -Depth 3)"
    }

    return $response
}
