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
    # A fake docker script stands in for the CLI; Windows can't run one without an extension.
    Describe 'Get-LambdaPSDockerBuilderPlatform' -Tag 'Unit' -Skip:$IsWindows {
        BeforeAll {
            $script:newDocker = {
                param([string]$Output, [int]$ExitCode = 0)
                $path = [System.IO.Path]::Combine($TestDrive, 'docker')
                Set-Content -LiteralPath $path -Value "#!/bin/sh`ncat <<'OUT'`n$Output`nOUT`nexit $ExitCode"
                chmod +x $path
                $path
            }
        }

        It 'Should return every node''s platforms without the user-set marker or the variant count' {
            $docker = & $script:newDocker ("Name: multi`nNodes:`nName: n1`nPlatforms: linux/amd64*, linux/386`n" +
                "Name: n2`nPlatforms: linux/arm64 (+3)")
            $platforms = Get-LambdaPSDockerBuilderPlatform -Path $docker
            $platforms | Should -Be @('linux/amd64', 'linux/386', 'linux/arm64')
        }

        It 'Should return nothing when <Case>' -ForEach @(
            @{ Case = 'the builder is not running and lists no platforms'; Output = "Name: c`nDriver: docker-container`nStatus: inactive"; ExitCode = 0 }
            @{ Case = 'docker buildx inspect fails'; Output = 'ERROR: no builder found'; ExitCode = 1 }
        ) {
            $docker = & $script:newDocker $Output $ExitCode
            Get-LambdaPSDockerBuilderPlatform -Path $docker | Should -BeNullOrEmpty
        }

        It 'Should return nothing when the docker executable is gone' {
            Get-LambdaPSDockerBuilderPlatform -Path ([System.IO.Path]::Combine($TestDrive, 'missing', 'docker')) | Should -BeNullOrEmpty
        }
    }
}
