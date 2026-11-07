BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Copy-LambdaPSProjectTemplate' -Tag 'Unit' {
    It 'merges the source roots in order, writing a path carried by both once, from the later root' {
        $shared = Join-Path $TestDrive 'merge-shared'
        $own = Join-Path $TestDrive 'merge-own'
        $null = New-Item -ItemType Directory -Path (Join-Path $shared 'modules'), $own -Force
        'shared' | Set-Content -LiteralPath (Join-Path $shared 'modules/a.ps1')
        'shared' | Set-Content -LiteralPath (Join-Path $shared 'both.txt')
        'own' | Set-Content -LiteralPath (Join-Path $own 'both.txt')
        'own' | Set-Content -LiteralPath (Join-Path $own 'bootstrap')
        $destination = Join-Path $TestDrive 'merge-out'

        $written = InModuleScope $script:moduleName -Parameters @{ Roots = @($shared, $own); Destination = $destination } {
            param($Roots, $Destination)
            Copy-LambdaPSProjectTemplate -Path $Roots -Destination $Destination -PwshVersion '7.4.20' -Target Layer -PowerShellVersion '7.4'
        }

        (Get-Content -LiteralPath (Join-Path $destination 'modules/a.ps1')) | Should -Be 'shared'
        (Get-Content -LiteralPath (Join-Path $destination 'both.txt')) | Should -Be 'own'
        (Get-Content -LiteralPath (Join-Path $destination 'bootstrap')) | Should -Be 'own'
        @($written | Where-Object { [System.IO.Path]::GetFileName($_) -eq 'both.txt' }).Count | Should -Be 1
    }

    It 'normalizes a CRLF <File> to LF' -ForEach @(@{ File = 'bootstrap' }, @{ File = 'lambda-entrypoint.sh' }) {
        $source = Join-Path $TestDrive "crlf-src-$File"
        $null = New-Item -ItemType Directory -Path $source -Force
        [System.IO.File]::WriteAllText((Join-Path $source $File), "#!/bin/sh`r`nexec /opt/powershell/pwsh`r`n")
        $destination = Join-Path $TestDrive "crlf-out-$File"

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            $null = Copy-LambdaPSProjectTemplate -Path $Source -Destination $Destination -PwshVersion '7.4.20' -Target Layer -PowerShellVersion '7.4'
        }

        [System.IO.File]::ReadAllText((Join-Path $destination $File)) | Should -Be "#!/bin/sh`nexec /opt/powershell/pwsh`n"
    }

    It 'adds a later root''s <name>.append to the end of <name> instead of writing it' {
        $base = Join-Path $TestDrive 'append-base'
        $extra = Join-Path $TestDrive 'append-extra'
        $null = New-Item -ItemType Directory -Path $base, $extra -Force
        [System.IO.File]::WriteAllText((Join-Path $base 'Dockerfile'), "FROM base`n")
        [System.IO.File]::WriteAllText((Join-Path $extra 'Dockerfile.append'), "CMD [ `"handler.ps1`" ]`n")
        $destination = Join-Path $TestDrive 'append-out'

        $written = InModuleScope $script:moduleName -Parameters @{ Roots = @($base, $extra); Destination = $destination } {
            param($Roots, $Destination)
            Copy-LambdaPSProjectTemplate -Path $Roots -Destination $Destination -PwshVersion '7.4.20' -Target Container -PowerShellVersion '7.4' -HandlerScript 'fn.ps1'
        }

        [System.IO.File]::ReadAllText((Join-Path $destination 'Dockerfile')) | Should -BeExactly "FROM base`n`nCMD [ `"fn.ps1`" ]`n" -Because 'one blank line separates the appended section'
        Test-Path -LiteralPath (Join-Path $destination 'Dockerfile.append') | Should -BeFalse
        @($written | Where-Object { $_ -like '*.append' }) | Should -BeNullOrEmpty
    }
    It 'stamps the runtime client version into the <Target> project''s pwsh-runtime.psd1' -ForEach @(
        @{ Target = 'CMR' }, @{ Target = 'Container' }, @{ Target = 'Layer' }, @{ Target = 'SelfContainedZip' }
    ) {
        $destination = [System.IO.Path]::Combine($TestDrive, "ricver-$Target")
        $moduleVersion = (Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine((Split-Path -Parent $PSScriptRoot), 'AWS.ToolExtras.LambdaPSRuntime.psd1'))).ModuleVersion

        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Destination = $destination } {
            param($Target, $Destination)
            $copyParams = @{
                Path              = Get-LambdaPSProjectTemplateRoot -Target $Target
                Destination       = $Destination
                PwshVersion       = '7.4.20'
                Target            = $Target
                PowerShellVersion = '7.4'
            }
            $null = Copy-LambdaPSProjectTemplate @copyParams -WarningAction SilentlyContinue
        }

        (Import-PowerShellDataFile -LiteralPath ([System.IO.Path]::Combine($destination, 'modules', 'pwsh-runtime.psd1'))).ModuleVersion | Should -Be $moduleVersion
    }

    It 'replaces each -Token, as a JSON value in a .json file and as text elsewhere' {
        $source = Join-Path $TestDrive 'token-src'
        $null = New-Item -ItemType Directory -Path $source -Force
        [System.IO.File]::WriteAllText((Join-Path $source 'manifest.json'), '{ "Alias": __ALIAS__, "Arch": __ARCH__ }')
        [System.IO.File]::WriteAllText((Join-Path $source 'README.md'), "alias '__ALIAS__'")
        $destination = Join-Path $TestDrive 'token-out'

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            $null = Copy-LambdaPSProjectTemplate -Path $Source -Destination $Destination -Target RuntimeBound `
                -Token @{ __ALIAS__ = 'a"b\c'; __ARCH__ = $null }
        }

        $manifest = [System.IO.File]::ReadAllText((Join-Path $destination 'manifest.json')) | ConvertFrom-Json
        $manifest.Alias | Should -BeExactly 'a"b\c'
        $manifest.Arch | Should -BeNullOrEmpty
        [System.IO.File]::ReadAllText((Join-Path $destination 'README.md')) | Should -BeExactly "alias 'a`"b\c'"
    }

    It 'does not read a value written in as a placeholder' {
        $source = Join-Path $TestDrive 'token-once-src'
        $null = New-Item -ItemType Directory -Path $source -Force
        [System.IO.File]::WriteAllText((Join-Path $source 'manifest.json'), '{ "Alias": __ALIAS__, "Arch": __ARCH__ }')
        [System.IO.File]::WriteAllText((Join-Path $source 'README.md'), "alias __ALIAS__, timeout __TIMEOUT__")
        $destination = Join-Path $TestDrive 'token-once-out'

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            $null = Copy-LambdaPSProjectTemplate -Path $Source -Destination $Destination -Target RuntimeBound `
                -Token @{ __ALIAS__ = '__ARCH__ __TIMEOUT__'; __ARCH__ = 'arm64' }
        }

        $manifest = [System.IO.File]::ReadAllText((Join-Path $destination 'manifest.json')) | ConvertFrom-Json
        $manifest.Alias | Should -BeExactly '__ARCH__ __TIMEOUT__'
        $manifest.Arch | Should -BeExactly 'arm64'
        $timeout = InModuleScope $script:moduleName { $script:Config.function.Timeout }
        [System.IO.File]::ReadAllText((Join-Path $destination 'README.md')) |
            Should -BeExactly "alias __ARCH__ __TIMEOUT__, timeout $timeout"
    }

    It 'writes no lambdaps-runtime.json marker for RuntimeBound, and needs no pwsh version for it' {
        $source = Join-Path $TestDrive 'bound-src'
        $null = New-Item -ItemType Directory -Path $source -Force
        [System.IO.File]::WriteAllText((Join-Path $source 'handler.ps1'), 'param($LambdaInput, $LambdaContext)')
        $destination = Join-Path $TestDrive 'bound-out'

        $written = InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            Copy-LambdaPSProjectTemplate -Path $Source -Destination $Destination -Target RuntimeBound
        }

        @($written | ForEach-Object { [System.IO.Path]::GetFileName($_) }) | Should -Be @('handler.ps1')
        Join-Path $destination 'lambdaps-runtime.json' | Should -Not -Exist
    }

    It 'throws PwshVersionRequired for a runtime-carrying target without the versions' {
        InModuleScope $script:moduleName -Parameters @{ Destination = (Join-Path $TestDrive 'noversion') } {
            param($Destination)
            { Copy-LambdaPSProjectTemplate -Path $Destination -Destination $Destination -Target Layer } |
                Should -Throw -ErrorId 'PwshVersionRequired'
        }
    }

    # The kernel runs the bootstrap and the image entrypoint directly, so a project zipped or built by
    # hand needs them executable.
    It 'writes the <Target> <File> executable, even from a template that is not' -Skip:$IsWindows -ForEach @(
        @{ Target = 'CMR'; File = 'bootstrap' }, @{ Target = 'Layer'; File = 'bootstrap' }, @{ Target = 'CMR'; File = 'lambda-entrypoint.sh' }
    ) {
        $source = Join-Path $TestDrive "mode-src-$Target-$File"
        $null = New-Item -ItemType Directory -Path $source -Force
        Set-Content -LiteralPath (Join-Path $source $File) -Value '#!/bin/sh'
        [System.IO.File]::SetUnixFileMode((Join-Path $source $File), [System.IO.UnixFileMode]'UserRead, UserWrite, GroupRead, OtherRead')
        $destination = Join-Path $TestDrive "mode-out-$Target-$File"
        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination; Target = $Target } {
            param($Source, $Destination, $Target)
            $null = Copy-LambdaPSProjectTemplate -Path $Source -Destination $Destination -Target $Target -PowerShellVersion '7.4' -PwshVersion '7.4.20'
        }
        (Get-Item -LiteralPath (Join-Path $destination $File)).UnixFileMode |
            Should -Be ([System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute, GroupRead, GroupExecute, OtherRead, OtherExecute')
    }
}

Describe 'Scaffolded README' -Tag 'Unit' {
    It 'describes the <Target> project and how to build and deploy it' -ForEach @(
        @{ Target = 'CMR'; Expect = @('customer-managed runtime image', 'Build-LambdaPSRuntimeImage', 'New-LambdaPSRuntimeDeployment'); NotExpect = @('Build-LambdaPSRuntimePackage') }
        @{ Target = 'Layer'; Expect = @('runtime layer', 'Build-LambdaPSRuntimePackage', 'Publish-LMLayerVersion'); NotExpect = @('Dockerfile', 'New-LMCRuntime') }
        @{ Target = 'SelfContainedZip'; Expect = @('self-contained function', 'Build-LambdaPSRuntimePackage', 'Publish-LMFunction'); NotExpect = @('Dockerfile', 'New-LMCRuntime') }
        @{ Target = 'Container'; Expect = @('container-image function', 'Build-LambdaPSRuntimeImage', '-PackageType Image'); NotExpect = @('New-LMCRuntime') }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "readme-$Target")
        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Path = $projectDir } {
            param($Target, $Path)
            $copyParams = @{
                Path              = Get-LambdaPSProjectTemplateRoot -Target $Target
                Destination       = $Path
                PwshVersion       = '7.4.20'
                Target            = $Target
                PowerShellVersion = '7.4'
            }
            $null = Copy-LambdaPSProjectTemplate @copyParams -WarningAction SilentlyContinue
        }
        $readme = Get-Content -LiteralPath ([System.IO.Path]::Combine($projectDir, 'README.md')) -Raw

        foreach ($text in @($Expect) + 'POWERSHELL_RUNTIME_JSON_DEPTH', 'POWERSHELL_RUNTIME_VERBOSE') { $readme | Should -BeLike "*$text*" }
        foreach ($text in $NotExpect) { $readme | Should -Not -BeLike "*$text*" }
        $readme | Should -Not -BeLike '*makes no AWS API calls*'
    }

    It 'gives the <Target> README''s <Step> step everything it needs' -ForEach @(
        @{ Target = 'Layer'; Step = 'function'; Expect = @('Compress-LambdaPSFunctionPackage -Path <folder>', 'Publish-LMFunction -FunctionName <name>', '-Role <exec-role-arn>', '-Architecture <the architecture you built>', '-ZipFilename ../function.zip') }
        @{ Target = 'CMR'; Step = 'pull grant'; Expect = @('Get-ECRRepositoryPolicy', 'Write the whole policy back with `Set-ECRRepositoryPolicy`', 'removes every other grant') }
        @{ Target = 'CMR'; Step = 'docker login'; Expect = @('--password-stdin', 'without `https://`', 'credential helper') }
    ) {
        $projectDir = [System.IO.Path]::Combine($TestDrive, "readme-step-$Target-$($Step -replace '\W', '-')")
        InModuleScope $script:moduleName -Parameters @{ Target = $Target; Path = $projectDir } {
            param($Target, $Path)
            $null = Copy-LambdaPSProjectTemplate -Path (Get-LambdaPSProjectTemplateRoot -Target $Target) -Destination $Path -PwshVersion '7.4.20' -Target $Target -PowerShellVersion '7.4' -WarningAction SilentlyContinue
        }
        # Lines are wrapped, so compare with the line breaks folded to spaces.
        $readme = (Get-Content -LiteralPath ([System.IO.Path]::Combine($projectDir, 'README.md')) -Raw) -replace '\s+', ' '
        foreach ($text in $Expect) { $readme | Should -BeLike "*$([System.Management.Automation.WildcardPattern]::Escape($text))*" }
    }
}

