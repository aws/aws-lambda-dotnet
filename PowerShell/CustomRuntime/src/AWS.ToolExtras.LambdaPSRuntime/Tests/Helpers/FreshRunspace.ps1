# Runs a script in a NEW runspace and returns its output, error stream, and a call log.
#
# The failure mode under test cannot be observed from inside an It block. When an advanced function
# calls a helper that raises with $PSCmdlet.ThrowTerminatingError, only the calling statement ends
# and the caller runs its next statement -- unless a try block is active anywhere on the call stack,
# in which case the error unwinds to it. Pester runs every It inside a try, so in-process the bug
# never shows. A fresh runspace has no enclosing try, which is how a user's console runs a cmdlet.
#
# The runspace is created in a child pwsh: a runspace in this process shares its AppDomain, where
# importing the module again can collide with the AWS SDK assemblies already loaded. What comes back
# is deserialized (Errors keep FullyQualifiedErrorId), and a script-terminating error is rethrown
# here with its message.
#
# $Stub runs inside the module's scope, so the functions it defines shadow the module's seams and
# the AWS.Tools cmdlets (a function is resolved before a cmdlet of the same name). Stubs record
# what ran by adding to the module-scope list $script:LambdaPSCalls. Before the stub runs, Microsoft's
# package repository is stubbed to list only the pinned releases and the runtime to have no versions,
# so no child reaches the network; a stub can redefine either.
function Invoke-InFreshRunspace {
    param(
        [Parameter()] [string]$Stub = '',
        [Parameter(Mandatory)] [string]$Command
    )
    $id = [guid]::NewGuid().ToString('n')
    $scriptPath = [System.IO.Path]::Combine($TestDrive, "fresh-$id.ps1")
    $runnerPath = [System.IO.Path]::Combine($TestDrive, "fresh-$id-runner.ps1")
    $resultPath = [System.IO.Path]::Combine($TestDrive, "fresh-$id.xml")
    # The test fakes (LambdaPSTests.Fakes.*) that stubs throw are defined in the child as well.
    Set-Content -LiteralPath $scriptPath -Value @"
. '$([System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent([System.IO.Path]::Combine($PSScriptRoot, 'FakeTypes.ps1')))'
Import-Module '$([System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($script:modulePath))' -Force
& (Get-Module AWS.ToolExtras.LambdaPSRuntime) {
    `$script:LambdaPSCalls = [System.Collections.Generic.List[string]]::new()
    function script:Get-LambdaPSPwshRepositoryRelease { param(`$PowerShellVersion) `$script:Config.pwsh.DefaultPatch[`$PowerShellVersion] }
    function script:Get-LMCRuntimeVersionList { }
    $Stub
}
$Command
"@
    Set-Content -LiteralPath $runnerPath -Value @'
param([string]$ScriptPath, [string]$ResultPath)
$ps = [powershell]::Create()
$result = try {
    $output = @($ps.AddScript((Get-Content -LiteralPath $ScriptPath -Raw)).Invoke())
    $errors = @($ps.Streams.Error)
    $ps.Commands.Clear()
    $calls = @($ps.AddScript('& (Get-Module AWS.ToolExtras.LambdaPSRuntime) { $script:LambdaPSCalls }').Invoke())
    [PSCustomObject]@{ Output = $output; Errors = $errors; Calls = $calls; Terminating = $null }
}
catch {
    [PSCustomObject]@{ Output = @(); Errors = @(); Calls = @(); Terminating = $_.Exception.Message }
}
finally {
    $ps.Dispose()
}
$result | Export-Clixml -LiteralPath $ResultPath -Depth 4
'@
    $console = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -File $runnerPath -ScriptPath $scriptPath -ResultPath $resultPath 2>&1 | Out-String
    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "The fresh runspace's process returned no result. Its output:`n$console"
    }
    $result = Import-Clixml -LiteralPath $resultPath
    if ($result.Terminating) { throw $result.Terminating }
    [PSCustomObject]@{
        Output = @($result.Output)
        Errors = @($result.Errors)
        Calls  = @($result.Calls)
    }
}

# Runs the same kind of script in a child pwsh whose console reads -Answer for each confirmation
# prompt, so a test can decline a stage under -Confirm (a runspace has no host to prompt with).
# Returns the call log and the console output, which holds the prompts, warnings and errors. Output
# has LF line endings on every platform, so a caller can split it on `n.
function Invoke-InChildPwsh {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it runs a child process.')]
    param(
        [Parameter()] [string]$Stub = '',
        [Parameter(Mandatory)] [string]$Command,
        [Parameter()] [string[]]$Answer = @()
    )
    $scriptPath = [System.IO.Path]::Combine($TestDrive, "child-$([guid]::NewGuid().ToString('n')).ps1")
    Set-Content -LiteralPath $scriptPath -Value @"
Import-Module '$([System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($script:modulePath))' -Force
& (Get-Module AWS.ToolExtras.LambdaPSRuntime) {
    `$script:LambdaPSCalls = [System.Collections.Generic.List[string]]::new()
    function script:Get-LambdaPSPwshRepositoryRelease { param(`$PowerShellVersion) `$script:Config.pwsh.DefaultPatch[`$PowerShellVersion] }
    function script:Get-LMCRuntimeVersionList { }
    $Stub
}
try { $Command } catch { "ERROR: `$(`$_.FullyQualifiedErrorId)" }
'CALLS:' + ((& (Get-Module AWS.ToolExtras.LambdaPSRuntime) { `$script:LambdaPSCalls }) -join ',')
"@
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    foreach ($argument in '-NoProfile', '-File', $scriptPath) { $startInfo.ArgumentList.Add($argument) }
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stderr = $process.StandardError.ReadToEndAsync()
    foreach ($line in $Answer) { $process.StandardInput.WriteLine($line) }
    $process.StandardInput.Close()
    $stdout = $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()
    $output = ($stdout + $stderr.Result) -replace "`r`n", "`n"
    $callLine = @($stdout -split "`r?`n" | Where-Object { $_ -like 'CALLS:*' }) | Select-Object -Last 1
    # The script prints CALLS: last, so a child that ends before it (a failed import, a crash, exit
    # in the command) is reported with what it printed.
    if ($null -eq $callLine) {
        throw "The child pwsh exited with code $($process.ExitCode) before it reported its calls. Its output:`n$output"
    }
    [PSCustomObject]@{
        Output = $output
        Calls  = @($callLine.Substring(6).Trim() -split ',' | Where-Object { $_ })
    }
}
