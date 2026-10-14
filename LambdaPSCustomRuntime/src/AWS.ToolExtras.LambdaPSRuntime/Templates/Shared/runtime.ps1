# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0
#
# The runtime: setup and the Runtime API event loop, for every target. Each target's bootstrap is an
# sh wrapper that only starts it, with exec pwsh -NoProfile -NonInteractive -File on
# /var/runtime/runtime.ps1 (CMR and Container), /opt/runtime.ps1 (Layer) or /var/task/runtime.ps1
# (SelfContainedZip); the Layer and SelfContainedZip wrappers first export
# DOTNET_SYSTEM_GLOBALIZATION_INVARIANT. pwsh -File runs this file in the global scope, which is
# where the handler script is dot-sourced below and where Invoke-FunctionHandler finds it.
#
# The runtime module and the LambdaContext assembly sit beside this file, so every path is built
# from $PSScriptRoot: /var/runtime, /opt or /var/task.
#
# The handler script is dot-sourced into this same scope, so the runtime's own variables carry a
# $__LambdaPSRuntime prefix that a handler will not use: a handler's $httpClient is its own.

# Function logs are plain text: without this, pwsh colours error and warning records with ANSI
# escape codes even when stderr is not a terminal.
$PSStyle.OutputRendering = 'PlainText'
if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Start: runtime.ps1'}
## Initiating custom runtime

# Set PowerShell version environment
$env:POWERSHELL_VERSION = ($PSVersionTable).PSVersion.ToString()
if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]Running PowerShell: $($env:POWERSHELL_VERSION)"}

# Importing runtime module helpers
if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Importing runtime module helpers'}
Import-Module ([System.IO.Path]::Combine($PSScriptRoot, 'modules', 'pwsh-runtime.psd1'))

# The Runtime API is on the sandbox's loopback. A function's HTTP_PROXY is for its own outbound calls:
# through it, events and responses would reach the proxy, or never arrive.
$private:__LambdaPSRuntimeHttpHandler = [System.Net.Http.SocketsHttpHandler]::new()
$private:__LambdaPSRuntimeHttpHandler.UseProxy = $false
$private:__LambdaPSRuntimeHttpClient = [System.Net.Http.HttpClient]::new($private:__LambdaPSRuntimeHttpHandler)
$private:__LambdaPSRuntimeHttpClient.Timeout = [System.Threading.Timeout]::InfiniteTimeSpan

# Whether an error carries only the runtime's own stack frames ('at <name>, <file>: line <n>', every one
# in runtime.ps1 or one of the runtime module's Private/*.ps1 files), which tell the customer nothing:
# such an error (a missing script, an invalid _HANDLER, a break or continue refused, ...) is reported
# without them on /init/error and /error. An error from the handler's own code keeps its stack. Frames
# are matched against the runtime's files, not its folder: in a self-contained zip that folder is also
# in the function's module path, where a customer module named Private would sit. The commands are
# module-qualified, so a handler's function of the same name is not called instead.
$private:__LambdaPSRuntimeIsOwnError = {
    param($ErrorRecord, [string[]]$RuntimeFiles)
    -not @(([string]$ErrorRecord.ScriptStackTrace -split '\r?\n') | Microsoft.PowerShell.Core\Where-Object {
            $_ -and ($_ -replace '^at .*?, (.*): line \d+$', '$1') -notin $RuntimeFiles
        }).Count
}
$private:__LambdaPSRuntimeFiles = @($PSCommandPath) + [System.IO.Directory]::GetFiles([System.IO.Path]::Combine($PSScriptRoot, 'modules', 'Private'), '*.ps1')

