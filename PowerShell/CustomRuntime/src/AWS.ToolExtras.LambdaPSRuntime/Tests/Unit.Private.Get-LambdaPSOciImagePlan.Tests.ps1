BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    $script:runtimePath = [System.IO.Path]::Combine($TestDrive, 'rt')
    $null = New-LambdaPSRuntimeProject -Path $script:runtimePath -PowerShellVersion '7.4' -WarningAction SilentlyContinue
    $script:functionPath = [System.IO.Path]::Combine($TestDrive, 'fn')
    $null = New-LambdaPSFunctionProject -Path $script:functionPath -Handler 'process' -Target Container -PowerShellVersion '7.4' -WarningAction SilentlyContinue
    [System.IO.File]::WriteAllText([System.IO.Path]::Combine($script:functionPath, 'helper.ps1'), '1')
    $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:functionPath, '.git')) -Force
    [System.IO.File]::WriteAllText([System.IO.Path]::Combine($script:functionPath, '.git', 'HEAD'), 'ref')
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSOciImagePlan' -Tag 'Unit' {
    It 'describes a scaffolded runtime image: base, entrypoint, no CMD, invariant globalization and the runtime files' {
        InModuleScope $script:moduleName -Parameters @{ Root = $script:runtimePath } {
            param($Root)
            $plan = Get-LambdaPSOciImagePlan -Path $Root -Architecture arm64

            $plan.Target | Should -Be 'CMR'
            $plan.Platform | Should -Be 'linux/arm64'
            $plan.PwshVersion | Should -Match '^7\.4\.\d+$'
            $plan.BaseImage | Should -Be 'public.ecr.aws/lambda/provided:al2023'
            $plan.Entrypoint | Should -Be @('/var/runtime/lambda-entrypoint.sh')
            $plan.Cmd.Count | Should -Be 0
            $plan.Environment['DOTNET_SYSTEM_GLOBALIZATION_INVARIANT'] | Should -Be '1'
            $plan.FunctionFiles.Count | Should -Be 0
            ($plan.RuntimeFiles | Where-Object Executable).ContainerPath | Should -Be @('/var/runtime/lambda-entrypoint.sh', '/var/runtime/bootstrap')
            ($plan.RuntimeFiles | Where-Object Normalize).Count | Should -Be 2
            $plan.RuntimeFiles.ContainerPath | Should -Contain '/var/runtime/runtime.ps1'
            $plan.RuntimeFiles.ContainerPath | Should -Contain '/var/runtime/PowerShellLambdaContext.cs'
            $plan.RuntimeFiles.ContainerPath | Should -Contain '/var/runtime/modules/pwsh-runtime.psm1'
        }
    }

    It 'accepts the scaffolded Dockerfile with CRLF line endings and a BOM (an edit on Windows that changes nothing)' {
        $copy = [System.IO.Path]::Combine($TestDrive, 'rt-crlf')
        Copy-Item -LiteralPath $script:runtimePath -Destination $copy -Recurse
        $dockerfile = [System.IO.Path]::Combine($copy, 'Dockerfile')
        [System.IO.File]::WriteAllText($dockerfile, [System.IO.File]::ReadAllText($dockerfile).Replace("`n", "`r`n"), [System.Text.UTF8Encoding]::new($true))
        InModuleScope $script:moduleName -Parameters @{ Root = $copy } {
            param($Root)
            (Get-LambdaPSOciImagePlan -Path $Root -Architecture x86_64).Platform | Should -Be 'linux/amd64'
        }
    }

    It 'refuses an edited Dockerfile with DockerfileModified, which names -BuildMethod Docker' {
        $copy = [System.IO.Path]::Combine($TestDrive, 'rt-edited')
        Copy-Item -LiteralPath $script:runtimePath -Destination $copy -Recurse
        Add-Content -LiteralPath ([System.IO.Path]::Combine($copy, 'Dockerfile')) -Value 'RUN dnf install -y jq'
        InModuleScope $script:moduleName -Parameters @{ Root = $copy } {
            param($Root)
            { Get-LambdaPSOciImagePlan -Path $Root -Architecture x86_64 } | Should -Throw -ErrorId 'DockerfileModified' -ExpectedMessage '*-BuildMethod Docker*'
        }
    }

    It 'refuses a project with no marker' {
        $copy = [System.IO.Path]::Combine($TestDrive, 'rt-nomarker')
        Copy-Item -LiteralPath $script:runtimePath -Destination $copy -Recurse
        Remove-Item -LiteralPath ([System.IO.Path]::Combine($copy, 'lambdaps-runtime.json'))
        InModuleScope $script:moduleName -Parameters @{ Root = $copy } {
            param($Root)
            { Get-LambdaPSOciImagePlan -Path $Root -Architecture x86_64 } | Should -Throw -ErrorId 'DockerfileModified'
        }
    }

    It 'adds a Container project''s files under /var/task, less .dockerignore and the runtime files, with its CMD' {
        InModuleScope $script:moduleName -Parameters @{ Root = $script:functionPath } {
            param($Root)
            $plan = Get-LambdaPSOciImagePlan -Path $Root -Architecture x86_64

            $plan.Target | Should -Be 'Container'
            $plan.Cmd | Should -Be @('process.ps1')
            $plan.FunctionFiles.ContainerPath | Should -Be @('/var/task/helper.ps1', '/var/task/process.ps1')
        }
    }
}
