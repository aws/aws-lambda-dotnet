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
    Describe 'Get-LambdaPSIcuPackage' -Tag 'Unit' {
        BeforeAll {
            $script:pinned = $script:Config.pwsh.Icu.Sha256.Clone()
            $script:content = [System.Text.Encoding]::UTF8.GetBytes('icu')
            $script:contentHash = [System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($script:content))
        }

        BeforeEach {
            Mock Invoke-WebRequest { $script:outFile = $OutFile; [System.IO.File]::WriteAllBytes($OutFile, $script:content) }
        }

        AfterEach {
            $script:Config.pwsh.Icu.Sha256 = $script:pinned.Clone()
        }

        It 'downloads the pinned <Architecture> package and returns it when its SHA256 matches' -ForEach @(
            @{ Architecture = 'x86_64'; Rid = 'x64' }
            @{ Architecture = 'arm64'; Rid = 'arm64' }
        ) {
            $script:Config.pwsh.Icu.Sha256[$Rid] = $script:contentHash

            $path = Get-LambdaPSIcuPackage -Architecture $Architecture

            [System.IO.File]::ReadAllBytes($path) | Should -Be $script:content
            Remove-Item -LiteralPath $path
            Should -Invoke Invoke-WebRequest -Times 1 -Exactly -ParameterFilter {
                $Uri -ceq "https://api.nuget.org/v3-flatcontainer/microsoft.icu.icu4c.runtime.linux-$Rid/72.1.0.3/microsoft.icu.icu4c.runtime.linux-$Rid.72.1.0.3.nupkg"
            }
        }

        It 'refuses a package that does not match the pinned SHA256 and removes the download' {
            { Get-LambdaPSIcuPackage -Architecture x86_64 } | Should -Throw -ErrorId 'IcuPackageHashMismatch'

            [System.IO.File]::Exists($script:outFile) | Should -BeFalse
        }
    }
}