Describe 'Copy-LambdaPSProjectTemplate parameter validation' -Tag 'Unit' {
    It 'rejects -<Parameter> with a trailing newline' -ForEach @(
        @{ Parameter = 'PowerShellVersion'; Arguments = @{ PowerShellVersion = "7.4`n" } }
        @{ Parameter = 'HandlerScript'; Arguments = @{ PowerShellVersion = '7.4'; HandlerScript = "fn.ps1`n" } }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Arguments = $Arguments; Destination = (Join-Path $TestDrive "newline-$Parameter") } {
            param($Arguments, $Destination)
            { Copy-LambdaPSProjectTemplate -Path (Get-LambdaPSProjectTemplateRoot) -Destination $Destination -PwshVersion '7.4.20' -Target CMR @Arguments } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Copy-LambdaPSProjectTemplate'
        }
    }

    It 'rejects a -HandlerScript with a Kelvin sign, matching case-sensitively' {
        InModuleScope $script:moduleName -Parameters @{ Destination = (Join-Path $TestDrive 'kelvin-HandlerScript') } {
            param($Destination)
            { Copy-LambdaPSProjectTemplate -Path (Get-LambdaPSProjectTemplateRoot -Target Container) -Destination $Destination -PwshVersion '7.4.20' -Target Container -PowerShellVersion '7.4' -HandlerScript "$([char]0x212A)fn.ps1" } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Copy-LambdaPSProjectTemplate'
        }
    }

    It 'rejects a -PwshVersion that is not x.y.z' {
        InModuleScope $script:moduleName -Parameters @{ Destination = (Join-Path $TestDrive 'bad-PwshVersion') } {
            param($Destination)
            # Layer carries no Dockerfile, so no other step reads the version first.
            { Copy-LambdaPSProjectTemplate -Path (Get-LambdaPSProjectTemplateRoot -Target Layer) -Destination $Destination -PwshVersion '7.4.x/../../evil' -Target Layer -PowerShellVersion '7.4' } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Copy-LambdaPSProjectTemplate'
        }
    }

    It 'rejects a -PowerShellVersion in non-ASCII digits' {
        InModuleScope $script:moduleName -Parameters @{ Destination = (Join-Path $TestDrive 'digits-PowerShellVersion') } {
            param($Destination)
            { Copy-LambdaPSProjectTemplate -Path (Get-LambdaPSProjectTemplateRoot) -Destination $Destination -PwshVersion '7.4.20' -Target CMR -PowerShellVersion "$([char]0x0667).$([char]0x0664)" } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Copy-LambdaPSProjectTemplate'
        }
    }
}

