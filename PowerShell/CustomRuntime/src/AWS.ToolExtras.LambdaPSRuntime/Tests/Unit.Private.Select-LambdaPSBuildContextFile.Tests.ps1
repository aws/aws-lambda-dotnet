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
    Describe 'Select-LambdaPSBuildContextFile' -Tag 'Unit' {
        BeforeAll {
            $script:newContextTree = {
                param([string]$Root, [string[]]$File, [string[]]$Ignore)
                foreach ($relative in $File) {
                    $path = [System.IO.Path]::Combine($Root, $relative)
                    $null = New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($path)) -Force
                    [System.IO.File]::WriteAllText($path, 'x')
                }
                if ($null -ne $Ignore) { [System.IO.File]::WriteAllLines([System.IO.Path]::Combine($Root, '.dockerignore'), $Ignore) }
                $Root
            }
        }

        It 'lists every file, hidden ones included, when there is no .dockerignore' {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'none')) -File 'handler.ps1', '.env', 'data/a.json'

            Select-LambdaPSBuildContextFile -Path $root | Should -Be @('.env', 'data/a.json', 'handler.ps1')
        }

        It 'applies the scaffolded patterns: a file, **/<name> at any depth and *.<ext>' {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'scaffold')) `
                -File 'handler.ps1', 'Dockerfile', 'README.md', '.git/HEAD', 'sub/.git/HEAD', 'sub/.gitignore', 'out.zip', 'sub/keep.ps1' `
                -Ignore '# comment', '', '.dockerignore', 'Dockerfile', 'README.md', '**/.git', '**/.gitignore', '**/*.zip'

            Select-LambdaPSBuildContextFile -Path $root | Should -Be @('handler.ps1', 'sub/keep.ps1')
        }

        # .dockerignore itself is part of the context, as with docker, unless a pattern names it.
        It 'excludes everything under a folder a pattern matches, and a later ! pattern includes a file again' {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'negate')) `
                -File 'secrets/a.txt', 'secrets/keep.txt', 'logs/x.log', 'logs/y.txt', 'handler.ps1' `
                -Ignore '/secrets', '!secrets/keep.txt', './logs/*.log'

            Select-LambdaPSBuildContextFile -Path $root | Should -Be @('.dockerignore', 'handler.ps1', 'logs/y.txt', 'secrets/keep.txt')
        }

        It 'lets * and ? stop at a folder separator' {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'star')) -File 'a.tmp', 'd/b.tmp', 'c1.txt' -Ignore '*.tmp', 'c?.txt'

            Select-LambdaPSBuildContextFile -Path $root | Should -Be @('.dockerignore', 'd/b.tmp')
        }

        It 'refuses <Pattern> rather than match it differently from docker' -ForEach @(
            @{ Pattern = '[ab].txt' }, @{ Pattern = 'foo\*' }, @{ Pattern = 'sub/../secret' }
        ) {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, "refused-$($Pattern.Length)")) -File 'a.txt' -Ignore $Pattern

            { Select-LambdaPSBuildContextFile -Path $root } | Should -Throw -ErrorId 'DockerIgnorePatternUnsupported'
        }

        It 'skips, with a warning, file and folder links that leave the context' -Skip:$IsWindows {
            $outside = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'outside')) -File 'secret.txt'
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'links')) -File 'handler.ps1'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($root, 'file-link')) -Target ([System.IO.Path]::Combine($outside, 'secret.txt'))
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($root, 'folder-link')) -Target $outside
            $files = Select-LambdaPSBuildContextFile -Path $root -WarningVariable warnings -WarningAction SilentlyContinue
            $files | Should -Be @('handler.ps1')
            @($warnings -like "*outside*") | Should -HaveCount 2
        }

        It 'walks a folder link inside the context, and leaves out a file link whose target is excluded' -Skip:$IsWindows {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'inner-links')) -File 'shared/lib.ps1', 'secrets/key.txt' -Ignore '.dockerignore', 'secrets'
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($root, 'lib')) -Target ([System.IO.Path]::Combine($root, 'shared'))
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($root, 'cfg.txt')) -Target ([System.IO.Path]::Combine($root, 'secrets', 'key.txt'))
            Select-LambdaPSBuildContextFile -Path $root | Should -Be @('lib/lib.ps1', 'shared/lib.ps1')
        }

        It 'excludes a folder named by a pattern with a trailing slash, as docker does' {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'slash')) -File 'handler.ps1', 'secrets/key.txt' -Ignore '.dockerignore', 'secrets/'

            Select-LambdaPSBuildContextFile -Path $root | Should -Be @('handler.ps1')
        }
    }
}
