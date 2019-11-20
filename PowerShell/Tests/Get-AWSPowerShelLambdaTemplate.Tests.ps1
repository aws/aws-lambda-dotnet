$module = 'AWSLambdaPSCore'
$moduleManifestPath = [System.IO.Path]::Combine('..', 'Module', "$module.psd1")

if (Get-Module -Name $module) {Remove-Module -Name $module}
Import-Module $moduleManifestPath

InModuleScope -ModuleName $module -ScriptBlock {
    Describe -Name 'Get-AWSPowerShellLambdaTemplate' -Fixture {

        function LoadFakeData
        {
            ConvertTo-Json -InputObject @{
                manifestVersion = 1
                blueprints      = @(
                    @{
                        name        = 'Basic'
                        description = 'Bare bones script'
                        content     = @(
                            @{
                                source   = 'basic.ps1.txt'
                                output   = '{basename}.ps1'
                                filetype = 'lambdaFunction'
                            },
                            @{
                                source = 'readme.txt'
                                output = 'readme.txt'
                            }
                        )
                    }
                )
            }
        }
        Mock -CommandName '_getHostedBlueprintsContent' -MockWith {LoadFakeData}
        Mock -CommandName '_getLocalBlueprintsContent' -MockWith {LoadFakeData}

        Context -Name 'Online Templates' -Fixture {
            It -Name 'Retrieves Blueprints from online sources by default' -Test {
                $null = Get-AWSPowerShellLambdaTemplate
                Assert-MockCalled -CommandName '_getHostedBlueprintsContent' -Times 1 -Scope 'It'
                Assert-MockCalled -CommandName '_getLocalBlueprintsContent' -Times 0 -Scope 'It'
            }
        }

        Context -Name 'Offline Templates' -Fixture {
            It -Name 'Retrieves Blueprints from local source when InstalledOnly is specified' -Test {
                $null = Get-AWSPowerShellLambdaTemplate -InstalledOnly
                Assert-MockCalled -CommandName '_getHostedBlueprintsContent' -Times 0 -Scope 'It'
                Assert-MockCalled -CommandName '_getLocalBlueprintsContent' -Times 1 -Scope 'It'
            }
        }
    } # End Describe
} # End InModuleScope