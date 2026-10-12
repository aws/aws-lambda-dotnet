# The module's public surface: what it exports, and the help every exported cmdlet carries.
#
# Comment-based help fails silently. A help block the parser cannot read (for example a line that
# starts with '.zip', which it takes for an unknown keyword) is discarded whole, and Get-Help falls
# back to a generated syntax line with no parameter descriptions. Nothing else in the suite notices.

BeforeDiscovery {
    $moduleRoot = Split-Path -Parent $PSScriptRoot
    $script:PublicNames = @(Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($moduleRoot, 'Public')) -Filter '*.ps1' |
            ForEach-Object { @{ Name = $_.BaseName } })
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    $script:publicFiles = @(Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($script:moduleRoot, 'Public')) -Filter '*.ps1' |
            ForEach-Object BaseName | Sort-Object)
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Module exports' -Tag 'Contract' {
    It 'lists exactly the Public/ cmdlets in the manifest''s FunctionsToExport' {
        $manifest = Import-PowerShellDataFile -LiteralPath $script:modulePath
        @($manifest.FunctionsToExport | Sort-Object) | Should -Be $script:publicFiles
    }

    It 'exports exactly the Public/ cmdlets' {
        @((Get-Module 'AWS.ToolExtras.LambdaPSRuntime').ExportedFunctions.Keys | Sort-Object) | Should -Be $script:publicFiles
    }
}

Describe 'Parameter binding' -Tag 'Contract' {
    # Implicit positional binding gives every parameter a position from its declaration order, so
    # reordering or inserting a parameter would silently break callers that pass values by position.
    It '<Name> binds at most one parameter by position' -ForEach $script:PublicNames {
        $positional = @((Get-Command $Name).Parameters.Values | Where-Object {
                $_.ParameterSets.Values | Where-Object { $_.Position -ge 0 }
            })
        $positional.Count | Should -BeLessOrEqual 1
    }

    # A blank string is refused when it is bound, with the parameter named, rather than reaching a
    # helper or an AWS call. The exceptions accept a blank value on purpose, each for the reason given.
    It '<Name> refuses blank values for its string parameters' -ForEach $script:PublicNames {
        $exceptions = @{
            # A gate reports a blank project as ProjectNotFound rather than failing binding.
            'Test-LambdaPSRuntimeProject' = @('Path')
        }
        $common = [System.Management.Automation.Cmdlet]::CommonParameters + [System.Management.Automation.Cmdlet]::OptionalCommonParameters
        if ($exceptions.ContainsKey($Name)) { $common += $exceptions[$Name] }
        $validating = [System.Management.Automation.ValidateNotNullOrWhiteSpaceAttribute],
            [System.Management.Automation.ValidateSetAttribute], [System.Management.Automation.ValidatePatternAttribute]
        $unvalidated = @((Get-Command $Name).Parameters.Values | Where-Object {
                $parameter = $_
                $parameter.ParameterType -in [string], [string[]] -and $common -notcontains $parameter.Name -and
                @($parameter.Attributes | Where-Object { $attribute = $_; @($validating | Where-Object { $attribute -is $_ }).Count -gt 0 }).Count -eq 0
            } | ForEach-Object Name)
        $unvalidated -join ', ' | Should -BeNullOrEmpty
    }

    # One name per concept across the cmdlets, with the other names in use kept as aliases.
    It '<Command> -<Parameter> also answers to -<Alias>' -ForEach @(
        @{ Command = 'Build-LambdaPSRuntimeImage'; Parameter = 'Path'; Alias = 'ProjectPath' }
        @{ Command = 'Build-LambdaPSRuntimePackage'; Parameter = 'Path'; Alias = 'ProjectPath' }
        @{ Command = 'Build-LambdaPSRuntimePackage'; Parameter = 'OutFile'; Alias = 'PackagePath' }
        @{ Command = 'Compress-LambdaPSFunctionPackage'; Parameter = 'Path'; Alias = 'ProjectPath' }
        @{ Command = 'Compress-LambdaPSFunctionPackage'; Parameter = 'OutFile'; Alias = 'PackagePath' }
        @{ Command = 'Test-LambdaPSRuntimeProject'; Parameter = 'Path'; Alias = 'ProjectPath' }
        @{ Command = 'New-LambdaPSRuntimeProject'; Parameter = 'Path'; Alias = 'ProjectPath' }
        @{ Command = 'New-LambdaPSFunctionProject'; Parameter = 'Path'; Alias = 'ProjectPath' }
        @{ Command = 'New-LambdaPSRuntimeDeployment'; Parameter = 'ProjectPath'; Alias = 'Path' }
        @{ Command = 'New-LambdaPSFunctionDeployment'; Parameter = 'ProjectPath'; Alias = 'Path' }
        @{ Command = 'New-LambdaPSFunctionDeployment'; Parameter = 'ProjectPath'; Alias = 'ScriptPath' }
        @{ Command = 'Test-LambdaPSRuntimeProject'; Parameter = 'Path'; Alias = 'FullName' }
        @{ Command = 'New-LambdaPSFunctionDeployment'; Parameter = 'IncludeModule'; Alias = 'Module' }
        @{ Command = 'New-LambdaPSRuntimeDeployment'; Parameter = 'IncludeModule'; Alias = 'Module' }
        @{ Command = 'Compress-LambdaPSFunctionPackage'; Parameter = 'IncludeModule'; Alias = 'Module' }
        @{ Command = 'New-LambdaPSRuntimeProject'; Parameter = 'IncludeModule'; Alias = 'Module' }
        @{ Command = 'New-LambdaPSFunctionProject'; Parameter = 'IncludeModule'; Alias = 'Module' }
    ) {
        (Get-Command $Command).Parameters[$Parameter].Aliases | Should -Contain $Alias
    }

    # A hashtable, as Lambda's environment is a map of names to values, and optional: when it is
    # omitted, a redeploy leaves the function's environment as it is.
    It 'New-LambdaPSFunctionDeployment -EnvironmentVariable is an optional hashtable, bound by name' {
        $parameter = (Get-Command New-LambdaPSFunctionDeployment).Parameters['EnvironmentVariable']
        $parameter.ParameterType | Should -Be ([hashtable])
        @($parameter.ParameterSets.Values | Where-Object { $_.IsMandatory -or $_.Position -ge 0 }) | Should -BeNullOrEmpty
    }

    # -PackagePath has no -OutFile alias.
    It '<Command> has no -<Alias> alias' -ForEach @(
        @{ Command = 'New-LambdaPSFunctionDeployment'; Alias = 'OutFile' }
    ) {
        @((Get-Command $Command).Parameters.Values.Aliases) | Should -Not -Contain $Alias
    }
}

