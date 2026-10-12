BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSPullConditionMet' -Tag 'Unit' {
    It 'returns <Expected> for <Case>' -ForEach @(
        @{ Case = 'no Condition'; Json = $null; Expected = $true }
        @{ Case = 'the exact runtime and account'; Json = '{"StringEquals":{"aws:SourceAccount":"111122223333","aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-ps"}}'; Expected = $true }
        @{ Case = 'a wildcard under ArnLike, lower-case key'; Json = '{"ArnLike":{"aws:sourcearn":"arn:aws:lambda:*:111122223333:runtime/*"}}'; Expected = $true }
        @{ Case = 'an IfExists operator'; Json = '{"StringEqualsIfExists":{"aws:SourceAccount":"111122223333"}}'; Expected = $true }
        @{ Case = 'a list of values, one matching'; Json = '{"StringEquals":{"aws:SourceAccount":["999988887777","111122223333"]}}'; Expected = $true }
        @{ Case = 'another runtime'; Json = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/other"}}'; Expected = $false }
        @{ Case = 'a different case in the value'; Json = '{"StringEquals":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/MY-PS"}}'; Expected = $false }
        @{ Case = 'a negated operator'; Json = '{"ArnNotLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/other"}}'; Expected = $false }
        @{ Case = 'a key the pull does not carry'; Json = '{"StringEquals":{"aws:PrincipalOrgID":"o-abc123"}}'; Expected = $false }
        @{ Case = 'brackets, which IAM reads literally'; Json = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-p[s]"}}'; Expected = $false }
        @{ Case = 'a lone bracket'; Json = '{"StringLike":{"aws:SourceArn":"arn:aws:lambda:*:111122223333:runtime/my-ps["}}'; Expected = $false }
        @{ Case = 'a wildcard under StringLike'; Json = '{"StringLike":{"aws:SourceArn":"arn:aws:lambda:*:111122223333:runtime/*"}}'; Expected = $true }
        @{ Case = 'a ? wildcard under ArnLike'; Json = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-p?"}}'; Expected = $true }
        @{ Case = 'a wildcard under ArnEquals'; Json = '{"ArnEquals":{"aws:SourceArn":"arn:aws:lambda:us-east-1:*:runtime/my-ps"}}'; Expected = $true }
        @{ Case = 'an ArnLikeIfExists operator'; Json = '{"ArnLikeIfExists":{"aws:SourceArn":"arn:aws:lambda:*:*:runtime/my-ps"}}'; Expected = $true }
        # IAM matches an ARN per colon-delimited component, so a * does not span colons.
        @{ Case = 'an ArnLike * spanning components'; Json = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:*:runtime/my-ps"}}'; Expected = $false }
        @{ Case = 'an ArnLike pattern with fewer than six components'; Json = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:*"}}'; Expected = $false }
        @{ Case = 'an Arn operator on aws:SourceAccount'; Json = '{"ArnEquals":{"aws:SourceAccount":"1111*"}}'; Expected = $false }
        @{ Case = 'a set operator prefix'; Json = '{"ForAnyValue:StringEquals":{"aws:SourceAccount":"111122223333"}}'; Expected = $false }
    ) {
        $condition = if ($Json) { $Json | ConvertFrom-Json -AsHashtable } else { $null }
        InModuleScope $script:moduleName -Parameters @{ Condition = $condition; Expected = $Expected } {
            param($Condition, $Expected)
            Test-LambdaPSPullConditionMet -Condition $Condition -AccountId '111122223333' -RuntimeArn 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' |
                Should -Be $Expected
        }
    }
}
