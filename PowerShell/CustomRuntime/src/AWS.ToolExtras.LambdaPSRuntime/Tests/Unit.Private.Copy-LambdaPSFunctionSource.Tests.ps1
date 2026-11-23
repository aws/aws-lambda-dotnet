BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# The copy itself is Copy-LambdaPSFolderContent's (tested with it); this layer adds the top-level .zip warning.
Describe 'Copy-LambdaPSFunctionSource' -Tag 'Unit' {
    BeforeEach {
        $script:source = [System.IO.Path]::Combine($TestDrive, "src-$([guid]::NewGuid().ToString('N'))")
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:source, 'lib')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, 'handler.ps1')) -Value 'param($LambdaInput, $LambdaContext)'
        $script:stage = [System.IO.Path]::Combine($TestDrive, "stage-$([guid]::NewGuid().ToString('N'))")
        $null = New-Item -ItemType Directory -Path $script:stage
    }

    It 'warns about a .zip at the top of the folder, whatever its case, and still copies it' {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, 'OLD.ZIP')) -Value 'x'

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Stage = $script:stage } {
            param($Source, $Stage)
            Copy-LambdaPSFunctionSource -Path $Source -Destination $Stage -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings).Count | Should -Be 1
            "$($warnings[0])" | Should -BeLike '*OLD.ZIP*ships inside the package*'
        }
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:stage, 'OLD.ZIP')) | Should -BeTrue
    }

    It 'does not warn about a .zip in a subfolder' {
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, 'lib', 'data.zip')) -Value 'x'

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Stage = $script:stage } {
            param($Source, $Stage)
            Copy-LambdaPSFunctionSource -Path $Source -Destination $Stage -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings) | Should -BeNullOrEmpty
        }
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:stage, 'lib', 'data.zip')) | Should -BeTrue
    }

    It 'neither copies nor warns about an excluded .zip, such as the package being written' {
        $package = [System.IO.Path]::Combine($script:source, 'fn.zip')
        Set-Content -LiteralPath $package -Value 'x'

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Stage = $script:stage; Package = $package } {
            param($Source, $Stage, $Package)
            Copy-LambdaPSFunctionSource -Path $Source -Destination $Stage -ExcludePath $Package -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings) | Should -BeNullOrEmpty
        }
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:stage, 'fn.zip')) | Should -BeFalse
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:stage, 'handler.ps1')) | Should -BeTrue
    }
}
