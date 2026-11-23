BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # Copies the module (without its tests) under $TestDrive, applies $Change to the copy's Config
    # folder, and imports the copy in a child pwsh. Returns the child's output and the Config folder:
    # a refusal prints 'FAILED: <error id> | <target paths> | <message>'.
    function Invoke-ImportWithConfig {
        param(
            [Parameter(Mandatory)] [string]$Name,
            [Parameter(Mandatory)] [scriptblock]$Change
        )
        $copy = [System.IO.Path]::Combine($TestDrive, $Name, 'AWS.ToolExtras.LambdaPSRuntime')
        $null = New-Item -ItemType Directory -Path $copy -Force
        Get-ChildItem -LiteralPath $script:moduleRoot -Force | Where-Object Name -ne 'Tests' |
            Copy-Item -Destination $copy -Recurse -Force
        & $Change ([System.IO.Path]::Combine($copy, 'Config'))
        $manifest = [System.IO.Path]::Combine($copy, 'AWS.ToolExtras.LambdaPSRuntime.psd1')
        $command = "try { Import-Module '$manifest' -ErrorAction Stop; 'IMPORTED' } catch { 'FAILED: ' + `$_.FullyQualifiedErrorId + ' | ' + (@(`$_.TargetObject) -join ';') + ' | ' + `$_.Exception.Message }"
        [PSCustomObject]@{
            Output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command $command 2>&1 | Out-String
            Config = [System.IO.Path]::Combine($copy, 'Config')
        }
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Module import' -Tag 'Component' {
    It 'refuses to import, naming every Config file, when the install has no Config folder' {
        $result = Invoke-ImportWithConfig -Name 'no-config' -Change {
            param($config)
            Remove-Item -LiteralPath $config -Recurse -Force
        }

        $result.Output | Should -BeLike "*FAILED: ConfigNotFound* | $((@('pwsh.psd1', 'wait.psd1', 'function.psd1', 'installer.psd1') | ForEach-Object { [System.IO.Path]::Combine($result.Config, $_) }) -join ';') | *"
        $result.Output | Should -BeLike "*Config/pwsh.psd1, Config/wait.psd1, Config/function.psd1, Config/installer.psd1 was not found*Reinstall the module*"
    }

    It 'refuses to import, naming only the missing file, when one Config file is absent' {
        $result = Invoke-ImportWithConfig -Name 'partial-config' -Change {
            param($config)
            Remove-Item -LiteralPath ([System.IO.Path]::Combine($config, 'function.psd1'))
        }

        $result.Output | Should -BeLike "*FAILED: ConfigNotFound* | $([System.IO.Path]::Combine($result.Config, 'function.psd1')) | *"
        $result.Output | Should -BeLike "*The module's Config/function.psd1 was not found*Reinstall the module*"
    }

    It 'refuses to import, naming the file, when a Config file cannot be parsed' {
        $result = Invoke-ImportWithConfig -Name 'malformed' -Change {
            param($config)
            Set-Content -LiteralPath ([System.IO.Path]::Combine($config, 'function.psd1')) -Value '@{ Timeout = '
        }

        $result.Output | Should -BeLike "*FAILED: ConfigUnreadable* | $([System.IO.Path]::Combine($result.Config, 'function.psd1')) | *"
        $result.Output | Should -BeLike "*Config/function.psd1 could not be read*Reinstall the module*"
        $result.Output | Should -Not -BeLike '*IMPORTED*'
    }

    It 'refuses to import, naming the setting, when a Config file lacks one' {
        $result = Invoke-ImportWithConfig -Name 'missing-key' -Change {
            param($config)
            Set-Content -LiteralPath ([System.IO.Path]::Combine($config, 'function.psd1')) -Value '@{ TimeOut_ = 30; MemorySize = 512 }'
        }

        $result.Output | Should -BeLike "*FAILED: ConfigSettingMissing* | $([System.IO.Path]::Combine($result.Config, 'function.psd1')) | *"
        $result.Output | Should -BeLike "*Missing: Timeout in Config/function.psd1.*Reinstall the module*"
    }

    It 'refuses to import, naming every missing setting, when a nested setting is absent' {
        $result = Invoke-ImportWithConfig -Name 'missing-nested' -Change {
            param($config)
            Set-Content -LiteralPath ([System.IO.Path]::Combine($config, 'wait.psd1')) -Value '@{ Function = @{ IntervalSeconds = 2 }; RuntimeVersion = @{ Attempts = 60; IntervalSeconds = 3 }; Role = @{ Attempts = 12; IntervalSeconds = 5 } }'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($config, 'function.psd1')) -Value '@{ Timeout = 30 }'
        }

        $result.Output | Should -BeLike "*FAILED: ConfigSettingMissing* | $((@('wait.psd1', 'function.psd1') | ForEach-Object { [System.IO.Path]::Combine($result.Config, $_) }) -join ';') | *"
        $result.Output | Should -BeLike "*Missing: Function.Attempts in Config/wait.psd1, MemorySize in Config/function.psd1.*Reinstall the module*"
    }

    It 'refuses to import, naming the setting, when Config/pwsh.psd1 has no download operation timeout' {
        $result = Invoke-ImportWithConfig -Name 'no-operation-timeout' -Change {
            param($config)
            $file = [System.IO.Path]::Combine($config, 'pwsh.psd1')
            Set-Content -LiteralPath $file -Value ((Get-Content -LiteralPath $file) -notmatch '^\s*OperationTimeoutSeconds\s*=')
        }

        $result.Output | Should -BeLike "*FAILED: ConfigSettingMissing* | $([System.IO.Path]::Combine($result.Config, 'pwsh.psd1')) | *"
        $result.Output | Should -BeLike "*Missing: Download.OperationTimeoutSeconds in Config/pwsh.psd1, PackageRepository.OperationTimeoutSeconds in Config/pwsh.psd1.*Reinstall the module*"
    }

    It 'ships download timeouts that are positive whole numbers, with the whole-download limit above the connect limit' {
        $download = (Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine($script:moduleRoot, 'Config', 'pwsh.psd1'))).Download

        foreach ($name in 'ConnectTimeoutSeconds', 'OperationTimeoutSeconds') {
            $download[$name] | Should -BeOfType [int]
            $download[$name] | Should -BeGreaterThan 0
        }
        $download.OperationTimeoutSeconds | Should -BeGreaterThan $download.ConnectTimeoutSeconds
    }

    # The Dockerfiles' pinned signing-key fingerprint and their arm64 tarball fallback are for this
    # repository, so moving it is a reviewed change to this test too.
    It 'ships Microsoft''s RHEL 9 repository as PackageRepository.Url' {
        (Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine($script:moduleRoot, 'Config', 'pwsh.psd1'))).PackageRepository.Url |
            Should -BeExactly 'https://packages.microsoft.com/rhel/9/prod'
    }

    It 'pins an x64 and an arm64 SHA256 for every pwsh release in Config/pwsh.psd1, including each default' {
        $pwsh = Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine($script:moduleRoot, 'Config', 'pwsh.psd1'))

        foreach ($release in $pwsh.DefaultPatch.Values) {
            $pwsh.ArchiveSha256.Keys | Should -Contain $release
        }
        foreach ($release in $pwsh.ArchiveSha256.Keys) {
            @($pwsh.ArchiveSha256[$release].Keys | Sort-Object) | Should -Be @('arm64', 'x64') -Because "release $release"
            foreach ($hash in $pwsh.ArchiveSha256[$release].Values) {
                $hash | Should -Match '^[0-9a-f]{64}$' -Because "release $release"
            }
        }
    }

    # An AWS.Tools build below the floors has no customer-managed runtime cmdlets, so the module is
    # refused at import rather than failing at its first AWS call.
    It 'refuses to import against AWS.Tools modules older than its RequiredModules floors' {
        $required = (Import-PowerShellDataFile -LiteralPath $script:modulePath).RequiredModules
        $oldTools = [System.IO.Path]::Combine($TestDrive, 'old-aws-tools')
        foreach ($module in $required) {
            # A released AWS.Tools build below the floors.
            $folder = [System.IO.Path]::Combine($oldTools, $module.ModuleName, '5.0.224')
            $null = New-Item -ItemType Directory -Path $folder -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($folder, "$($module.ModuleName).psd1")) -Value "@{ ModuleVersion = '5.0.224'; GUID = '$($module.GUID)' }"
        }
        $modulePath = $oldTools + [System.IO.Path]::PathSeparator + [System.IO.Path]::Combine($PSHOME, 'Modules')
        $target = [System.IO.Path]::Combine($TestDrive, 'never-created')
        # After the refusal, a cmdlet of the module is not there to run, so nothing is created.
        $command = "`$env:PSModulePath = '$modulePath'; try { Import-Module '$script:modulePath' -ErrorAction Stop; 'IMPORTED' } catch { 'FAILED: ' + `$_.FullyQualifiedErrorId + ' | ' + `$_.Exception.Message }; " +
            "try { New-LambdaPSRuntimeProject -Path '$target' -PowerShellVersion '7.4.*' -ErrorAction Stop; 'RAN' } catch { 'NEXT: ' + `$_.FullyQualifiedErrorId }"

        $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command $command 2>&1 | Out-String

        # The error ID, not pwsh's message, which is localized; the module it names is not.
        $output | Should -BeLike '*FAILED: Modules_InvalidManifest,Microsoft.PowerShell.Commands.ImportModuleCommand | *'
        $output | Should -BeLike "*'AWS.Tools.Lambda'*"
        $output | Should -BeLike '*NEXT: CommandNotFoundException*'
        Test-Path -LiteralPath $target | Should -BeFalse
    }

    # Every Public/ cmdlet, and nothing else, imported from a caller in the strictest mode. A caller's
    # strict mode does not reach the module's own code, so this does not check that code.
    It 'imports from a caller under Set-StrictMode -Version Latest and exports every Public/ cmdlet' {
        $public = [System.IO.Path]::Combine($script:moduleRoot, 'Public')
        $expected = @()
        if (Test-Path -LiteralPath $public) { $expected = @((Get-ChildItem -LiteralPath $public -Filter '*.ps1').BaseName | Sort-Object) }
        $command = "Set-StrictMode -Version Latest; try { Import-Module '$script:modulePath' -ErrorAction Stop; 'EXPORTED: ' + (@(Get-Command -Module AWS.ToolExtras.LambdaPSRuntime | ForEach-Object Name | Sort-Object) -join ',') } catch { 'FAILED: ' + `$_.FullyQualifiedErrorId }"

        $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command $command 2>&1 | Out-String

        $output | Should -Not -BeLike '*FAILED:*'
        $exported = @($output -split "`r?`n" | Where-Object { $_ -like 'EXPORTED:*' })
        $exported | Should -HaveCount 1
        ($exported[0] -replace '^EXPORTED:\s*', '') | Should -BeExactly ($expected -join ',')
    }
}

