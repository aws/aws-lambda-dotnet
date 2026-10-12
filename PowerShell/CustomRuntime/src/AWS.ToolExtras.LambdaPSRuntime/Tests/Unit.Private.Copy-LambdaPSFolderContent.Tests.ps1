BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Copy-LambdaPSFolderContent' -Tag 'Unit' {
    BeforeEach {
        $script:source = Join-Path $TestDrive "src-$([guid]::NewGuid().ToString('N'))"
        $script:destination = Join-Path $TestDrive "dst-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:source, 'sub', '.git')) -Force
        $null = New-Item -ItemType Directory -Path $script:destination -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, 'a.ps1')) -Value '# a'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, '.hidden')) -Value 'x'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, 'sub', 'b.ps1')) -Value '# b'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($script:source, 'sub', '.git', 'config')) -Value 'token'
    }

    It 'copies hidden files and subfolders, and leaves out .git at any depth and -ExcludePath' {
        $skip = [System.IO.Path]::Combine($script:source, 'a.ps1')
        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination; Skip = $skip } {
            param($Source, $Destination, $Skip)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -ExcludePath $Skip.ToUpperInvariant()
        }

        @(Get-ChildItem -LiteralPath $script:destination -Recurse -Force -File | ForEach-Object { $_.FullName.Substring($script:destination.Length + 1) }) |
            Sort-Object | Should -Be @('.hidden', [System.IO.Path]::Combine('sub', 'b.ps1'))
    }

    It 'copies a link inside the folder as its content, and skips links that leave it or contain themselves' -Skip:$IsWindows {
        $outside = Join-Path $TestDrive "outside-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType Directory -Path $outside -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($outside, 'credentials')) -Value 'SECRET'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'inside.ps1')) -Target ([System.IO.Path]::Combine($script:source, 'a.ps1'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'creds')) -Target ([System.IO.Path]::Combine($outside, 'credentials'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'sub', 'loop')) -Target $script:source

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings).Count | Should -Be 2
            ($warnings -join ' ') | Should -BeLike "*creds*outside*"
            ($warnings -join ' ') | Should -BeLike "*loop*a folder it was reached through*"
        }

        $inside = Get-Item -LiteralPath ([System.IO.Path]::Combine($script:destination, 'inside.ps1'))
        $inside.LinkType | Should -BeNullOrEmpty
        Get-Content -LiteralPath $inside.FullName | Should -Be '# a'
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'creds')) | Should -BeFalse
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'sub', 'loop')) | Should -BeFalse
    }

    It 'treats a -Path reached through a link like the folder itself' -Skip:$IsWindows {
        # A module folder linked into the PSModulePath: the inner link points at the real folder.
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'inside.ps1')) -Target ([System.IO.Path]::Combine($script:source, 'a.ps1'))
        $alias = Join-Path $TestDrive "alias-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType SymbolicLink -Path $alias -Target $script:source
        $exclude = [System.IO.Path]::Combine($alias, '.hidden')

        InModuleScope $script:moduleName -Parameters @{ Source = $alias; Destination = $script:destination; Exclude = $exclude } {
            param($Source, $Destination, $Exclude)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -ExcludePath $Exclude -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings).Count | Should -Be 0
        }

        Get-Content -LiteralPath ([System.IO.Path]::Combine($script:destination, 'inside.ps1')) | Should -Be '# a'
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, '.hidden')) | Should -BeFalse
    }

    It 'skips, with a warning, two folder links that point at each other' -Skip:$IsWindows {
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'loopA')) -Target ([System.IO.Path]::Combine($script:source, 'loopB'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'loopB')) -Target ([System.IO.Path]::Combine($script:source, 'loopA'))

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            # Resolving either link fails with too many levels of links; both are skipped with a warning.
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings | Where-Object { "$_" -like '*cannot be resolved*' }).Count | Should -Be 2
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'a.ps1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'loopA')) | Should -BeFalse
    }

    It 'skips a folder link back to a folder it was reached through another link' -Skip:$IsWindows {
        # sub/up points at sub; 'via' points at sub too, so via/up must be seen as containing itself.
        $sub = [System.IO.Path]::Combine($script:source, 'sub')
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($sub, 'up')) -Target $sub
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'via')) -Target $sub

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings | Where-Object { "$_" -like '*a folder it was reached through*' }).Count | Should -Be 2
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'via', 'b.ps1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'via', 'up')) | Should -BeFalse
    }

    It 'decides a link by its final target when the link text climbs out of another link with ..' -Skip:$IsWindows {
        # awsdir points outside; awsdir/../.aws/credentials resolves through it to the outside secret,
        # not to <source>/.aws/credentials as joining the text would suggest.
        $outside = Join-Path $TestDrive "home-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($outside, '.aws')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($outside, '.aws', 'credentials')) -Value 'SECRET'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'awsdir')) -Target ([System.IO.Path]::Combine($outside, '.aws'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'b.txt')) -Target 'awsdir/../.aws/credentials'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'c.txt')) -Target 'sub/../a.ps1'

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            ($warnings -join ' ') | Should -BeLike '*b.txt*outside*'
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'b.txt')) | Should -BeFalse
        Get-Content -LiteralPath ([System.IO.Path]::Combine($script:destination, 'c.txt')) | Should -Be '# a' -Because 'a .. that stays inside the folder is fine'
    }

    It 'leaves out a link that resolves to a .git folder or into one' -Skip:$IsWindows {
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'g')) -Target ([System.IO.Path]::Combine('sub', '.git'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'cfg')) -Target ([System.IO.Path]::Combine('sub', '.git', 'config'))

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningAction SilentlyContinue
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'g')) | Should -BeFalse
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'cfg')) | Should -BeFalse
        @(Get-ChildItem -LiteralPath $script:destination -Recurse -Force -File | Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match 'token' }) |
            Should -BeNullOrEmpty
    }

    It 'skips, with a warning, a chain of links that leads back to its start' -Skip:$IsWindows {
        # a -> b -> c -> a: no single pair points at each other, but the chain never ends.
        foreach ($pair in @(@('chainA', 'chainB'), @('chainB', 'chainC'), @('chainC', 'chainA'))) {
            $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, $pair[0])) -Target $pair[1]
        }

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings | Where-Object { "$_" -like '*cannot be resolved*' }).Count | Should -Be 3
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'a.ps1')) | Should -BeTrue
    }

    It 'skips, with a warning, two folders that link to each other' -Skip:$IsWindows {
        $a = [System.IO.Path]::Combine($script:source, 'a')
        $b = [System.IO.Path]::Combine($script:source, 'b')
        $null = New-Item -ItemType Directory -Path $a, $b -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($a, 'in-a.txt')) -Value 'a'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($a, 'toB')) -Target '../b'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($b, 'toA')) -Target '../a'

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            # Guard: a copy that runs away fails fast here instead of filling the disk.
            Mock 'New-Item' -ParameterFilter { $Path -like '*toB*toA*toB*' } -MockWith { throw 'the copy ran away' }
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings | Where-Object { "$_" -like '*a folder it was reached through*' }).Count | Should -Be 2
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'a', 'toB', 'toA')) | Should -BeFalse
        Get-Content -LiteralPath ([System.IO.Path]::Combine($script:destination, 'b', 'toA', 'in-a.txt')) | Should -Be 'a'
    }

    It 'treats a folder whose name differs from -Path only in case as outside it' -Skip:(-not $IsLinux) {
        $twin = [System.IO.Path]::Combine((Split-Path -Parent $script:source), (Split-Path -Leaf $script:source).ToUpperInvariant())
        $null = New-Item -ItemType Directory -Path $twin -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($twin, 'secret.txt')) -Value 'SECRET'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'twin')) -Target $twin

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            ($warnings -join ' ') | Should -BeLike '*twin*outside*'
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'twin')) | Should -BeFalse
    }

    It 'leaves out a link to an excluded path, and keeps what an excluded link points to' -Skip:$IsWindows {
        $build = [System.IO.Path]::Combine($script:source, 'build')
        $null = New-Item -ItemType Directory -Path $build -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($build, 'pkg.zip')) -Value 'old package'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'alias')) -Target 'build'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'latest.zip')) -Target 'build/pkg.zip'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'alias.ps1')) -Target 'a.ps1'
        $exclude = @($build, [System.IO.Path]::Combine($script:source, 'alias.ps1'))

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination; Exclude = $exclude } {
            param($Source, $Destination, $Exclude)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -ExcludePath $Exclude -WarningAction SilentlyContinue
        }

        foreach ($name in 'build', 'alias', 'latest.zip', 'alias.ps1') {
            Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, $name)) | Should -BeFalse -Because "$name is, or links to, an excluded path"
        }
        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'a.ps1')) | Should -BeTrue -Because 'excluding a link does not exclude its target'
    }

    It 'skips, with a warning, a link to something that does not exist' -Skip:$IsWindows {
        # An editor lock file is a link to a name that is not a file.
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, '.#a.ps1')) -Target 'user@host.12345:1700000000'

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            ($warnings -join ' ') | Should -BeLike '*.#a.ps1*does not exist*'
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($script:destination, 'a.ps1')) | Should -BeTrue
    }

    It 'names a skipped cross-link by the path it was reached through' -Skip:$IsWindows {
        $a = [System.IO.Path]::Combine($script:source, 'a')
        $b = [System.IO.Path]::Combine($script:source, 'b')
        $null = New-Item -ItemType Directory -Path $a, $b -Force
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($a, 'toB')) -Target '../b'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($b, 'toA')) -Target '../a'
        $reached = [System.IO.Path]::Combine((Resolve-Path -LiteralPath $script:source).ProviderPath, 'a', 'toB', 'toA')

        $crossWarnings = InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings | ForEach-Object { "$_" })
        }

        $crossWarnings | Should -Contain "Skipped '$reached': it links to '$((Resolve-Path -LiteralPath $a).ProviderPath)', a folder it was reached through."
    }

    It 'names a skipped link from -Path as given when -Path is reached through a link' -Skip:$IsWindows {
        $a = [System.IO.Path]::Combine($script:source, 'a')
        $b = [System.IO.Path]::Combine($script:source, 'b')
        $null = New-Item -ItemType Directory -Path $a, $b -Force
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($a, 'toB')) -Target '../b'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($b, 'toA')) -Target '../a'
        $alias = Join-Path $TestDrive "alias-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType SymbolicLink -Path $alias -Target $script:source

        $aliasWarnings = InModuleScope $script:moduleName -Parameters @{ Source = $alias; Destination = $script:destination } {
            param($Source, $Destination)
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings | ForEach-Object { "$_" })
        }

        $aliasWarnings | Should -Contain "Skipped '$([System.IO.Path]::Combine($alias, 'a', 'toB', 'toA'))': it links to '$((Resolve-Path -LiteralPath $a).ProviderPath)', a folder it was reached through."
    }

    It 'stops at a folder it may not read' -Skip:($IsWindows -or [Environment]::UserName -eq 'root') {
        $locked = [System.IO.Path]::Combine($script:source, 'locked')
        $null = New-Item -ItemType Directory -Path $locked -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($locked, 'inside.txt')) -Value 'x'
        try {
            & chmod 000 $locked
            InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
                param($Source, $Destination)
                { Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningAction SilentlyContinue } | Should -Throw '*denied*'
            }
        }
        finally {
            & chmod 755 $locked
        }
    }

    It 'stops at a link into a folder it may not read, rather than calling it missing' -Skip:($IsWindows -or [Environment]::UserName -eq 'root') {
        $locked = [System.IO.Path]::Combine($script:source, 'locked')
        $null = New-Item -ItemType Directory -Path $locked -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($locked, 'secret.txt')) -Value 'x'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'link.txt')) -Target 'locked/secret.txt'
        # Only the link is listed: the locked folder itself is excluded, so the link decides the outcome.
        try {
            & chmod 000 $locked
            InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination; Locked = $locked } {
                param($Source, $Destination, $Locked)
                { Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -ExcludePath $Locked -WarningVariable warnings -WarningAction SilentlyContinue } |
                    Should -Throw '*denied*'
            }
        }
        finally {
            & chmod 755 $locked
        }
    }

    It 'copies a file link from the real path it checked' -Skip:$IsWindows {
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:source, 'alias.ps1')) -Target 'sub/b.ps1'
        $real = [System.IO.Path]::Combine((Resolve-Path -LiteralPath $script:source).ProviderPath, 'sub', 'b.ps1')

        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination; Real = $real } {
            param($Source, $Destination, $Real)
            Mock 'Copy-Item'
            Copy-LambdaPSFolderContent -Path $Source -Destination $Destination -WarningAction SilentlyContinue
            Should -Invoke 'Copy-Item' -Times 1 -Exactly -ParameterFilter { $Destination -like '*alias.ps1' -and $LiteralPath -eq $Real }
        }
    }

    It 'stops at a file it cannot copy' {
        InModuleScope $script:moduleName -Parameters @{ Source = $script:source; Destination = $script:destination } {
            param($Source, $Destination)
            Mock 'Copy-Item' -MockWith { throw [System.IO.IOException]::new('disk full') }
            { Copy-LambdaPSFolderContent -Path $Source -Destination $Destination } | Should -Throw '*disk full*'
        }
    }
}
