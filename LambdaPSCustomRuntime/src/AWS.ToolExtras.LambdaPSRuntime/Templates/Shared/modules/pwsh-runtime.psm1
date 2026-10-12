# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

# NO 'Set-PSDebug -Strict' here. It is process-wide, not module-scoped, so it would impose strict mode
# on the CUSTOMER'S handler as well: a handler reading a variable it never set -- common, legal
# PowerShell, and normal in AWSLambdaPSCore scripts -- would fail inside Lambda with "The variable
# cannot be retrieved because it has not been set", while working locally.

# Dot-source the private functions. They are kept as separate files so each can be read and tested
# on its own; loading them is a directory listing and nine small files at import.
$privateFunctionsPath = Join-Path $PSScriptRoot 'Private'
Get-ChildItem -Path $privateFunctionsPath -Filter '*.ps1' | ForEach-Object {
    . $_.FullName
}
