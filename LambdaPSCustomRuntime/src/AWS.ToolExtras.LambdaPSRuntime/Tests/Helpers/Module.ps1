# Imports the module under test. Dot-source it from a test file's BeforeAll, and call
# Remove-LambdaPSTestModule from its AfterAll.

$script:moduleName = 'AWS.ToolExtras.LambdaPSRuntime'
$script:moduleRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:modulePath = [System.IO.Path]::Combine($script:moduleRoot, "$script:moduleName.psd1")
Import-Module $script:modulePath -Force

function Remove-LambdaPSTestModule {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it only unloads the module under test.')]
    param()
    Remove-Module 'AWS.ToolExtras.LambdaPSRuntime' -Force -ErrorAction SilentlyContinue
}
