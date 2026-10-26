function Invoke-LambdaPSExternalTool {
    <#
    .SYNOPSIS
        Runs an external CLI executable and fails hard on a non-zero exit code.
    .DESCRIPTION
        Thin wrapper that invokes an external executable through the call operator with an argument
        array, then inspects $LASTEXITCODE. Every line the tool writes, to stdout or stderr, goes to
        the host as it arrives, so the function returns nothing. On a non-zero exit it raises a
        terminating error ('ExternalToolFailed', OperationStopped) whose message gives the exit code and
        the arguments, and whose exception carries the last 50 lines in .Data['Output'] (string[]), for
        a caller that explains the failure. Because the message repeats the arguments, never pass a
        secret as one.

        The name is resolved to an executable file only (Get-Command -CommandType Application), and
        that file is run: a function or alias of the same name in the session does not run instead.
        A name that resolves to no executable is a terminating CommandNotFoundException.

        It runs a standalone CLI executable (for example docker) as a child process, and loads no
        assembly.

        This function is intentionally a single seam over process invocation so callers can mock it
        in tests instead of running a real external process.
    .PARAMETER Executable
        The name or path of the external executable to run (for example 'docker').
    .PARAMETER ArgumentList
        The argument array passed to the executable.
    .EXAMPLE
        Invoke-LambdaPSExternalTool -Executable 'docker' -ArgumentList @('version')

        Runs 'docker version' and throws if the process exits non-zero.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Executable,

        [Parameter(Mandatory)]
        [ValidateNotNull()]
        [string[]]$ArgumentList
    )

    $application = Get-Command -Name $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
    # The exit code is checked below; with the user's preference on, pwsh would raise its own error first.
    $PSNativeCommandUseErrorActionPreference = $false
    # Each argument reaches the tool as one argument, quotes and spaces included, whatever the caller's setting.
    $PSNativeCommandArgumentPassing = 'Standard'
    $tail = [System.Collections.Generic.Queue[string]]::new()
    # One Out-Host for the whole stream; each line reaches the host as it arrives.
    & $application.Source @ArgumentList 2>&1 | ForEach-Object {
        # A stderr line arrives as an ErrorRecord; its text is the line.
        $line = [string]$_
        $tail.Enqueue($line)
        if ($tail.Count -gt 50) { $null = $tail.Dequeue() }
        $line
    } | Out-Host
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        # Use `throw` (not $PSCmdlet.ThrowTerminatingError): a terminating error raised via
        # $PSCmdlet inside this nested function terminates only this function, so the CALLER
        # (Build-LambdaPSRuntimeImage) would keep running and emit a false success. `throw`
        # propagates up the whole call stack and halts the caller.
        # Quoted as PowerShell would read them, so an argument with a space reads as one argument.
        $argumentText = @($ArgumentList | ForEach-Object {
                if ($_ -match '[\s''"]') { "'" + ($_ -replace "'", "''") + "'" } else { $_ }
            }) -join ' '
        $exception = [System.InvalidOperationException]::new(
            "External tool '$Executable' exited with code $exitCode. Arguments: $argumentText")
        $exception.Data['Output'] = [string[]]$tail.ToArray()
        $errorRecord = [System.Management.Automation.ErrorRecord]::new(
            $exception,
            'ExternalToolFailed', [System.Management.Automation.ErrorCategory]::OperationStopped, $Executable)
        throw $errorRecord
    }
}