Describe 'ShouldProcess' -Tag 'Contract' {
    # Invoke-LMFunction declares ConfirmImpact Low, so an invoke never asks unless -Confirm is given.
    It 'Invoke-LambdaPSFunction supports -WhatIf and -Confirm at ConfirmImpact Low, as Invoke-LMFunction does' {
        $binding = (Get-Command Invoke-LambdaPSFunction).ScriptBlock.Attributes |
            Where-Object { $_ -is [System.Management.Automation.CmdletBindingAttribute] }
        $binding.SupportsShouldProcess | Should -BeTrue
        $binding.ConfirmImpact | Should -Be ([System.Management.Automation.ConfirmImpact]::Low)
    }
}

# The help states the function defaults and the readiness wait as numbers, so a reader sees them.
# Those numbers come from Config/, so a Config change must come with a help change.
Describe 'Help that states Config values' -Tag 'Contract' {
    BeforeAll {
        $script:config = & (Get-Module 'AWS.ToolExtras.LambdaPSRuntime') { $script:Config }
        $functionWait = $script:config.wait.Function
        $script:functionWaitSeconds = $functionWait.Attempts * $functionWait.IntervalSeconds
        # The comment-based help of a source file, read with the parser, so private helpers count too.
        $script:helpOf = {
            param([string]$Folder, [string]$Name)
            $path = [System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), $Folder, "$Name.ps1")
            $ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$null, [ref]$null)
            $ast.Find({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $args[0].Name -eq $Name }, $true).GetHelpContent()
        }
    }

    It 'New-LambdaPSFunctionDeployment -Timeout and -MemorySize state the Config/function.psd1 defaults' {
        $help = & $script:helpOf 'Public' 'New-LambdaPSFunctionDeployment'
        $help.Parameters['TIMEOUT'] -replace '\s+', ' ' | Should -Match "Defaults to $($script:config.function.Timeout),"
        $help.Parameters['MEMORYSIZE'] -replace '\s+', ' ' | Should -Match "Defaults to $($script:config.function.MemorySize),"
    }

    It 'New-LambdaPSFunctionDeployment states the Config/wait.psd1 function wait' {
        (& $script:helpOf 'Public' 'New-LambdaPSFunctionDeployment').Description -replace '\s+', ' ' |
            Should -Match "up to $($script:functionWaitSeconds) seconds"
    }

    It 'README.md states the Config/function.psd1 defaults and the Config/wait.psd1 function wait' {
        $readme = [System.IO.Path]::Combine((Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))), 'README.md')
        $text = (Get-Content -LiteralPath $readme -Raw) -replace '\s+', ' '
        $text | Should -Match "defaults ``-Timeout`` to $($script:config.function.Timeout) seconds and ``-MemorySize`` to $($script:config.function.MemorySize) MB"
        $text | Should -Match "New-LambdaPSFunctionDeployment`` waits for both to settle \(up to $($script:functionWaitSeconds) seconds each"
    }

    It 'Publish-LambdaPSFunction states the Config/wait.psd1 poll interval and budget' {
        (& $script:helpOf 'Private' 'Publish-LambdaPSFunction').Description -replace '\s+', ' ' |
            Should -Match "\($($script:config.wait.Function.IntervalSeconds) s poll, $($script:functionWaitSeconds) s budget each\)"
    }
}

