BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# Resolve-LambdaPSHandlerScript is the single answer to "which .ps1 is the entry point", shared by the
# packager and the deployer so they cannot disagree (a fixed 'script.ps1' default, say, would miss the
# '<Handler>.ps1' the scaffolder emits).
Describe 'Resolve-LambdaPSHandlerScript' -Tag 'Unit' {
    It 'returns an explicit -HandlerScript verbatim without touching the source' {
        InModuleScope $script:moduleName {
            Resolve-LambdaPSHandlerScript -Path '/does/not/exist' -HandlerScript 'given.ps1' |
                Should -Be 'given.ps1'
        }
    }

    It 'returns $null for a path that does not exist' {
        InModuleScope $script:moduleName {
            Resolve-LambdaPSHandlerScript -Path '/does/not/exist' | Should -BeNullOrEmpty
        }
    }

    # Get-Item does not see a name that starts with '.' on Linux and macOS without -Force.
    It 'resolves a source whose name starts with a dot: <Case>' -ForEach @(
        @{ Case = 'a folder'; Folder = $true }, @{ Case = 'a single file'; Folder = $false }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Folder = $Folder } {
            param($Folder)
            if ($Folder) {
                $source = [System.IO.Path]::Combine($TestDrive, '.dot-folder')
                $null = New-Item -ItemType Directory -Path $source -Force
                Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'only.ps1')) -Value '# handler'
                Resolve-LambdaPSHandlerScript -Path $source -ErrorAction Stop | Should -Be 'only.ps1'
            }
            else {
                $source = [System.IO.Path]::Combine($TestDrive, '.dot-file.ps1')
                Set-Content -LiteralPath $source -Value '# handler'
                Resolve-LambdaPSHandlerScript -Path $source -ErrorAction Stop | Should -Be '.dot-file.ps1'
            }
        }
    }

    It 'returns the file name for a single-file source' {
        InModuleScope $script:moduleName {
            $file = [System.IO.Path]::Combine($TestDrive, 'single.ps1')
            Set-Content -LiteralPath $file -Value '# handler'
            Resolve-LambdaPSHandlerScript -Path $file | Should -Be ([System.IO.Path]::GetFileName($file))
        }
    }

    # A malformed manifest.json stops resolution rather than guess past the handler it may record.
    It 'throws InvalidProjectManifest for an unparseable manifest.json, even with a sole root script' {
        InModuleScope $script:moduleName {
            $dir = [System.IO.Path]::Combine($TestDrive, 'unparseable-manifest')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value '{ not json'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'only.ps1')) -Value '# handler'
            { Resolve-LambdaPSHandlerScript -Path $dir } | Should -Throw -ErrorId 'InvalidProjectManifest*'
        }
    }

    It 'ignores a manifest.json with no ScriptFile and falls back to the sole root script' {
        InModuleScope $script:moduleName {
            $dir = [System.IO.Path]::Combine($TestDrive, 'manifest-without-scriptfile')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value '{"Other":"value"}'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'only.ps1')) -Value '# handler'
            Resolve-LambdaPSHandlerScript -Path $dir | Should -Be 'only.ps1'
        }
    }

    It 'returns the manifest''s ScriptFile over the other root scripts' {
        InModuleScope $script:moduleName {
            $dir = [System.IO.Path]::Combine($TestDrive, 'manifest-wins')
            $null = New-LambdaPSFunctionProject -Path $dir -Handler 'b'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'a.ps1')) -Value '# helper'
            Resolve-LambdaPSHandlerScript -Path $dir | Should -Be 'b.ps1'
        }
    }

    It 'returns $null for several root scripts and no manifest, rather than guess' {
        InModuleScope $script:moduleName {
            $dir = [System.IO.Path]::Combine($TestDrive, 'ambiguous')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'a.ps1')) -Value '# handler'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'b.ps1')) -Value '# handler'
            Resolve-LambdaPSHandlerScript -Path $dir | Should -BeNullOrEmpty
        }
    }

    It 'falls back to the sole root script when the scaffolder''s manifest has an empty ScriptFile' {
        InModuleScope $script:moduleName {
            $dir = [System.IO.Path]::Combine($TestDrive, 'empty-scriptfile')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'manifest.json')) -Value '{"HandlerString":"x","ScriptFile":""}'
            Set-Content -LiteralPath ([System.IO.Path]::Combine($dir, 'only.ps1')) -Value '# handler'
            Resolve-LambdaPSHandlerScript -Path $dir | Should -Be 'only.ps1'
        }
    }

    It 'returns $null for a folder with no root script at all' {
        InModuleScope $script:moduleName {
            $dir = [System.IO.Path]::Combine($TestDrive, 'no-root-script')
            $null = New-Item -ItemType Directory -Path $dir -Force
            Resolve-LambdaPSHandlerScript -Path $dir | Should -BeNullOrEmpty
        }
    }
}
