# Runs Templates/Shared/runtime.ps1 for real, in a child pwsh, against a fake Lambda Runtime API on
# localhost, and returns what the runtime posted back. Dot-source it from a test file's BeforeAll.

function Invoke-LambdaPSRuntimeLoop {
    <#
    .SYNOPSIS
        Serves the events to a child runtime.ps1 and returns one object per post it made, in order.
    .PARAMETER TaskRoot
        The folder holding the handler script (LAMBDA_TASK_ROOT).
    .PARAMETER Handler
        The _HANDLER string, for example 'handler.ps1' or 'handler.ps1::Invoke'.
    .PARAMETER EventBody
        The event bodies (JSON strings) to serve, in order.
    .PARAMETER LogVariable
        The name of a variable in the caller's scope to receive the runtime's console output (stdout and
        stderr), for tests of what the function log shows.
    .PARAMETER TimeoutSeconds
        How long to wait for the runtime to finish all events before failing (default 90), counted from
        when the first event is served, so pwsh start-up is not part of it.
    .PARAMETER StartupTimeoutSeconds
        How long to wait for the runtime to ask for its first event, or to exit, before failing
        (default 120).
    .PARAMETER ExitCodeVariable
        The name of a variable in the caller's scope to receive the runtime's exit code, or $null when
        the runtime was still running and this function stopped it.
    .PARAMETER RuntimePath
        The script to run as the runtime (default Templates/Shared/runtime.ps1).
    .PARAMETER ResponseStatus
        The status to answer each /response post with, in order (default 202 for each). A post beyond
        the list, and every other post, is answered 202. An event counts as done once its /response or
        /error post is answered 2xx, so a rejected /response leaves the event open for the runtime's
        /error post (an /init/error post closes no event).
    .PARAMETER NextStatus
        The status to answer /next with once every event has been served. Implies -ExpectExit. Without
        it, /next is left unanswered: the runtime is stopped once every event is done, or, with
        -ExpectExit, the run fails at the timeout.
    .PARAMETER ExpectExit
        The runtime is expected to exit by itself, for example after an /init/error post, or after a
        /next answered with -NextStatus: the function waits for that and returns the posts made, even
        with events never served, and fails if the runtime does not exit before the timeout.
    .PARAMETER TraceId
        The Lambda-Runtime-Trace-Id header to send with each event, in order. An empty or missing
        entry sends no header.
    .PARAMETER ClientContext
        The Lambda-Runtime-Client-Context header (JSON text) to send with each event, in order. An empty
        or missing entry sends no header.
    .PARAMETER CognitoIdentity
        The Lambda-Runtime-Cognito-Identity header (JSON text) to send with each event, in order. An
        empty or missing entry sends no header.
    .PARAMETER TenantId
        The Lambda-Runtime-Aws-Tenant-Id header to send with each event, in order, as for a function
        with tenant isolation. An empty or missing entry sends no header.
    .PARAMETER InvocationId
        The Lambda-Runtime-Invocation-Id header to send with each event, in order. An empty or missing
        entry sends no header.
    .OUTPUTS
        One object per post, in order: Path (the path posted to: an invocation's /response or /error,
        or /init/error), Body, Status (the status it was answered with) and InvocationId (the post's
        Lambda-Runtime-Invocation-Id header).
    .NOTES
        If the runtime exits while an event is still open (without -ExpectExit or -NextStatus), this throws
        with the runtime's exit code and stderr instead of waiting out the timeout.
        Without either, events still open at the timeout also throw, naming how many.
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '', Justification = 'Test helper: it starts and stops a local process and listener.')]
    param(
        [Parameter(Mandatory)] [string]$TaskRoot,
        [Parameter(Mandatory)] [string]$Handler,
        [Parameter(Mandatory)] [string[]]$EventBody,
        [int]$TimeoutSeconds = 90,
        [string]$LogVariable,
        [int[]]$ResponseStatus = @(),
        [int]$NextStatus,
        [string[]]$TraceId = @(),
        [string[]]$ClientContext = @(),
        [string[]]$CognitoIdentity = @(),
        [string[]]$TenantId = @(),
        [string[]]$InvocationId = @(),
        [switch]$ExpectExit,
        [int]$StartupTimeoutSeconds = 120,
        [string]$ExitCodeVariable,
        [string]$RuntimePath = [System.IO.Path]::Combine((Split-Path -Parent (Split-Path -Parent $PSScriptRoot)), 'Templates', 'Shared', 'runtime.ps1')
    )
    $expectExit = $ExpectExit.IsPresent -or $PSBoundParameters.ContainsKey('NextStatus')

    # HttpListener cannot bind port 0, so a free port is found first. Another process can take it
    # before the listener binds (tests running in parallel), so a failed bind tries a new port.
    for ($attempt = 1; ; $attempt++) {
        $probe = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $probe.Start(); $port = $probe.LocalEndpoint.Port; $probe.Stop()
        $listener = [System.Net.HttpListener]::new()
        $listener.Prefixes.Add("http://127.0.0.1:$port/")
        try { $listener.Start(); break }
        catch [System.Net.HttpListenerException] {
            $listener.Close()
            if ($attempt -ge 5) { throw }
        }
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    foreach ($argument in '-NoProfile', '-NonInteractive', '-File', $RuntimePath) { $startInfo.ArgumentList.Add($argument) }
    $startInfo.Environment['AWS_LAMBDA_RUNTIME_API'] = "127.0.0.1:$port"
    $startInfo.Environment['LAMBDA_TASK_ROOT'] = $TaskRoot
    $startInfo.Environment['_HANDLER'] = $Handler
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()

    $posts = [System.Collections.Generic.List[object]]::new()
    $pending = [System.Collections.Generic.Queue[string]]::new([string[]]$EventBody)
    $served = 0
    $done = 0
    $responses = 0
    $exited = $false
    $stopped = $false
    $timeoutMessage = $null
    # Until the first event is served, the runtime is starting: that has its own limit.
    $deadline = [datetime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    try {
        $contextTask = $listener.GetContextAsync()
        while ([datetime]::UtcNow -lt $deadline -and ($expectExit -or $done -lt $EventBody.Count)) {
            if (-not $contextTask.Wait(500)) {
                if ($process.HasExited) {
                    if ($expectExit) { $exited = $true; break }
                    $process.WaitForExit()
                    throw "The runtime exited with code $($process.ExitCode) with $($EventBody.Count - $done) event(s) still open. Its stderr:`n$($stderr.Result)"
                }
                continue
            }
            $context = $contextTask.Result
            $contextTask = $listener.GetContextAsync()
            $path = $context.Request.Url.AbsolutePath
            if ($context.Request.HttpMethod -eq 'GET' -and $path.EndsWith('/invocation/next') -and $pending.Count -gt 0) {
                if ($served -eq 0) { $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds) }
                $served++
                $context.Response.Headers['Lambda-Runtime-Aws-Request-Id'] = "req-$served"
                $context.Response.Headers['Lambda-Runtime-Deadline-Ms'] = [string]([DateTimeOffset]::UtcNow.AddMinutes(5).ToUnixTimeMilliseconds())
                $context.Response.Headers['Lambda-Runtime-Invoked-Function-Arn'] = 'arn:aws:lambda:us-east-1:111122223333:function:fn'
                if ($served -le $TraceId.Count -and $TraceId[$served - 1]) {
                    $context.Response.Headers['Lambda-Runtime-Trace-Id'] = $TraceId[$served - 1]
                }
                if ($served -le $ClientContext.Count -and $ClientContext[$served - 1]) {
                    $context.Response.Headers['Lambda-Runtime-Client-Context'] = $ClientContext[$served - 1]
                }
                if ($served -le $CognitoIdentity.Count -and $CognitoIdentity[$served - 1]) {
                    $context.Response.Headers['Lambda-Runtime-Cognito-Identity'] = $CognitoIdentity[$served - 1]
                }
                if ($served -le $TenantId.Count -and $TenantId[$served - 1]) {
                    $context.Response.Headers['Lambda-Runtime-Aws-Tenant-Id'] = $TenantId[$served - 1]
                }
                if ($served -le $InvocationId.Count -and $InvocationId[$served - 1]) {
                    $context.Response.Headers['Lambda-Runtime-Invocation-Id'] = $InvocationId[$served - 1]
                }
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($pending.Dequeue())
                $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                $context.Response.Close()
            }
            elseif ($context.Request.HttpMethod -eq 'GET' -and $path.EndsWith('/invocation/next') -and $PSBoundParameters.ContainsKey('NextStatus')) {
                $context.Response.StatusCode = $NextStatus
                $context.Response.Close()
            }
            elseif ($context.Request.HttpMethod -eq 'POST') {
                $reader = [System.IO.StreamReader]::new($context.Request.InputStream)
                $status = 202
                if ($path.EndsWith('/response')) {
                    if ($responses -lt $ResponseStatus.Count) { $status = $ResponseStatus[$responses] }
                    $responses++
                }
                $posts.Add([PSCustomObject]@{ Path = $path; Body = $reader.ReadToEnd(); Status = $status; InvocationId = $context.Request.Headers['Lambda-Runtime-Invocation-Id'] })
                if ($path -match '/invocation/[^/]+/(response|error)$' -and $status -ge 200 -and $status -lt 300) { $done++ }
                $context.Response.StatusCode = $status
                $context.Response.Close()
            }
            # Otherwise (a /next after the last event, without -NextStatus) the request is left
            # unanswered; the process is stopped below.
        }
        # The output is complete only once the runtime has stopped, so these throw after the finally.
        if ($served -eq 0 -and -not $exited -and ($expectExit -or $EventBody.Count -gt 0)) {
            $timeoutMessage = "The runtime neither asked for its first event nor exited within $StartupTimeoutSeconds seconds."
        }
        elseif ($expectExit -and -not $exited) {
            $timeoutMessage = "The runtime was expected to exit, but was still running after $TimeoutSeconds seconds."
        }
        elseif (-not $expectExit -and $done -lt $EventBody.Count) {
            $timeoutMessage = "The runtime still had $($EventBody.Count - $done) of $($EventBody.Count) event(s) open after $TimeoutSeconds seconds."
        }
    }
    finally {
        if (-not $process.HasExited) { $stopped = $true; $process.Kill($true) }
        $process.WaitForExit()
        $listener.Close()
        if ($LogVariable) { Set-Variable -Name $LogVariable -Value ($stdout.Result + $stderr.Result) -Scope 1 }
        if ($ExitCodeVariable) { Set-Variable -Name $ExitCodeVariable -Value $(if ($stopped) { $null } else { $process.ExitCode }) -Scope 1 }
    }
    if ($timeoutMessage) {
        $tail = @(($stdout.Result + $stderr.Result) -split '\r?\n' | Select-Object -Last 40) -join "`n"
        throw "$timeoutMessage The last lines of its stdout and stderr:`n$tail"
    }
    $posts
}
