BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Split-LambdaPSFunctionArn' -Tag 'Unit' {
    It 'returns name <Name>, region <Region> and qualifier <Qualifier> for <Value>' -ForEach @(
        @{ Value = 'arn:aws:lambda:eu-west-1:111122223333:function:hello'; Name = 'hello'; Region = 'eu-west-1'; Qualifier = $null }
        @{ Value = 'arn:aws:lambda:eu-west-1:111122223333:function:hello:live'; Name = 'hello'; Region = 'eu-west-1'; Qualifier = 'live' }
        @{ Value = 'arn:aws-cn:lambda:cn-north-1:111122223333:function:hello'; Name = 'hello'; Region = 'cn-north-1'; Qualifier = $null }
        @{ Value = 'arn:aws-us-gov:lambda:us-gov-west-1:111122223333:function:hello:$LATEST'; Name = 'hello'; Region = 'us-gov-west-1'; Qualifier = '$LATEST' }
        @{ Value = '111122223333:function:hello'; Name = 'hello'; Region = $null; Qualifier = $null }
        @{ Value = '111122223333:function:hello:live'; Name = 'hello'; Region = $null; Qualifier = 'live' }
        @{ Value = 'function:hello'; Name = 'hello'; Region = $null; Qualifier = $null }
        @{ Value = 'eu-west-1:111122223333:function:hello:live'; Name = 'hello'; Region = $null; Qualifier = 'live' }
        @{ Value = 'hello'; Name = 'hello'; Region = $null; Qualifier = $null }
        @{ Value = 'hello:prod'; Name = 'hello'; Region = $null; Qualifier = 'prod' }
        @{ Value = 'arn-hello'; Name = 'arn-hello'; Region = $null; Qualifier = $null }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Value = $Value; Name = $Name; Region = $Region; Qualifier = $Qualifier } {
            param($Value, $Name, $Region, $Qualifier)
            $parts = Split-LambdaPSFunctionArn -FunctionName $Value
            $parts.Name | Should -Be $Name
            $parts.Region | Should -Be $Region
            $parts.Qualifier | Should -Be $Qualifier
        }
    }

    It 'reads the region of an ARN whose region is in capitals, as it is written' {
        InModuleScope $script:moduleName {
            $parts = Split-LambdaPSFunctionArn -FunctionName 'arn:aws:lambda:EU-WEST-1:111122223333:function:hello'
            $parts.Name | Should -BeExactly 'hello'
            $parts.Region | Should -BeExactly 'EU-WEST-1'
        }
    }

    It 'treats a value whose account has a non-ASCII digit as a plain name' {
        InModuleScope $script:moduleName {
            $value = "arn:aws:lambda:eu-west-1:11112222333$([char]0x0663):function:hello"
            $parts = Split-LambdaPSFunctionArn -FunctionName $value
            $parts.Name | Should -Be $value
            $parts.Region | Should -BeNullOrEmpty
        }
    }
}
