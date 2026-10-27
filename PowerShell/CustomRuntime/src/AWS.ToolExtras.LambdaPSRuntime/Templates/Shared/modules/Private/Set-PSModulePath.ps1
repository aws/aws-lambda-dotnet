# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Set-PSModulePath {
    <#
        .SYNOPSIS
            Modify $env:PSModulePath to support Lambda module file paths.

        .DESCRIPTION
            Sets $env:PSModulePath to the function's, the layers', the runtime's and pwsh's module folders, in
            that order.

        .Notes
            FUNCTION-FIRST order. Because pwsh module resolution is POSITIONAL (first match on
            PSModulePath wins, NOT highest version), the function's own modules must come first so a
            function can override a runtime default:
                1: Function package modules (/var/task/modules) — wins
                2: Lambda Layer modules (/opt/modules)
                3: Runtime default modules — the modules directory this file ships in
                4: Modules shipped with pwsh ($PSHOME/Modules) — last, so a bundled module can
                   never shadow a function/runtime module of the same name

            Tier 3 is derived from this file's own location rather than spelled per target, so one
            copy of this file serves every target. The runtime's modules directory is
            /var/runtime/modules for a runtime image or container image, /opt/modules for a layer,
            and /var/task/modules for a self-contained zip. In the last two it is already tier 2 or
            tier 1, and the duplicate is dropped, leaving the earlier position in force.

            This ASSIGNS PSModulePath rather than appending, which discards the default value pwsh
            builds at startup — so the pwsh-shipped tier has to be put back explicitly, and it MUST
            be spelled $PSHOME/Modules:

              * The directory is 'Modules' with a capital M (that is how the Microsoft RPM and the
                release tarball lay it down) and Lambda runs on Linux, where paths are
                case-sensitive. A lowercase '.../powershell/modules' silently resolves to nothing.
              * $PSHOME is derived from the running interpreter, so this one expression is correct
                for every target (/var/runtime/powershell, /opt/powershell, /var/task/powershell).

            Getting this wrong does not break a hello-world handler: the core modules
            (Microsoft.PowerShell.Utility/.Management/.Security/.Host) are already imported at
            session start. It breaks the ones that are auto-loaded on first use — Compress-Archive
            (Microsoft.PowerShell.Archive), Start-ThreadJob (ThreadJob), Get-CimInstance
            (CimCmdlets) — with 'The term ... is not recognized'.
    #>
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Set-PSModulePath]Start: Set-PSModulePath' }
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Set-PSModulePath]Setting PSModulePath environment variable' }
    $env:PSModulePath = @(
        [System.IO.Path]::Combine($env:LAMBDA_TASK_ROOT, 'modules'), # Function package modules — wins
        '/opt/modules', # User supplied modules as part of Lambda Layers
        (Microsoft.PowerShell.Management\Split-Path -Parent $PSScriptRoot), # Runtime default modules (this file is in <modules>/Private)
        [System.IO.Path]::Combine($PSHOME, 'Modules') # Modules shipped with pwsh — last
    ) | Microsoft.PowerShell.Utility\Select-Object -Unique | Microsoft.PowerShell.Utility\Join-String -Separator ':'
    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Set-PSModulePath]PSModulePath environment variable set to: $($env:PSModulePath)" }
}