# Every later start-up step is in this try: a failure is reported to /runtime/init/error and ends
# the runtime, so Lambda names the cause instead of starting an event loop with no handler. Posting
# that report needs only the runtime module above and this client.
# A labelled break or continue that matches no loop goes past every sentinel and past catch; this
# finally then reports it on /init/error and exits 1, rather than letting the runtime end with exit 0.
$private:__LambdaPSRuntimeStartupReported = $false
try {
    try {
        # The LambdaContext type: load PowerShellLambdaContext.dll, which an image or package build
        # precompiles when the build host can run the bundled pwsh, so that cold start does not compile it.
        # If the DLL is absent (built on another OS or architecture) or does not load (built for another
        # .NET), compile the shipped PowerShellLambdaContext.cs instead: a slower cold start, but it works.
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Loading precompiled LambdaContext assembly' }
        try {
            Add-Type -Path ([System.IO.Path]::Combine($PSScriptRoot, 'PowerShellLambdaContext.dll')) -ErrorAction Stop
        }
        catch {
            $private:__LambdaPSRuntimeDllError = $_.Exception.Message
            if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]Precompiled LambdaContext unavailable ($private:__LambdaPSRuntimeDllError); compiling PowerShellLambdaContext.cs" }
            try {
                Add-Type -TypeDefinition ([System.IO.File]::ReadAllText([System.IO.Path]::Combine($PSScriptRoot, 'PowerShellLambdaContext.cs'))) -ErrorAction Stop
            }
            catch {
                throw [System.InvalidOperationException]::new(
                    ('The runtime could not load its LambdaContext type. PowerShellLambdaContext.dll: {0} PowerShellLambdaContext.cs: {1}' -f
                        $private:__LambdaPSRuntimeDllError, $_.Exception.GetBaseException().Message))
            }
        }

        # Modify $env:PSModulePath to support Lambda paths
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Modify PSModulePath to support Lambda paths'}
        pwsh-runtime\Set-PSModulePath

        # Parse _HANDLER environment variable
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Parse _HANDLER environment variable'}
        $private:__LambdaPSRuntimeHandler = pwsh-runtime\Get-Handler -Handler $env:_HANDLER

        # A Script handler is not run until the first event, so check here that its script exists and
        # parses. For the Function form this also gives a clearer message than the dot-source's own error.
        if ($private:__LambdaPSRuntimeHandler.scriptFilePath) {
            $private:__LambdaPSRuntimeScriptPath = $private:__LambdaPSRuntimeHandler.scriptFilePath
            if (-not [System.IO.File]::Exists($private:__LambdaPSRuntimeScriptPath)) {
                throw [System.IO.FileNotFoundException]::new("Handler script not found: $private:__LambdaPSRuntimeScriptPath", $private:__LambdaPSRuntimeScriptPath)
            }
            $private:__LambdaPSRuntimeParseErrors = $null
            $null = [System.Management.Automation.Language.Parser]::ParseFile($private:__LambdaPSRuntimeScriptPath, [ref]$null, [ref]$private:__LambdaPSRuntimeParseErrors)
            if ($private:__LambdaPSRuntimeParseErrors) {
                throw [System.Management.Automation.ParseException]::new($private:__LambdaPSRuntimeParseErrors)
            }
        }

        ## Initiating function
        # Run function/module pre-handler initialization in global scope
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Run function/module pre-handler initialization in global scope'}
        switch ($private:__LambdaPSRuntimeHandler.handlerType) {
            'Function' {
                if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]Dot sourcing PowerShell script: $($private:__LambdaPSRuntimeHandler.scriptFilePath)"}
                # A break or continue in the script's top-level code that is outside a loop of its own would
                # stop the dot-source silently, perhaps before the handler function is defined. This loop is
                # the nearest one, so the stop is seen here and reported.
                $private:__LambdaPSRuntimeScriptCompleted = $false
                do {
                    . $private:__LambdaPSRuntimeHandler.ScriptFilePath
                    $private:__LambdaPSRuntimeScriptCompleted = $true
                } while ($false)
                if (-not $private:__LambdaPSRuntimeScriptCompleted) {
                    throw [System.InvalidOperationException]::new(
                        "The handler script's top-level code used break or continue outside a loop of its own, so it stopped before it finished: $($private:__LambdaPSRuntimeHandler.scriptFilePath)")
                }
            }
            'Module' {
                if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]Importing module: $($private:__LambdaPSRuntimeHandler.moduleName)"}
                # The same for a break or continue in the module's top-level code, which would stop the import.
                $private:__LambdaPSRuntimeImportCompleted = $false
                do {
                    try {
                        Import-Module -Name $private:__LambdaPSRuntimeHandler.moduleName -Force -ErrorAction Stop
                    }
                    catch {
                        # A module that is not found is named with where the runtime looked; any other import
                        # error (such as one the module's own code raises) is reported as it is.
                        if ($_.FullyQualifiedErrorId -notlike 'Modules_ModuleNotFound*') { throw }
                        throw [System.IO.FileNotFoundException]::new(
                            "Handler module not found: $($private:__LambdaPSRuntimeHandler.moduleName) (_HANDLER $env:_HANDLER). The runtime looked in: $($env:PSModulePath -split [System.IO.Path]::PathSeparator -join ', ')")
                    }
                    $private:__LambdaPSRuntimeImportCompleted = $true
                } while ($false)
                if (-not $private:__LambdaPSRuntimeImportCompleted) {
                    throw [System.InvalidOperationException]::new(
                        "The handler module's top-level code used break or continue outside a loop of its own, so its import stopped before it finished: $($private:__LambdaPSRuntimeHandler.moduleName)")
                }
            }
        }

        # Look the handler's name up as '&' resolves it, and literally: a function, filter, alias or cmdlet,
        # not an executable on PATH, a wildcard pattern, or one of this runtime's own functions. An alias is
        # judged by the command it resolves to, which may also be a script file: an alias to nothing, to an
        # executable or to a runtime function is refused too. A misspelled name would otherwise fail every
        # event. The command found here is the one each invocation runs.
        if ($private:__LambdaPSRuntimeHandler.handlerType -ne 'Script') {
            $private:__LambdaPSRuntimeCommandTypes = [System.Management.Automation.CommandTypes]'Function, Filter, Alias, Cmdlet'
            $private:__LambdaPSRuntimeAllowedTypes = @('Function', 'Filter', 'Cmdlet')
            # Set before it is read: a handler script dot-sourced here may have turned on strict mode.
            $private:__LambdaPSRuntimeAlias = $null
            $private:__LambdaPSRuntimeCommand = $ExecutionContext.InvokeCommand.GetCommand(
                $private:__LambdaPSRuntimeHandler.functionName, $private:__LambdaPSRuntimeCommandTypes)
            if ($private:__LambdaPSRuntimeCommand -is [System.Management.Automation.AliasInfo]) {
                $private:__LambdaPSRuntimeAlias = $private:__LambdaPSRuntimeCommand
                $private:__LambdaPSRuntimeAllowedTypes += 'ExternalScript'
                $private:__LambdaPSRuntimeCommand = $private:__LambdaPSRuntimeAlias.ResolvedCommand
                # ResolvedCommand does not load a module on first use, as '&' does; GetCommand does.
                if ($null -eq $private:__LambdaPSRuntimeCommand) {
                    $private:__LambdaPSRuntimeCommand = $ExecutionContext.InvokeCommand.GetCommand(
                        $private:__LambdaPSRuntimeAlias.Definition, $private:__LambdaPSRuntimeCommandTypes)
                    if ($private:__LambdaPSRuntimeCommand -is [System.Management.Automation.AliasInfo]) {
                        $private:__LambdaPSRuntimeCommand = $private:__LambdaPSRuntimeCommand.ResolvedCommand
                    }
                }
            }
            if ($null -eq $private:__LambdaPSRuntimeCommand -or
                $private:__LambdaPSRuntimeCommand.CommandType -notin $private:__LambdaPSRuntimeAllowedTypes -or
                $private:__LambdaPSRuntimeCommand.ModuleName -eq 'pwsh-runtime') {
                # For an alias, say what it resolves to and why that is refused, since the alias itself exists.
                $private:__LambdaPSRuntimeNotFound = if ($null -eq $private:__LambdaPSRuntimeAlias) {
                    "Handler function not found: $($private:__LambdaPSRuntimeHandler.functionName)"
                }
                elseif ($null -eq $private:__LambdaPSRuntimeCommand) {
                    "Handler alias $($private:__LambdaPSRuntimeHandler.functionName) resolves to '$($private:__LambdaPSRuntimeAlias.Definition)', which was not found"
                }
                elseif ($private:__LambdaPSRuntimeCommand.ModuleName -eq 'pwsh-runtime') {
                    "Handler alias $($private:__LambdaPSRuntimeHandler.functionName) resolves to '$($private:__LambdaPSRuntimeAlias.Definition)', one of the runtime's own functions"
                }
                else {
                    "Handler alias $($private:__LambdaPSRuntimeHandler.functionName) resolves to '$($private:__LambdaPSRuntimeAlias.Definition)' ($($private:__LambdaPSRuntimeCommand.CommandType)), not a function, filter, cmdlet or script file"
                }
                throw [System.Management.Automation.CommandNotFoundException]::new("$private:__LambdaPSRuntimeNotFound (_HANDLER $env:_HANDLER)")
            }
            Microsoft.PowerShell.Utility\Add-Member -InputObject $private:__LambdaPSRuntimeHandler -NotePropertyName command -NotePropertyValue $private:__LambdaPSRuntimeCommand
        }
    }
    catch {
        $private:__LambdaPSRuntimeOwnError = & $private:__LambdaPSRuntimeIsOwnError $_ $private:__LambdaPSRuntimeFiles
        $private:__LambdaPSRuntimeStartupReported = $true
        pwsh-runtime\Send-FunctionHandlerError $private:__LambdaPSRuntimeHttpClient $_ -Uri "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime/init/error" -NoStackTrace:$private:__LambdaPSRuntimeOwnError
        exit 1
    }
    $private:__LambdaPSRuntimeStartupReported = $true
}
finally {
    if (-not $private:__LambdaPSRuntimeStartupReported) {
        pwsh-runtime\Send-FunctionHandlerError $private:__LambdaPSRuntimeHttpClient ([System.Management.Automation.ErrorRecord]::new(
                [System.InvalidOperationException]::new("The handler's start-up code ended the runtime: it used break or continue with a label that matches no loop of its own."),
                'HandlerEndedStartup', [System.Management.Automation.ErrorCategory]::InvalidOperation, $null)) -Uri "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime/init/error"
        exit 1
    }
}

