if (!($DefaultFunctionMemory))
{
    New-Variable -Name DefaultFunctionMemory -Value 512 -Option Constant
}

if (!($DefaultFunctionTimeout))
{
    New-Variable -Name DefaultFunctionTimeout -Value 90 -Option Constant
}

if (!($ProjectModuleDirectory))
{
    New-Variable -Name ProjectModuleDirectory -Value 'Modules' -Option Constant
}

if (!($AwsPowerShellFunctionEnvName))
{
    New-Variable -Name AwsPowerShellFunctionEnvName -Value 'AWS_POWERSHELL_FUNCTION_HANDLER' -Option Constant
}

if (!($AwsPowerShellDefaultSdkVersion))
{
    New-Variable -Name AwsPowerShellDefaultSdkVersion -Value '7.6.0' -Option Constant
}

if (!($AwsPowerShellTargetFramework))
{
    New-Variable -Name AwsPowerShellTargetFramework -Value 'net10.0' -Option Constant
}

if (!($AwsPowerShellLambdaRuntime))
{
    New-Variable -Name AwsPowerShellLambdaRuntime -Value 'dotnet10' -Option Constant
}

if (!($AwsModuleStripFilters))
{
    # File patterns inside AWS-authored PowerShell modules that have no purpose at
    # Lambda runtime (no interactive shell, no Get-Help, no debugger). Stripping
    # them reduces package size and INIT (cold-start) duration. LICENSE / NOTICE
    # files are intentionally retained.
    #
    # The '*.xml' pattern matches case-insensitively, covering:
    #   - <Module>.dll-Help.xml — PowerShell MAML help
    #   - <Module>.XML          — .NET XMLDoc compiler output (IntelliSense data)
    #   - PSGetModuleInfo.xml   — PowerShellGet install metadata
    # Format.ps1xml / Types.ps1xml are NOT matched because their extension is
    # .ps1xml (not .xml) and the wildcard requires a literal '.xml' suffix.
    New-Variable -Name AwsModuleStripFilters -Value @(
        '*.xml',
        '*.pdb'
    ) -Option Constant
}

if (!($AwsAuthoredModuleNamePatterns))
{
    # Only AWS-authored modules under Modules/ are stripped; third-party / community
    # modules are left untouched.
    New-Variable -Name AwsAuthoredModuleNamePatterns -Value @(
        'AWSPowerShell.NetCore',
        'AWS.Tools.*'
    ) -Option Constant
}