Describe 'Module loader' -Tag 'Component' {
    # Square brackets are wildcard characters to a -Path parameter, so the loader must read its own
    # files by literal path.
    It 'imports from a folder whose path contains square brackets, with every Private/ and Public/ function' {
        $copy = [System.IO.Path]::Combine($TestDrive, '[x]', 'AWS.ToolExtras.LambdaPSRuntime')
        $null = New-Item -ItemType Directory -Path $copy -Force
        Get-ChildItem -LiteralPath $script:moduleRoot -Force | Where-Object Name -ne 'Tests' |
            Copy-Item -Destination $copy -Recurse -Force
        $functionFiles = @('Private', 'Public' | ForEach-Object { [System.IO.Path]::Combine($script:moduleRoot, $_) } |
            Where-Object { Test-Path -LiteralPath $_ } | ForEach-Object { Get-ChildItem -LiteralPath $_ -Filter '*.ps1' })
        $manifest = [System.IO.Path]::Combine($copy, 'AWS.ToolExtras.LambdaPSRuntime.psd1')
        $command = "try { Import-Module -Name '$manifest' -ErrorAction Stop; 'IMPORTED'; 'DEFINED:' + @(& (Get-Module AWS.ToolExtras.LambdaPSRuntime) { param(`$names) `$names | Where-Object { Get-Command -Name `$_ -CommandType Function -ErrorAction SilentlyContinue } } @('$(($functionFiles.BaseName) -join "','")')).Count } catch { 'FAILED: ' + `$_.FullyQualifiedErrorId + ' | ' + `$_.Exception.Message }"

        $output = & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command $command 2>&1 | Out-String

        $output | Should -BeLike '*IMPORTED*'
        $output | Should -BeLike "*DEFINED:$($functionFiles.Count)*"
    }

    It 'leaves only its settings in the module scope' {
        # An empty module's script scope holds only PowerShell's automatic variables, plus $foreach,
        # which PowerShell leaves behind after any foreach loop.
        $automatic = @(& (New-Module -ScriptBlock { }) { (Get-Variable -Scope Script).Name }) + 'foreach'

        $own = & (Get-Module $script:moduleName) { (Get-Variable -Scope Script).Name } | Where-Object { $_ -notin $automatic }

        $own | Sort-Object | Should -Be @('Config', 'LambdaPSClientAppId', 'LambdaPSModuleVersion')
    }
}
