BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Zip.ps1'))

    # A stand-in pwsh tarball, so the build runs offline: its 'pwsh' hands over to the host pwsh.
    $fakeSrc = [System.IO.Path]::Combine($TestDrive, 'fake-pwsh-src')
    $null = New-Item -ItemType Directory -Path $fakeSrc -Force
    Set-Content -LiteralPath ([System.IO.Path]::Combine($fakeSrc, 'pwsh')) -Value "#!/bin/sh`nexec '$([System.Environment]::ProcessPath)' `"`$@`""
    $script:fakeTarball = [System.IO.Path]::Combine($TestDrive, 'fake-pwsh.tar.gz')
    $tar = InModuleScope 'AWS.ToolExtras.LambdaPSRuntime' { Get-LambdaPSTarCommand }
    & $tar -czf $script:fakeTarball -C $fakeSrc .
    # The stand-in is not the official tarball, so no pinned hash applies to it here.
    Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName 'AWS.ToolExtras.LambdaPSRuntime' -MockWith { }
}

AfterAll {
    Remove-LambdaPSTestModule
}

# Save-LambdaPSModule, Build-LambdaPSRuntimePackage and Test-LambdaPSRuntimeProject together, with only the
# pwsh tarball's hash mocked: a module saved into a project's modules/ ships with the project.
Describe 'Module staging across commands' -Tag 'Component' {
    It 'Save-LambdaPSModule into a SelfContainedZip project''s modules/ ships with the next build, and the project still lints' {
        $installed = Join-Path $TestDrive 'installed-modules'
        $null = New-FakeModule -Root $installed -Name 'LambdaPSFake.Local' -Version '1.0.0'
        $proj = Join-Path $TestDrive 'scz'
        $null = New-LambdaPSFunctionProject -Path $proj -Handler 'handler' -Target 'SelfContainedZip' -PowerShellVersion '7.4'
        $saved = $env:PSModulePath
        try {
            $env:PSModulePath = $installed + [System.IO.Path]::PathSeparator + $env:PSModulePath
            $null = Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Local'; RequiredVersion = '1.0.0' } -Path (Join-Path $proj 'modules') -WarningAction SilentlyContinue
        }
        finally {
            $env:PSModulePath = $saved
        }

        $p = Build-LambdaPSRuntimePackage -Path $proj -OutFile (Join-Path $TestDrive 'scz.zip') -PwshArchivePath $script:fakeTarball -WarningAction SilentlyContinue

        $names = Get-ZipEntryNames -ZipPath $p.PackagePath
        $names | Should -Contain 'modules/LambdaPSFake.Local/1.0.0/LambdaPSFake.Local.psd1'
        Test-LambdaPSRuntimeProject -Path $proj -Quiet | Should -BeTrue
    }

    # AWS.Tools service modules ship cmdlet help, about half their size, which Lambda never reads.
    It 'stages an AWS.Tools module without *.xml and *.pdb, keeps *.ps1xml, and the staged copy imports; other modules stay whole' {
        $source = Join-Path $TestDrive 'help-symbols-src'
        $null = New-FakeModule -Root $source -Name 'AWS.Tools.LambdaPSFake' -Version '9.0.1' -WithHelpAndSymbols
        $other = New-FakeModule -Root $source -Name 'LambdaPSFake.Other' -Version '1.0.0' -WithHelpAndSymbols
        $modules = Join-Path $TestDrive 'help-symbols-modules'

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Modules = $modules } {
            param($Source, $Modules)
            $specs = @('AWS.Tools.LambdaPSFake', '9.0.1'), @('LambdaPSFake.Other', '1.0.0') | ForEach-Object { @{ ModuleName = $_[0]; RequiredVersion = $_[1] } }
            $null = Copy-LambdaPSModuleSet -Module $specs -Path $Modules -SearchPath $Source -Confirm:$false
        }

        $staged = [System.IO.Path]::Combine($modules, 'AWS.Tools.LambdaPSFake', '9.0.1')
        @(Get-ChildItem -LiteralPath $staged -File).Name | Sort-Object | Should -Be @('AWS.Tools.LambdaPSFake.Format.ps1xml', 'AWS.Tools.LambdaPSFake.psd1')
        @(Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($modules, 'LambdaPSFake.Other', '1.0.0')) -File).Name |
            Sort-Object | Should -Be (@(Get-ChildItem -LiteralPath $other -File).Name | Sort-Object)
        $command = "try { Import-Module '$([System.IO.Path]::Combine($staged, 'AWS.Tools.LambdaPSFake.psd1'))' -ErrorAction Stop; 'IMPORTED' } catch { 'FAILED: ' + `$_.Exception.Message }"
        & (Get-Process -Id $PID).Path -NoProfile -NonInteractive -Command $command 2>&1 | Out-String | Should -BeLike '*IMPORTED*'
    }
}
