<#
    .DESCRIPTION
    Argument registration, allowing dynamic completion of the supported
    templates and other data
#>
function _argumentCompleterRegistration
{
    param
    (
        # The script block that provider parameter completion
        [scriptblock]$scriptBlock,

        # Optional map restraining use of the scriptblock to designated
        # cmdlets
        [hashtable]$param2CmdletsMap
    )

    foreach ($paramName in $param2CmdletsMap.Keys)
    {
        $hash = @{
            ScriptBlock = $scriptBlock
            Parameter   = $paramName
        }

        $cmdletNames = $param2CmdletsMap[$paramName]
        if ($cmdletNames -And $cmdletNames.Length -gt 0)
        {
            $hash['Command'] = $cmdletNames
        }

        Register-ArgumentCompleter @hash -Verbose
    }
}

$PsTemplateCompleter = {
    param ($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)

    $templateNameHash = @{ }

    $manifest = _loadBlueprintManifest
    foreach ($b in $manifest.blueprints)
    {
        $templateNameHash.Add($b.name, $b.description)
    }

    $templateNameHash.Keys.Where({$_ -like "$wordToComplete*"}) | Sort-Object | Foreach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $templateNameHash[$_])
    }
}

$hashtable = @{ 'Template' = @('New-AWSPowerShellLambda') }
_argumentCompleterRegistration -scriptBlock $PsTemplateCompleter -param2CmdletsMap $hashtable

