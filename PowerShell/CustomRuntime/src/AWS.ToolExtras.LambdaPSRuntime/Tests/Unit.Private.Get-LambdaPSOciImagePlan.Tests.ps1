# InModuleScope wraps the tests at discovery, so the module is imported then too.
BeforeDiscovery {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' {
    Describe 'Get-LambdaPSOciImagePlan' -Tag 'Unit' {
        BeforeAll {
            $script:runtimePath = [System.IO.Path]::Combine($TestDrive, 'rt')
            $null = New-LambdaPSRuntimeProject -Path $script:runtimePath -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            $script:functionPath = [System.IO.Path]::Combine($TestDrive, 'fn')
            $null = New-LambdaPSFunctionProject -Path $script:functionPath -Handler 'process' -Target Container -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($script:functionPath, 'helper.ps1'), '1')
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:functionPath, '.git')) -Force
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($script:functionPath, '.git', 'HEAD'), 'ref')
        }

        It 'describes a scaffolded runtime image: base, entrypoint, no CMD, app-local ICU and the runtime files' {
            $plan = Get-LambdaPSOciImagePlan -Path $script:runtimePath -Architecture arm64 -OnWindows $false

            $plan.Target | Should -Be 'CMR'
            $plan.Platform | Should -Be 'linux/arm64'
            $plan.PwshVersion | Should -Match '^7\.4\.\d+$'
            $plan.BaseImage | Should -Be 'public.ecr.aws/lambda/provided:al2023'
            $plan.Entrypoint | Should -Be @('/var/runtime/lambda-entrypoint.sh')
            $plan.Cmd.Count | Should -Be 0
            $plan.Environment['DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU'] | Should -Be $script:Config.pwsh.Icu.Version
            $plan.Environment.Contains('DOTNET_SYSTEM_GLOBALIZATION_INVARIANT') | Should -BeFalse
            $plan.FunctionFiles.Count | Should -Be 0
            ($plan.RuntimeFiles | Where-Object Executable).ContainerPath | Should -Be @('/var/runtime/lambda-entrypoint.sh', '/var/runtime/bootstrap')
            ($plan.RuntimeFiles | Where-Object Normalize).Count | Should -Be 2
            $plan.RuntimeFiles.ContainerPath | Should -Contain '/var/runtime/runtime.ps1'
            $plan.RuntimeFiles.ContainerPath | Should -Contain '/var/runtime/PowerShellLambdaContext.cs'
            $plan.RuntimeFiles.ContainerPath | Should -Contain '/var/runtime/modules/pwsh-runtime.psm1'
        }

        It 'leaves out of modules/ what .dockerignore excludes, such as a module''s .git folder' {
            $root = [System.IO.Path]::Combine($TestDrive, 'rt-git')
            $null = New-LambdaPSRuntimeProject -Path $root -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($root, 'modules', 'MyModule', '.git')) -Force
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($root, 'modules', 'MyModule', '.git', 'HEAD'), 'ref')
            [System.IO.File]::WriteAllText([System.IO.Path]::Combine($root, 'modules', 'MyModule', 'MyModule.psm1'), '1')
            [System.IO.File]::WriteAllLines([System.IO.Path]::Combine($root, '.dockerignore'), [string[]]@('**/.git'))
            $paths = (Get-LambdaPSOciImagePlan -Path $root -Architecture x86_64).RuntimeFiles.ContainerPath
            $paths | Should -Contain '/var/runtime/modules/MyModule/MyModule.psm1'
            @($paths -like '*/.git/*') | Should -HaveCount 0
        }

        It 'refuses a project whose .dockerignore excludes modules/ with ModulesFolderIgnored, as docker''s COPY modules fails' {
            $root = [System.IO.Path]::Combine($TestDrive, 'rt-nomodules')
            $null = New-LambdaPSRuntimeProject -Path $root -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            [System.IO.File]::WriteAllLines([System.IO.Path]::Combine($root, '.dockerignore'), [string[]]@('modules'))
            { Get-LambdaPSOciImagePlan -Path $root -Architecture x86_64 } | Should -Throw -ErrorId 'ModulesFolderIgnored'
        }

        # Docker's COPY modules fails on a 'Modules' folder on every OS, and copies an empty one.
        It 'refuses a project whose modules folder is named in another case with ProjectFileNotFound, naming it' {
            $root = [System.IO.Path]::Combine($TestDrive, 'rt-case')
            $null = New-LambdaPSRuntimeProject -Path $root -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            # Two moves, so a case-insensitive file system renames it too.
            Move-Item -LiteralPath ([System.IO.Path]::Combine($root, 'modules')) -Destination ([System.IO.Path]::Combine($root, 'm-tmp'))
            Move-Item -LiteralPath ([System.IO.Path]::Combine($root, 'm-tmp')) -Destination ([System.IO.Path]::Combine($root, 'Modules'))
            $err = { Get-LambdaPSOciImagePlan -Path $root -Architecture x86_64 } | Should -Throw -ErrorId 'ProjectFileNotFound' -PassThru
            $err.Exception.Message | Should -BeLike "*has no modules folder (it has 'Modules'*"
        }

        It 'builds a project with an empty modules folder and no .dockerignore' {
            $root = [System.IO.Path]::Combine($TestDrive, 'rt-empty')
            $null = New-LambdaPSRuntimeProject -Path $root -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            Remove-Item -LiteralPath ([System.IO.Path]::Combine($root, '.dockerignore')) -Force
            Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($root, 'modules')) -Force | Remove-Item -Recurse -Force
            $paths = @((Get-LambdaPSOciImagePlan -Path $root -Architecture x86_64).RuntimeFiles.ContainerPath)
            $paths | Should -Contain '/var/runtime/runtime.ps1'
            @($paths -like '/var/runtime/modules/*') | Should -HaveCount 0
        }

        It 'accepts the scaffolded Dockerfile with CRLF line endings and a BOM (an edit on Windows that changes nothing)' {
            $copy = [System.IO.Path]::Combine($TestDrive, 'rt-crlf')
            Copy-Item -LiteralPath $script:runtimePath -Destination $copy -Recurse
            $dockerfile = [System.IO.Path]::Combine($copy, 'Dockerfile')
            [System.IO.File]::WriteAllText($dockerfile, [System.IO.File]::ReadAllText($dockerfile).Replace("`n", "`r`n"), [System.Text.UTF8Encoding]::new($true))

            (Get-LambdaPSOciImagePlan -Path $copy -Architecture x86_64).Platform | Should -Be 'linux/amd64'
        }

        It 'accepts a Container project whose CMD was changed to another script, as its Dockerfile says to' {
            $root = [System.IO.Path]::Combine($TestDrive, 'fn-cmd')
            $null = New-LambdaPSFunctionProject -Path $root -Handler 'handler' -Target Container -PowerShellVersion '7.4' -WarningAction SilentlyContinue
            Copy-Item -LiteralPath ([System.IO.Path]::Combine($root, 'handler.ps1')) -Destination ([System.IO.Path]::Combine($root, 'other.ps1'))
            $dockerfile = [System.IO.Path]::Combine($root, 'Dockerfile')
            [System.IO.File]::WriteAllText($dockerfile, [System.IO.File]::ReadAllText($dockerfile).Replace('CMD [ "handler.ps1" ]', 'CMD [ "other.ps1" ]'))
            (Get-LambdaPSOciImagePlan -Path $root -Architecture x86_64).Cmd | Should -Be @('other.ps1')
        }

        It 'refuses an edited Dockerfile with DockerfileModified, which names -BuildMethod Docker' {
            $copy = [System.IO.Path]::Combine($TestDrive, 'rt-edited')
            Copy-Item -LiteralPath $script:runtimePath -Destination $copy -Recurse
            Add-Content -LiteralPath ([System.IO.Path]::Combine($copy, 'Dockerfile')) -Value 'RUN dnf install -y jq'

            { Get-LambdaPSOciImagePlan -Path $copy -Architecture x86_64 } | Should -Throw -ErrorId 'DockerfileModified' -ExpectedMessage '*-BuildMethod Docker*'
        }

        It 'refuses an x86_64 build of a pwsh release with no pinned x64 hash with PwshX64HashNotPinned' {
            $unpinned = [System.IO.Path]::Combine($TestDrive, 'rt-unpinned')
            $null = New-LambdaPSRuntimeProject -Path $unpinned -PowerShellVersion '7.4' -PwshVersion '7.4.99' -WarningAction SilentlyContinue

            { Get-LambdaPSOciImagePlan -Path $unpinned -Architecture x86_64 } |
                Should -Throw -ErrorId 'PwshX64HashNotPinned' -ExpectedMessage '*pwsh 7.4.99 has no pinned SHA256*(7.4.*-BuildMethod Docker*'
        }

        It 'refuses a project with no marker' {
            $copy = [System.IO.Path]::Combine($TestDrive, 'rt-nomarker')
            Copy-Item -LiteralPath $script:runtimePath -Destination $copy -Recurse
            Remove-Item -LiteralPath ([System.IO.Path]::Combine($copy, 'lambdaps-runtime.json'))

            { Get-LambdaPSOciImagePlan -Path $copy -Architecture x86_64 } | Should -Throw -ErrorId 'DockerfileModified'
        }

        It 'adds a Container project''s files under /var/task, less .dockerignore and the runtime files, with its CMD' {
            $plan = Get-LambdaPSOciImagePlan -Path $script:functionPath -Architecture x86_64

            $plan.Target | Should -Be 'Container'
            $plan.Cmd | Should -Be @('process.ps1')
            $plan.FunctionFiles.ContainerPath | Should -Be @('/var/task/helper.ps1', '/var/task/process.ps1')
        }

        It 'marks every file executable on Windows, as docker build from Windows adds files as 0755' {
            $plan = Get-LambdaPSOciImagePlan -Path $script:functionPath -Architecture x86_64 -OnWindows $true

            @($plan.RuntimeFiles + $plan.FunctionFiles | Where-Object { -not $_.Executable }).Count | Should -Be 0
            ($plan.RuntimeFiles | Where-Object Normalize).Count | Should -Be 2
        }
    }
}
