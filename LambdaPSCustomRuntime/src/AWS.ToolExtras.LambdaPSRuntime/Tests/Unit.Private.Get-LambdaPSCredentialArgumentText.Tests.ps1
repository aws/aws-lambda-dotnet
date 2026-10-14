BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Get-LambdaPSCredentialArgumentText' -Tag 'Unit' {
    It 'returns <Expected> for <Case>' -ForEach @(
        @{ Case = 'no credential parameters'; Parameter = @{}; Expected = '' }
        @{ Case = 'a null hashtable'; Parameter = $null; Expected = '' }
        @{ Case = 'a profile and its file'; Parameter = [ordered]@{ ProfileName = 'prod'; ProfileLocation = '/creds' }; Expected = " -ProfileName 'prod' -ProfileLocation '/creds'" }
        @{ Case = 'a profile name with a quote'; Parameter = @{ ProfileName = "o'brien" }; Expected = " -ProfileName 'o''brien'" }
        @{ Case = 'a profile name with a typographic quote'; Parameter = @{ ProfileName = "o$([char]0x2019)brien" }; Expected = " -ProfileName 'o$([char]0x2019)$([char]0x2019)brien'" }
        @{ Case = 'a credential object'; Parameter = @{ Credential = [object]::new() }; Expected = ' -Credential <the credentials you deployed with>' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Parameter = [hashtable]$Parameter; Expected = $Expected } {
            param($Parameter, $Expected)
            Get-LambdaPSCredentialArgumentText -CredentialParameter $Parameter | Should -BeExactly $Expected
        }
    }

    It 'names Set-AWSCredential for credentials the caller''s script set rather than passed' {
        InModuleScope $script:moduleName {
            $parameter = @{ Credential = [object]::new() }
            Add-Member -InputObject $parameter -NotePropertyName 'LambdaPSCredentialSource' -NotePropertyValue 'SetAWSCredential'
            Get-LambdaPSCredentialArgumentText -CredentialParameter $parameter |
                Should -BeExactly ' -Credential <the credentials your script set with Set-AWSCredential>'
        }
    }
}
