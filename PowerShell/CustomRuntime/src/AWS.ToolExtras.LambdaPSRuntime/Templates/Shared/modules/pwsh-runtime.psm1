# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

# NO 'Set-PSDebug -Strict' here. It is process-wide, not module-scoped, so it would impose strict mode
# on the CUSTOMER'S handler as well: a handler reading a variable it never set -- common, legal
# PowerShell, and normal in AWSLambdaPSCore scripts -- would fail inside Lambda with "The variable
# cannot be retrieved because it has not been set", while working locally.

# The private functions' Write-Host calls pass -InformationAction Continue, so that the handler's
# $InformationPreference, which these functions inherit from the global scope, does not drop or fail
# the runtime's log lines (see runtime.ps1).

# Dot-source the private functions. They are kept as separate files so each can be read and tested
# on its own; loading them is a directory listing and a few small files at import.
$privateFunctionsPath = Join-Path $PSScriptRoot 'Private'
Get-ChildItem -Path $privateFunctionsPath -Filter '*.ps1' | ForEach-Object {
    . $_.FullName
}

# The runtime's functions call these helpers through these bindings, not by name: a name is
# looked up in the scope the handler shares, where a handler's alias or function of the same name wins.
$script:__LambdaPSRuntimeSendRuntimeApiRequest = ${function:Send-RuntimeApiRequest}
$script:__LambdaPSRuntimeWriteLambdaLog = ${function:Write-LambdaLog}

# A <script>.ps1 handler runs in a child of this module's scope, so a variable left here would be one it
# can read; the loader's own variable goes once the files are loaded.
Remove-Variable -Name privateFunctionsPath -Scope Script
