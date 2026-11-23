# Relative paths must resolve against the PowerShell location (Set-Location), not the process working
# directory, which Set-Location does not change. Each test moves the PowerShell location somewhere the
# process directory is not, so a helper that resolves against the process directory acts on the wrong
# folder and the test fails.
BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeModule.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Zip.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Relative paths' -Tag 'Component' {
    BeforeEach {
        $script:work = [System.IO.Path]::Combine($TestDrive, [guid]::NewGuid().ToString('N'))
        $null = New-Item -ItemType Directory -Path $script:work -Force
        [System.IO.Directory]::GetCurrentDirectory() | Should -Not -Be $script:work
        Push-Location -LiteralPath $script:work
    }

    AfterEach {
        Pop-Location
    }

    It 'scaffolds -Force into the folder it emptied' {
        $null = New-Item -ItemType Directory -Path ./rt
        Set-Content -LiteralPath ./rt/old.txt -Value 'old'

        $null = New-LambdaPSRuntimeProject -Path ./rt -Target Layer -PowerShellVersion '7.4.*' -Force

        Test-Path -LiteralPath ./rt/old.txt | Should -BeFalse
        Test-Path -LiteralPath ./rt/lambdaps-runtime.json -PathType Leaf | Should -BeTrue
        Test-LambdaPSRuntimeProject -Path ./rt -Quiet | Should -BeTrue
    }

    It 'writes a relative -OutFile under the current location' {
        Set-Content -LiteralPath ./handler.ps1 -Value 'param($LambdaInput, $LambdaContext)'

        $package = Compress-LambdaPSFunctionPackage -Path ./handler.ps1 -OutFile ./out/fn.zip

        $package.PackagePath | Should -Be ([System.IO.Path]::Combine($script:work, 'out', 'fn.zip'))
        Test-Path -LiteralPath ./out/fn.zip -PathType Leaf | Should -BeTrue
    }

    It 'builds a package from a relative -Path to a relative -OutFile' {
        $null = New-LambdaPSFunctionProject -Path ./scz -Handler fn -Target SelfContainedZip -PowerShellVersion '7.4.*'
        $fakeSource = [System.IO.Path]::Combine($TestDrive, 'rel-pwsh-src')
        $null = New-Item -ItemType Directory -Path $fakeSource -Force
        # Stands in for the bundled Linux pwsh by forwarding to the host's, so the context precompile works.
        Set-Content -LiteralPath ([System.IO.Path]::Combine($fakeSource, 'pwsh')) -Value "#!/bin/sh`nexec pwsh `"`$@`""
        $tarball = [System.IO.Path]::Combine($TestDrive, 'rel-pwsh.tar.gz')
        # The tar the build runs. On Windows the tar on PATH can be Git's GNU tar, which reads C: as a host.
        $tar = InModuleScope $script:moduleName { Get-LambdaPSTarCommand }
        & $tar -czf $tarball -C $fakeSource .
        # The stand-in is not the official tarball, so no pinned hash applies to it here.
        Mock 'Get-LambdaPSPwshArchiveHash' -ModuleName $script:moduleName -MockWith { }

        $package = Build-LambdaPSRuntimePackage -Path ./scz -OutFile ./out/scz.zip -PwshArchivePath $tarball -WarningAction SilentlyContinue

        $package.PackagePath | Should -Be ([System.IO.Path]::Combine($script:work, 'out', 'scz.zip'))
        $names = Get-ZipEntryNames -ZipPath $package.PackagePath
        $names | Should -Contain 'fn.ps1'
    }

    It 'saves modules into a relative -Path' {
        $null = New-FakeModule -Root ([System.IO.Path]::Combine($TestDrive, 'rel-mods')) -Name 'LambdaPSFake.Rel' -Version '1.0.0'
        $env:PSModulePath = [System.IO.Path]::Combine($TestDrive, 'rel-mods') + [System.IO.Path]::PathSeparator + $env:PSModulePath
        try {
            $saved = Save-LambdaPSModule -Module @{ ModuleName = 'LambdaPSFake.Rel'; RequiredVersion = '1.0.0' } -Path ./mods
        }
        finally { $env:PSModulePath = ($env:PSModulePath -split [System.IO.Path]::PathSeparator | Select-Object -Skip 1) -join [System.IO.Path]::PathSeparator }

        $saved.Path | Should -Be ([System.IO.Path]::Combine($script:work, 'mods', 'LambdaPSFake.Rel', '1.0.0'))
        Test-Path -LiteralPath ./mods/LambdaPSFake.Rel/1.0.0/LambdaPSFake.Rel.psd1 -PathType Leaf | Should -BeTrue
    }

    It 'validates a relative -Path piped in' {
        $null = New-LambdaPSRuntimeProject -Path ./pipe-rt -Target Layer -PowerShellVersion '7.4.*'

        './pipe-rt' | Test-LambdaPSRuntimeProject -Quiet | Should -BeTrue
    }
}
