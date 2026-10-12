BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSRealPath' -Tag 'Unit' {
    BeforeEach {
        $script:base = (Resolve-Path -LiteralPath $TestDrive).ProviderPath
        $script:case = [System.IO.Path]::Combine($script:base, "case-$([guid]::NewGuid().ToString('N'))")
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:case, 'home', '.aws')) -Force
        $null = New-Item -ItemType Directory -Path ([System.IO.Path]::Combine($script:case, 'proj', 'sub')) -Force
    }

    It 'returns a path with no links unchanged, without a trailing separator' {
        $plain = [System.IO.Path]::Combine($script:case, 'proj', 'sub')
        InModuleScope $script:moduleName -Parameters @{ Plain = $plain } {
            param($Plain)
            Get-LambdaPSRealPath -Path ($Plain + [System.IO.Path]::DirectorySeparatorChar) | Should -Be $Plain
        }
    }

    It 'keeps components that do not exist' {
        $missing = [System.IO.Path]::Combine($script:case, 'proj', 'not-there', 'file.txt')
        InModuleScope $script:moduleName -Parameters @{ Missing = $missing } {
            param($Missing)
            Get-LambdaPSRealPath -Path $Missing | Should -Be $Missing
        }
    }

    It 'applies .. after a link to where the link points, not to the link''s folder' -Skip:$IsWindows {
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:case, 'proj', 'awsdir')) -Target ([System.IO.Path]::Combine($script:case, 'home', '.aws'))
        $through = [System.IO.Path]::Combine($script:case, 'proj', 'awsdir', '..', '.aws', 'credentials')
        InModuleScope $script:moduleName -Parameters @{ Through = $through; Case = $script:case } {
            param($Through, $Case)
            Get-LambdaPSRealPath -Path $Through | Should -Be ([System.IO.Path]::Combine($Case, 'home', '.aws', 'credentials'))
        }
    }

    It 'resolves a chain of links, relative and absolute, in every segment' -Skip:$IsWindows {
        # proj/hop -> sub (relative); sub/out -> <case>/home (absolute); so proj/hop/out/.aws is home/.aws.
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:case, 'proj', 'hop')) -Target 'sub'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:case, 'proj', 'sub', 'out')) -Target ([System.IO.Path]::Combine($script:case, 'home'))
        $chained = [System.IO.Path]::Combine($script:case, 'proj', 'hop', 'out', '.aws')
        InModuleScope $script:moduleName -Parameters @{ Chained = $chained; Case = $script:case } {
            param($Chained, $Case)
            Get-LambdaPSRealPath -Path $Chained | Should -Be ([System.IO.Path]::Combine($Case, 'home', '.aws'))
        }
    }

    It 'throws an IOException for links that point at each other' -Skip:$IsWindows {
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:case, 'proj', 'loopA')) -Target 'loopB'
        $null = New-Item -ItemType SymbolicLink -Path ([System.IO.Path]::Combine($script:case, 'proj', 'loopB')) -Target 'loopA'
        $loop = [System.IO.Path]::Combine($script:case, 'proj', 'loopA')
        InModuleScope $script:moduleName -Parameters @{ Loop = $loop } {
            param($Loop)
            $err = { Get-LambdaPSRealPath -Path $Loop } | Should -Throw -PassThru
            $err.Exception | Should -BeOfType [System.IO.IOException]
        }
    }

    It 'makes a relative path full against the current directory' {
        InModuleScope $script:moduleName -Parameters @{ Case = $script:case } {
            param($Case)
            $saved = [System.Environment]::CurrentDirectory
            try {
                [System.Environment]::CurrentDirectory = $Case
                Get-LambdaPSRealPath -Path ([System.IO.Path]::Combine('proj', 'sub')) | Should -Be ([System.IO.Path]::Combine($Case, 'proj', 'sub'))
            }
            finally {
                [System.Environment]::CurrentDirectory = $saved
            }
        }
    }
}
