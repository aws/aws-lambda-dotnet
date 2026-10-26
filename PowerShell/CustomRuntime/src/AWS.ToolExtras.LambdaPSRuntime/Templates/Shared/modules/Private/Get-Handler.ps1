# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: Apache-2.0

function private:Get-Handler {
    <#
        .SYNOPSIS
            Parse _HANDLER environment variable.

        .DESCRIPTION
            Splits the handler setting into its form (Script, Function or Module), the script path or module
            name, and the function name, and throws when it is not one of the three forms.

        .Notes
            Valid "_HANDLER" options:
            - "<script.ps1>"
            - "<script.ps1>::<function_name>"
            - "Module::<module_name>::<function_name>"
    #>
    [CmdletBinding()]
    param (
        [String]$handler = $env:_HANDLER
    )

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Start: Get-Handler' }

    enum HandlerType {
        Script
        Function
        Module
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Split _HANDLER environment variable' }
    # The value is quoted, so a blank or whitespace setting is visible in the message.
    $private:invalidHandlerMessage = "Invalid Lambda Handler: _HANDLER is '{0}'. Use one of <script>.ps1, <script>.ps1::<function> or Module::<module>::<function>."
    $private:handlerArray = $handler.Split('::')
    # An empty part ('fn.ps1::', 'Module::MyMod::') names nothing to call.
    if ($private:handlerArray -contains '') { throw ($private:invalidHandlerMessage -f $handler) }

    if ($private:handlerArray[0] -like '*.ps1' -and $private:handlerArray.Count -eq 1) {
        # Handler contains only a script file
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Handler contains only a script file' }

        $private:handlerObject = @{
            handlerType    = [HandlerType]::Script
            scriptFileName = $private:handlerArray[0]
            scriptFilePath = [System.IO.Path]::Combine($env:LAMBDA_TASK_ROOT, $private:handlerArray[0])
        }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Handler type set to: $($private:handlerObject.handlerType)" }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Handler set to PowerShell script name: $($private:handlerObject.scriptFileName)" }
    }

    elseif ($private:handlerArray[0] -like '*.ps1' -and $private:handlerArray.Count -eq 2) {
        # Handler contains a script file and handler function name
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Handler contains a script file and handler function name' }

        $private:handlerObject = @{
            handlerType    = [HandlerType]::Function
            scriptFileName = $private:handlerArray[0]
            scriptFilePath = [System.IO.Path]::Combine($env:LAMBDA_TASK_ROOT, $private:handlerArray[0])
            functionName   = $private:handlerArray[1]
        }

        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Handler type set to: $($private:handlerObject.handlerType)" }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Script file set to PowerShell script name: $($private:handlerObject.scriptFileName)" }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Handler set to PowerShell function: $($private:handlerObject.functionName)" }

    }
    elseif ($private:handlerArray[0] -eq 'Module' -and $private:handlerArray.Count -eq 3) {
        # Handler contains a module name and handler function name
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Handler contains a module name and handler function name' }

        $private:handlerObject = @{
            handlerType  = [HandlerType]::Module
            moduleName   = $private:handlerArray[1]
            functionName = $private:handlerArray[2]
        }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Handler type set to: $($private:handlerObject.handlerType)" }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Module name set to PowerShell module: $($private:handlerObject.moduleName)" }
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue "[RUNTIME-Get-Handler]Handler set to PowerShell function: $($private:handlerObject.functionName)" }

    }
    # Unable to parse Handler object
    else {
        if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Unable to parse Handler object' }
        throw ($private:invalidHandlerMessage -f $handler)
    }

    if ($env:POWERSHELL_RUNTIME_VERBOSE -eq 'TRUE') { Microsoft.PowerShell.Utility\Write-Host -InformationAction Continue '[RUNTIME-Get-Handler]Return handlerObject' }
    return [pscustomobject]$private:handlerObject
}