# The public cmdlets call this inside try and rethrow with their own $PSCmdlet; the error they
# surface, and every record -ErrorVariable collects, must name the cmdlet, not this helper.
Describe 'Copy-LambdaPSProjectTemplate raised through a calling cmdlet' -Tag 'Unit' {
    It 'surfaces <ErrorId> from the calling cmdlet, and names no internal helper in -ErrorVariable' -ForEach @(
        @{ ErrorId = 'PwshVersionRequired' }
    ) {
        $destination = [System.IO.Path]::Combine($TestDrive, 'caller-noversion')
        $Call = { Copy-LambdaPSProjectTemplate -Path $Destination -Destination $Destination -Target Layer }
        InModuleScope $script:moduleName -Parameters @{ ErrorId = $ErrorId; Call = $Call.ToString(); Destination = $destination } {
            param($ErrorId, $Call, $Destination)
            # Rebuilt here, so the call runs in the module and reaches its private helper.
            $Call = [scriptblock]::Create($Call)
            function Invoke-LambdaPSTestCaller {
                [CmdletBinding()]
                param([scriptblock]$Call)
                try { $null = & $Call }
                catch { $PSCmdlet.ThrowTerminatingError($_) }
            }
            $collected = $null
            $err = $null
            try { Invoke-LambdaPSTestCaller -Call $Call -ErrorVariable collected } catch { $err = $_ }

            $err.FullyQualifiedErrorId | Should -Be "$ErrorId,Invoke-LambdaPSTestCaller"
            $ids = @($collected | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.FullyQualifiedErrorId } else { $_.ErrorRecord.FullyQualifiedErrorId } })
            $ids | Should -Contain "$ErrorId,Invoke-LambdaPSTestCaller"
            @($ids | Where-Object { $_ -like '*,Copy-LambdaPSProjectTemplate' }) | Should -BeNullOrEmpty
        }
    }
}