Describe 'Comment-based help' -Tag 'Contract' {
    It '<Name> has a synopsis and a description' -ForEach $script:PublicNames {
        $help = Get-Help $Name -Full
        # When the help block is unreadable, Get-Help puts the generated syntax line in Synopsis.
        $help.Synopsis | Should -Not -BeNullOrEmpty
        $help.Synopsis.Trim() | Should -Not -BeLike "$Name *" -Because 'the comment-based help block did not parse'
        ($help.Description | Out-String).Trim() | Should -Not -BeNullOrEmpty
    }

    It '<Name> describes every parameter' -ForEach $script:PublicNames {
        $common = [System.Management.Automation.Cmdlet]::CommonParameters + [System.Management.Automation.Cmdlet]::OptionalCommonParameters
        $declared = @((Get-Command $Name).Parameters.Keys | Where-Object { $common -notcontains $_ })
        $help = Get-Help $Name -Full
        $undocumented = @($declared | Where-Object {
                $parameterName = $_
                $entry = @($help.Parameters.Parameter) | Where-Object { $_.Name -eq $parameterName }
                $null -eq $entry -or [string]::IsNullOrWhiteSpace(($entry.Description | Out-String))
            })
        $undocumented -join ', ' | Should -BeNullOrEmpty
    }

    It '<Name> has at least one example' -ForEach $script:PublicNames {
        @((Get-Help $Name -Full).Examples.Example | Where-Object { $null -ne $_ }).Count | Should -BeGreaterThan 0
    }
}

# A helper's $PSCmdlet.ThrowTerminatingError ends only the statement that called it, so a public
# cmdlet with no try/catch of its own carries on past a failure. Component.ErrorPropagation shows
# the effect in a fresh runspace; this checks every cmdlet has the wrap.
Describe 'Public cmdlets stop on a helper''s terminating error' -Tag 'Contract' {
    It '<Name> wraps its body in a try/catch that rethrows with ThrowTerminatingError' -ForEach @(
        Get-ChildItem -LiteralPath ([System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'Public')) -Filter '*.ps1' |
            ForEach-Object { @{ Name = $_.BaseName; Path = $_.FullName } }
    ) {
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$null)
        $function = $ast.Find({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)
        $blocks = @($function.Body.BeginBlock, $function.Body.ProcessBlock, $function.Body.EndBlock) |
            Where-Object { $null -ne $_ -and $_.Statements.Count -gt 0 }
        $blocks | Should -Not -BeNullOrEmpty

        foreach ($block in $blocks) {
            $block.Statements.Count | Should -Be 1 -Because "the whole $($block.BlockKind) block belongs inside the try"
            $try = $block.Statements[0]
            $try | Should -BeOfType ([System.Management.Automation.Language.TryStatementAst])
            $catchAll = @($try.CatchClauses | Where-Object IsCatchAll)
            $catchAll.Count | Should -Be 1
            $catchAll[0].Body.Extent.Text | Should -Match '\$PSCmdlet\.ThrowTerminatingError\(\$_\)'
        }
    }
}