# What start-up leaves, which the cleanup after each invocation restores. Jobs (including the event
# jobs behind -Action subscribers) and remoting sessions that exist now belong to start-up and are kept.
$private:__LambdaPSRuntimeModulePath = $env:PSModulePath
$private:__LambdaPSRuntimeExitCode = Microsoft.PowerShell.Utility\Get-Variable -Name LASTEXITCODE -Scope Global -ValueOnly -ErrorAction Ignore
$private:__LambdaPSRuntimeStartupJobIds = @(foreach ($private:__LambdaPSRuntimeJob in @(Microsoft.PowerShell.Core\Get-Job)) { $private:__LambdaPSRuntimeJob.Id })
$private:__LambdaPSRuntimeStartupSessionIds = @(foreach ($private:__LambdaPSRuntimeSession in @(Microsoft.PowerShell.Core\Get-PSSession)) { $private:__LambdaPSRuntimeSession.Id })

## Event loop
# Processes invocation events from Lambda Runtime API in a loop until runtime environment is terminated.
# Each turn has one of four outcomes: /next fails, so the runtime exits 1 (Lambda restarts it); the
# invocation fails, so it is reported on /error; the response is posted; or the handler ends the
# runtime (exit in a handler function, or a break or continue whose label matches no loop of its own),
# so the finally below reports it on /error and exits 1, and Lambda is told why. Invoke-FunctionHandler
# turns an unlabelled break or continue that leaves the handler into an invocation error.
if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Start event loop'}
do {
    # Get /NEXT invocation from AWS Lambda Runtime API. A handler script dot-sourced into this scope may
    # have set $ErrorActionPreference; inside try, the throw for an error status is honoured whatever it is.
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Get /NEXT invocation from AWS Lambda Runtime API'}
    try {
        $private:__LambdaPSRuntimeNextInvocation = pwsh-runtime\Get-LambdaNextInvocation $private:__LambdaPSRuntimeHttpClient
    }
    catch {
        Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]$($_.Exception.Message)"
        exit 1
    }
    # $null means the Runtime API could not be reached; ask again.
    if ($null -eq $private:__LambdaPSRuntimeNextInvocation) { continue }

    # Set default and AWS Lambda specific environment variables for each invocation
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Set default and AWS Lambda specific environment variables for each invocation'}
    pwsh-runtime\Set-HandlerEnvironmentVariables $private:__LambdaPSRuntimeNextInvocation.headers

    $private:__LambdaPSRuntimeTurnCompleted = $false
    try {
        try {
            # Create the $LambdaContext object
            if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Create the $LambdaContext object'}
            $private:__LambdaPSRuntimeContext = pwsh-runtime\Set-LambdaContext

            # Run function handler from script/module
            if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Run function handler from script/module'}
            $private:__LambdaPSRuntimeResponse = pwsh-runtime\Invoke-FunctionHandler $private:__LambdaPSRuntimeNextInvocation $private:__LambdaPSRuntimeHandler $private:__LambdaPSRuntimeContext

            # POST function handler response back to Runtime API
            if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]POST function handler response back to Runtime API'}

            pwsh-runtime\Send-FunctionHandlerResponse $private:__LambdaPSRuntimeHttpClient $private:__LambdaPSRuntimeResponse
        }
        catch {
            # POST function invocation error back to Runtime API
            if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]POST function invocation error back to Runtime API'}
            pwsh-runtime\Send-FunctionHandlerError $private:__LambdaPSRuntimeHttpClient $_ -NoStackTrace:(& $private:__LambdaPSRuntimeIsOwnError $_ $private:__LambdaPSRuntimeFiles)
        }
        $private:__LambdaPSRuntimeTurnCompleted = $true
    }
    finally {
        # Reached without completing only when the handler is ending the runtime (exit, or a labelled
        # break or continue that matches no loop): report it on /error, then exit 1 rather than 0.
        if (-not $private:__LambdaPSRuntimeTurnCompleted) {
            pwsh-runtime\Send-FunctionHandlerError $private:__LambdaPSRuntimeHttpClient ([System.Management.Automation.ErrorRecord]::new(
                    [System.InvalidOperationException]::new('The handler ended the runtime: it called exit, or used break or continue with a label that matches no loop of its own. Use return to leave the handler early.'),
                    'HandlerEndedRuntime', [System.Management.Automation.ErrorCategory]::InvalidOperation, $null))
            exit 1
        }
    }

    # Cleanup
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Cleanup'}
    # Release this invocation's event, context and response before waiting for the next one.
    $private:__LambdaPSRuntimeContext = $null
    $private:__LambdaPSRuntimeNextInvocation = $null
    $private:__LambdaPSRuntimeResponse = $null
    $env:_X_AMZN_TRACE_ID = ''
    # Remove the jobs and remoting sessions this invocation left, which would otherwise go on running into
    # later invocations, and restore $LASTEXITCODE and PSModulePath to their values after start-up. Jobs
    # and sessions that start-up created are kept. The commands are module-qualified, so a handler's
    # function of the same name is not called instead. A step that fails is logged and the next one
    # still runs: the invocation's result is already posted.
    try {
        foreach ($private:__LambdaPSRuntimeJob in @(Microsoft.PowerShell.Core\Get-Job)) {
            if ($private:__LambdaPSRuntimeJob.Id -notin $private:__LambdaPSRuntimeStartupJobIds) {
                Microsoft.PowerShell.Core\Remove-Job -Job $private:__LambdaPSRuntimeJob -Force -ErrorAction Stop
            }
        }
    }
    catch { Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]Could not remove the handler's jobs: $($_.Exception.Message)" }
    try {
        foreach ($private:__LambdaPSRuntimeSession in @(Microsoft.PowerShell.Core\Get-PSSession)) {
            if ($private:__LambdaPSRuntimeSession.Id -notin $private:__LambdaPSRuntimeStartupSessionIds) {
                Microsoft.PowerShell.Core\Remove-PSSession -Session $private:__LambdaPSRuntimeSession -ErrorAction Stop
            }
        }
    }
    catch { Microsoft.PowerShell.Utility\Write-Host "[RUNTIME]Could not remove the handler's remoting sessions: $($_.Exception.Message)" }
    if ($null -eq $private:__LambdaPSRuntimeExitCode) { Microsoft.PowerShell.Utility\Remove-Variable -Name LASTEXITCODE -Scope Global -ErrorAction Ignore }
    else { $global:LASTEXITCODE = $private:__LambdaPSRuntimeExitCode }
    $env:PSModulePath = $private:__LambdaPSRuntimeModulePath
}
while ($true)
if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') {Microsoft.PowerShell.Utility\Write-Host '[RUNTIME]Complete event loop, spindown runtime environment'}
