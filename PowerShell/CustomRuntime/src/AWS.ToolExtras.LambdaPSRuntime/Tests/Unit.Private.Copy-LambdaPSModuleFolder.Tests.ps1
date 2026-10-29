BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Copy-LambdaPSModuleFolder' -Tag 'Unit' {
    It 'copies every file, including from a source path with wildcard characters' {
        $source = [System.IO.Path]::Combine($TestDrive, 'mods[1]', 'Fake', '1.0.0')
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($source, 'Private')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'Fake.psd1')) -Value '@{}'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'Private', 'a.ps1')) -Value '# a'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, '.hidden')) -Value 'x'
        $destination = [System.IO.Path]::Combine($TestDrive, 'out', 'Fake', '1.0.0')

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($destination, 'Fake.psd1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($destination, 'Private', 'a.ps1')) | Should -BeTrue
        Test-Path -LiteralPath ([System.IO.Path]::Combine($destination, '.hidden')) | Should -BeTrue
    }

    It 'leaves out .git and links that point outside the module folder' -Skip:$IsWindows {
        # A module installed from a checkout, with links to a key and a credentials folder elsewhere.
        $outside = Join-Path $TestDrive 'outside'
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($outside, 'secrets')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($outside, 'secrets', 'credentials')) -Value 'aws_secret_access_key=SECRET'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($outside, 'id_rsa')) -Value 'PRIVATE KEY'
        $source = [System.IO.Path]::Combine($TestDrive, 'checkout', 'Evil', '1.0.0')
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($source, '.git')) -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'Evil.psd1')) -Value '@{}'
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, '.git', 'config')) -Value 'url = https://user:TOKEN@example.com/repo.git'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($source, 'key.txt')) -Target ([System.IO.Path]::Combine($outside, 'id_rsa'))
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($source, 'aws')) -Target ([System.IO.Path]::Combine($outside, 'secrets'))
        $destination = [System.IO.Path]::Combine($TestDrive, 'staged', 'Evil', '1.0.0')

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination -WarningVariable warnings -WarningAction SilentlyContinue
            @($warnings).Count | Should -Be 2
        }

        @(Get-ChildItem -LiteralPath $destination -Recurse -Force).Name | Should -Be @('Evil.psd1')
    }

    It 'removes the folder it created when the copy fails, so no partial module version is left' {
        $source = [System.IO.Path]::Combine($TestDrive, 'fail-src', 'Fake', '1.0.0')
        $null = New-Item -ItemType Directory -Path $source -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'Fake.psd1')) -Value '@{}'
        $destination = [System.IO.Path]::Combine($TestDrive, 'fail-out', 'Fake', '1.0.0')

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            Mock 'Copy-Item' -MockWith { throw [System.IO.IOException]::new('disk full') }
            { Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination } | Should -Throw '*disk full*'
        }

        Test-Path -LiteralPath $destination | Should -BeFalse
    }

    It 'leaves no module version folder, and no partial copy, when the copy is stopped' {
        $source = [System.IO.Path]::Combine($TestDrive, 'stop-src', 'Big', '1.0.0')
        $null = New-Item -ItemType Directory -Path $source -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'Big.psd1')) -Value '@{}'
        foreach ($d in 1..30) {
            $dir = [System.IO.Directory]::CreateDirectory([System.IO.Path]::Combine($source, "d$d")).FullName
            foreach ($f in 1..100) { [System.IO.File]::WriteAllText([System.IO.Path]::Combine($dir, "f$f.txt"), 'x') }
        }
        $parent = [System.IO.Path]::Combine($TestDrive, 'stop-out', 'Big')
        $destination = [System.IO.Path]::Combine($parent, '1.0.0')

        # Ctrl+C stops the pipeline: catch blocks are skipped, finally blocks run.
        $ps = [PowerShell]::Create()
        try {
            $null = $ps.AddScript({
                    param($modulePath, $Source, $Destination)
                    Import-Module $modulePath
                    & (Get-Module AWS.ToolExtras.LambdaPSRuntime) { param($s, $d) Copy-LambdaPSModuleFolder -Path $s -Destination $d } $Source $Destination
                }).AddArgument($script:modulePath).AddArgument($source).AddArgument($destination)
            $async = $ps.BeginInvoke()
            $deadline = [DateTime]::UtcNow.AddSeconds(60)
            while (-not $async.IsCompleted -and [DateTime]::UtcNow -lt $deadline -and
                @(Get-ChildItem -LiteralPath $parent -Recurse -File -Force -ErrorAction SilentlyContinue).Count -lt 200) {
                Start-Sleep -Milliseconds 20
            }
            $async.IsCompleted | Should -BeFalse -Because 'the copy must still be running when it is stopped'
            $ps.Stop()
        }
        finally {
            $ps.Dispose()
        }

        Test-Path -LiteralPath $destination | Should -BeFalse
        @(Get-ChildItem -LiteralPath $parent -Force -ErrorAction SilentlyContinue).Count | Should -Be 0
    }

    Context 'when the finished copy cannot be renamed into place at once' {
        BeforeEach {
            $script:moveSource = [System.IO.Path]::Combine($TestDrive, "move-src-$([guid]::NewGuid().ToString('N'))", 'Fake', '1.0.0')
            $null = New-Item -ItemType Directory -Path $script:moveSource -Force
            Set-Content -LiteralPath ([System.IO.Path]::Combine($script:moveSource, 'Fake.psd1')) -Value '@{}'
            $script:moveParent = [System.IO.Path]::Combine($TestDrive, "move-out-$([guid]::NewGuid().ToString('N'))", 'Fake')
            $script:moveDestination = [System.IO.Path]::Combine($script:moveParent, '1.0.0')
        }

        It 'retries a move that Windows reports in use, and finishes it' {
            InModuleScope $script:moduleName -Parameters @{ Source = $script:moveSource; Destination = $script:moveDestination } {
                param($Source, $Destination)
                $script:moveAttempts = 0
                # An antivirus scanner holds the new folder for the first two attempts.
                Mock 'Move-Item' -MockWith {
                    $script:moveAttempts++
                    if ($script:moveAttempts -le 2) { throw [System.IO.IOException]::new('The process cannot access the file because it is being used by another process.') }
                    [System.IO.Directory]::Move($LiteralPath, $Destination)
                }
                Mock 'Start-Sleep'
                Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination
                Should -Invoke 'Move-Item' -Times 3 -Exactly
            }
            [System.IO.Path]::Combine($script:moveDestination, 'Fake.psd1') | Should -Exist
        }

        It 'fails as before when the folder stays in use, leaving no partial copy' {
            InModuleScope $script:moduleName -Parameters @{ Source = $script:moveSource; Destination = $script:moveDestination } {
                param($Source, $Destination)
                Mock 'Move-Item' -MockWith { throw [System.IO.IOException]::new('The process cannot access the file because it is being used by another process.') }
                Mock 'Start-Sleep'
                { Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination } | Should -Throw '*being used by another process*'
                Should -Invoke 'Move-Item' -Times 5 -Exactly
            }
            Test-Path -LiteralPath $script:moveDestination | Should -BeFalse
            @(Get-ChildItem -LiteralPath $script:moveParent -Force).Count | Should -Be 0
        }

        It 'does not retry an error that is not about the folder being in use' {
            InModuleScope $script:moduleName -Parameters @{ Source = $script:moveSource; Destination = $script:moveDestination } {
                param($Source, $Destination)
                Mock 'Move-Item' -MockWith { throw [System.ArgumentException]::new('bad path') }
                Mock 'Start-Sleep'
                { Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination } | Should -Throw '*bad path*'
                Should -Invoke 'Move-Item' -Times 1 -Exactly
            }
        }
    }

    It 'leaves a folder that already existed when the copy fails' {
        $source = [System.IO.Path]::Combine($TestDrive, 'keep-src', 'Fake', '1.0.0')
        $null = New-Item -ItemType Directory -Path $source -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($source, 'Fake.psd1')) -Value '@{}'
        $destination = [System.IO.Path]::Combine($TestDrive, 'keep-out', 'Fake', '1.0.0')
        $null = New-Item -ItemType Directory -Path $destination -Force
        Set-Content -LiteralPath ([System.IO.Path]::Combine($destination, 'marker.txt')) -Value 'original'

        InModuleScope $script:moduleName -Parameters @{ Source = $source; Destination = $destination } {
            param($Source, $Destination)
            Mock 'Copy-Item' -MockWith { throw [System.IO.IOException]::new('disk full') }
            { Copy-LambdaPSModuleFolder -Path $Source -Destination $Destination } | Should -Throw '*disk full*'
        }

        Test-Path -LiteralPath ([System.IO.Path]::Combine($destination, 'marker.txt')) | Should -BeTrue
    }
}
