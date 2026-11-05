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

        It 'refuses a character class rather than match it differently from docker' {
            $root = & $script:newContextTree -Root ([System.IO.Path]::Combine($TestDrive, 'class')) -File 'a.txt' -Ignore '[ab].txt'

            { Select-LambdaPSBuildContextFile -Path $root } | Should -Throw -ErrorId 'DockerIgnorePatternUnsupported'
        }
    }
}
