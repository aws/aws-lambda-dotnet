BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Split-LambdaPSFunctionArn' -Tag 'Unit' {
    It 'returns name <Name> and region <Region> for <Value>' -ForEach @(
        @{ Value = 'arn:aws:lambda:eu-west-1:111122223333:function:hello'; Name = 'hello'; Region = 'eu-west-1' }
        @{ Value = 'arn:aws:lambda:eu-west-1:111122223333:function:hello:live'; Name = 'hello'; Region = 'eu-west-1' }
        @{ Value = 'arn:aws-cn:lambda:cn-north-1:111122223333:function:hello'; Name = 'hello'; Region = 'cn-north-1' }
        @{ Value = 'arn:aws-us-gov:lambda:us-gov-west-1:111122223333:function:hello:$LATEST'; Name = 'hello'; Region = 'us-gov-west-1' }
        @{ Value = '111122223333:function:hello'; Name = 'hello'; Region = $null }
        @{ Value = '111122223333:function:hello:live'; Name = 'hello'; Region = $null }
        @{ Value = 'hello'; Name = 'hello'; Region = $null }
        @{ Value = 'arn-hello'; Name = 'arn-hello'; Region = $null }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Value = $Value; Name = $Name; Region = $Region } {
            param($Value, $Name, $Region)
            $parts = Split-LambdaPSFunctionArn -FunctionName $Value
            $parts.Name | Should -Be $Name
            $parts.Region | Should -Be $Region
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
