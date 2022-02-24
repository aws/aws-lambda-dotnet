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
    New-Variable -Name AwsPowerShellDefaultSdkVersion -Value '7.2.1' -Option Constant
}

if (!($AwsPowerShellTargetFramework))
{
    New-Variable -Name AwsPowerShellTargetFramework -Value 'net6.0' -Option Constant
}

if (!($AwsPowerShellLambdaRuntime))
{
    New-Variable -Name AwsPowerShellLambdaRuntime -Value 'dotnet6' -Option Constant
}