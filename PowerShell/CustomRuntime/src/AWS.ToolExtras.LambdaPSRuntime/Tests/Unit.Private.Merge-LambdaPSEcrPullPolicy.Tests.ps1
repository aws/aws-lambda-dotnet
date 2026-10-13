BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Merge-LambdaPSEcrPullPolicy' -Tag 'Unit' {
    BeforeAll {
        # InModuleScope blocks see module scope, so the shared arguments live there.
        InModuleScope $script:moduleName {
            $script:scope = @{ AccountId = '111122223333'; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' }
        }
    }

    It 'returns a Lambda-only policy when there is no existing policy' {
        InModuleScope $script:moduleName {
            $p = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $null | ConvertFrom-Json
            @($p.Statement).Count | Should -Be 1
            $p.Statement[0].Principal.Service | Should -Be 'lambda.amazonaws.com'
            @($p.Statement[0].Action) | Should -Contain 'ecr:BatchGetImage'
            @($p.Statement[0].Action) | Should -Contain 'ecr:GetDownloadUrlForLayer'
        }
    }

    It 'returns $null when <Case>' -ForEach @(
        @{ Case = 'Lambda already has both actions'; Action = '["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"]'; Service = '"lambda.amazonaws.com"' }
        @{ Case = 'Lambda has ecr:*'; Action = '"ecr:*"'; Service = '"lambda.amazonaws.com"' }
        @{ Case = 'Lambda has *'; Action = '"*"'; Service = '"lambda.amazonaws.com"' }
        @{ Case = 'the Service principal is an array'; Action = '["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"]'; Service = '["ecs-tasks.amazonaws.com","lambda.amazonaws.com"]' }
    ) {
        $text = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":' + $Service + '},"Action":' + $Action + '}]}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text } {
            param($Text)
            # Open to any account, so it also warns (asserted below).
            Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text -WarningAction SilentlyContinue | Should -BeNullOrEmpty
        }
    }

    It 'warns that a Lambda grant open to any account lets every account pull (<Case>)' -ForEach @(
        @{ Case = 'no Condition'; Condition = '' }
        @{ Case = 'an empty Condition'; Condition = ',"Condition":{}' }
        @{ Case = 'a * account'; Condition = ',"Condition":{"StringLike":{"aws:SourceAccount":"*"}}' }
        @{ Case = 'a * account and ARN'; Condition = ',"Condition":{"StringLike":{"aws:SourceAccount":"*","aws:SourceArn":"*"}}' }
        @{ Case = 'an ARN pattern with any account'; Condition = ',"Condition":{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:*:*:runtime/*"}}' }
        @{ Case = 'an ARN pattern of wildcards'; Condition = ',"Condition":{"ArnLike":{"aws:SourceArn":"arn:*:*:*:*:*"}}' }
        @{ Case = 'a StringLike ARN prefix'; Condition = ',"Condition":{"StringLike":{"aws:SourceArn":"arn:*"}}' }
        @{ Case = 'an account list that includes *'; Condition = ',"Condition":{"StringLike":{"aws:SourceAccount":["111122223333","*"]}}' }
        @{ Case = 'a ? account pattern'; Condition = ',"Condition":{"StringLike":{"aws:SourceAccount":"????????????"}}' }
        # An ...IfExists operator is also met by a request that does not carry the key.
        @{ Case = 'an IfExists account'; Condition = ',"Condition":{"StringEqualsIfExists":{"aws:SourceAccount":"111122223333"}}' }
    ) {
        $text = '{"Version":"2012-10-17","Statement":[{"Sid":"Open","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*"' + $Condition + '}]}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text } {
            param($Text)
            Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text -WarningVariable warnings -WarningAction SilentlyContinue | Should -BeNullOrEmpty
            @($warnings).Count | Should -Be 1
            "$warnings" | Should -BeLike "*'Open' lets any account's Lambda functions pull its images. Scope it with aws:SourceAccount '111122223333' plus an aws:SourceArn of this runtime ('arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'), or 'arn:aws:lambda:us-east-1:111122223333:runtime/``*' with ArnLike to allow every runtime in the account."
        }
    }

    It 'does not warn about a Lambda grant scoped to <Case>' -ForEach @(
        @{ Case = 'this account'; Condition = '{"StringEquals":{"aws:SourceAccount":"111122223333"}}' }
        @{ Case = 'this runtime'; Condition = '{"StringEquals":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-ps"}}' }
        @{ Case = 'this account''s runtimes'; Condition = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:*:111122223333:runtime/*"}}' }
        @{ Case = 'this account, with any ARN'; Condition = '{"StringLike":{"aws:SourceAccount":"111122223333","aws:SourceArn":"*"}}' }
        @{ Case = 'this account, beside an IfExists key'; Condition = '{"StringEquals":{"aws:SourceAccount":"111122223333"},"StringEqualsIfExists":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-ps"}}' }
    ) {
        $text = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*","Condition":' + $Condition + '}]}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text } {
            param($Text)
            Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text -WarningVariable warnings -WarningAction SilentlyContinue | Should -BeNullOrEmpty
            $warnings | Should -BeNullOrEmpty
        }
    }

    It 'adds a statement when Lambda has only one of the two required actions' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2012-10-17","Statement":[{"Sid":"Partial","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:BatchGetImage"}]}'
            $sids = @((Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json).Statement.Sid)
            $sids | Should -Contain 'Partial'
            $sids | Should -Contain 'LambdaPSRuntimeImagePull'
        }
    }

    It 'does not treat a Deny for Lambda as a grant, and keeps the Deny' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2012-10-17","Statement":[{"Sid":"NoLambda","Effect":"Deny","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*"}]}'
            $p = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json
            @($p.Statement.Sid) | Should -Be @('NoLambda', 'LambdaPSRuntimeImagePull')
            $p.Statement[0].Effect | Should -Be 'Deny'
        }
    }

    It 'accepts a single Statement object rather than an array, keeping it first' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2012-10-17","Statement":{"Sid":"One","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":"ecr:BatchGetImage"}}'
            $p = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json
            @($p.Statement.Sid) | Should -Be @('One', 'LambdaPSRuntimeImagePull')
        }
    }

    It 'adds Version when the existing policy has none' {
        InModuleScope $script:moduleName {
            $text = '{"Statement":[{"Sid":"One","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":"ecr:BatchGetImage"}]}'
            (Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json).Version | Should -Be '2012-10-17'
        }
    }

    It 'returns $null when a later statement grants the pull after one that does not' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2012-10-17","Statement":[' +
                '{"Sid":"Other","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*","Condition":{"StringEquals":{"aws:SourceAccount":"999988887777"}}},' +
                '{"Sid":"Open","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*"}]}'
            Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text -WarningAction SilentlyContinue | Should -BeNullOrEmpty
        }
    }

    It 'says a grant is added only when it adds one' {
        InModuleScope $script:moduleName {
            $other = '{"Sid":"Other","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*","Condition":{"StringEquals":{"aws:SourceAccount":"999988887777"}}}'
            $scoped = '{"Sid":"Scoped","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*","Condition":{"StringEquals":{"aws:SourceAccount":"111122223333"}}}'
            $null = Merge-LambdaPSEcrPullPolicy @scope -PolicyText ('{"Version":"2012-10-17","Statement":[' + $other + ',' + $scoped + ']}') -Verbose 4>&1 |
                Tee-Object -Variable covered
            @($covered | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] -and $_.Message -like '*is added*' }) | Should -BeNullOrEmpty
            $added = Merge-LambdaPSEcrPullPolicy @scope -PolicyText ('{"Version":"2012-10-17","Statement":[' + $other + ']}') -Verbose 4>&1
            @($added | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] -and $_.Message -like '1 Lambda image-pull grant with *was left unchanged*is added beside it.' }).Count | Should -Be 1
            # One line for all unmet grants, carrying their count.
            $two = Merge-LambdaPSEcrPullPolicy @scope -PolicyText ('{"Version":"2012-10-17","Statement":[' + $other + ',' + ($other -replace '"Other"', '"Other2"') + ']}') -Verbose 4>&1
            @($two | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] -and $_.Message -like '2 Lambda image-pull grants with *were left unchanged*is added beside them.' }).Count | Should -Be 1
        }
    }

    # IAM's ArnLike splits on colons, so a bare * is not taken as matching the runtime ARN.
    It 'adds a scoped grant beside an ArnLike grant on a bare *' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2012-10-17","Statement":[{"Sid":"Bare","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"ecr:*","Condition":{"ArnLike":{"aws:SourceArn":"*"}}}]}'
            @((Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json).Statement.Sid) | Should -Be @('Bare', 'LambdaPSRuntimeImagePull')
        }
    }

    It 'rejects <Case> ending in a newline at parameter binding' -ForEach @(
        @{ Case = 'an account'; Arguments = @{ AccountId = "111122223333`n"; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' } }
        @{ Case = 'a runtime ARN'; Arguments = @{ AccountId = '111122223333'; RuntimeArn = "arn:aws:lambda:us-east-1:111122223333:runtime/my-ps`n" } }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Arguments = $Arguments } {
            param($Arguments)
            { Merge-LambdaPSEcrPullPolicy @Arguments -PolicyText $null } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Merge-LambdaPSEcrPullPolicy'
        }
    }

    It 'rejects a runtime ARN with an uppercase <Case> at parameter binding' -ForEach @(
        @{ Case = 'partition'; RuntimeArn = 'arn:AWS:lambda:us-east-1:111122223333:runtime/my-ps' }
        @{ Case = 'region'; RuntimeArn = 'arn:aws:lambda:US-EAST-1:111122223333:runtime/my-ps' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ RuntimeArn = $RuntimeArn } {
            param($RuntimeArn)
            { Merge-LambdaPSEcrPullPolicy -AccountId '111122223333' -RuntimeArn $RuntimeArn -PolicyText $null } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Merge-LambdaPSEcrPullPolicy'
        }
    }

    # \d also matches other scripts' digits, such as Arabic-Indic ones; an account ID is ASCII digits only.
    It 'rejects <Case> in non-ASCII digits at parameter binding' -ForEach @(
        @{ Case = 'an account'; Arguments = @{ AccountId = "11112222333$([char]0x0663)"; RuntimeArn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps' } }
        @{ Case = 'a runtime ARN'; Arguments = @{ AccountId = '111122223333'; RuntimeArn = "arn:aws:lambda:us-east-1:11112222333$([char]0x0663):runtime/my-ps" } }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Arguments = $Arguments } {
            param($Arguments)
            { Merge-LambdaPSEcrPullPolicy @Arguments -PolicyText $null } |
                Should -Throw -ErrorId 'ParameterArgumentValidationError,Merge-LambdaPSEcrPullPolicy'
        }
    }

    # A redeploy runs the merge again on the policy the first deploy wrote.
    It 'returns $null when run again on its own output (<Case>)' -ForEach @(
        @{ Case = 'no policy'; Text = $null }
        @{ Case = 'an unrelated policy'; Text = '{"Version":"2012-10-17","Statement":[{"Sid":"CrossAccountPull","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":"ecr:BatchGetImage"}]}' }
        @{ Case = 'the default Sid taken'; Text = '{"Version":"2012-10-17","Statement":[{"Sid":"LambdaPSRuntimeImagePull","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":"ecr:BatchGetImage"}]}' }
        @{ Case = 'another runtime''s grant'; Text = '{"Version":"2012-10-17","Statement":[{"Sid":"LambdaPSRuntimeImagePull","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"],"Condition":{"StringEquals":{"aws:SourceAccount":"111122223333","aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/other"}}}]}' }
    ) {
        InModuleScope $script:moduleName -Parameters @{ Text = $Text } {
            param($Text)
            $once = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text
            $once | Should -Not -BeNullOrEmpty
            Merge-LambdaPSEcrPullPolicy @scope -PolicyText $once | Should -BeNullOrEmpty
        }
    }

    It 'keeps the new Sid unique when the default name is already taken' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2012-10-17","Statement":[{"Sid":"LambdaPSRuntimeImagePull","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":"ecr:BatchGetImage"}]}'
            $sids = @((Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json).Statement.Sid)
            ($sids | Sort-Object -Unique).Count | Should -Be $sids.Count
            $sids | Should -Contain 'LambdaPSRuntimeImagePull2'
        }
    }

    It 'scopes the statement it adds to this account and this runtime' {
        InModuleScope $script:moduleName {
            $statement = (Merge-LambdaPSEcrPullPolicy @scope -PolicyText $null | ConvertFrom-Json).Statement[0]
            $statement.Condition.StringEquals.'aws:SourceAccount' | Should -Be '111122223333'
            $statement.Condition.StringEquals.'aws:SourceArn' | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
            @($statement.Condition.PSObject.Properties.Name) | Should -Be @('StringEquals')
        }
    }

    It 'returns $null for a Lambda grant whose SourceArn condition names this runtime (<Case>)' -ForEach @(
        @{ Case = 'exact ARN'; Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'; Operator = 'StringEquals'; Key = 'aws:SourceArn' }
        @{ Case = 'wildcard, lower-case key'; Arn = 'arn:aws:lambda:us-east-1:111122223333:runtime/*'; Operator = 'ArnLike'; Key = 'aws:sourceArn' }
    ) {
        $text = '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},' +
            '"Action":["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"],"Condition":{"' + $Operator + '":{"' + $Key + '":"' + $Arn + '"}}}]}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text } {
            param($Text)
            Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text | Should -BeNullOrEmpty
        }
    }

    It 'adds a scoped grant beside a Lambda grant whose Condition this pull does not meet (<Case>)' -ForEach @(
        @{ Case = 'a negated operator'; Condition = '{"ArnNotLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-ps"}}' }
        @{ Case = 'another account'; Condition = '{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-ps"},"StringEquals":{"aws:SourceAccount":"999988887777"}}' }
        @{ Case = 'a key it does not evaluate'; Condition = '{"StringEquals":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/my-ps","aws:PrincipalOrgID":"o-abc123"}}' }
        @{ Case = 'a * under StringEquals, which IAM compares literally'; Condition = '{"StringEquals":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/*"}}' }
    ) {
        $text = '{"Version":"2012-10-17","Statement":[{"Sid":"Existing","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},' +
            '"Action":["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"],"Condition":' + $Condition + '}]}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text } {
            param($Text)
            $p = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text | ConvertFrom-Json
            @($p.Statement.Sid) | Should -Be @('Existing', 'LambdaPSRuntimeImagePull')
        }
    }

    It 'writes the existing statements back exactly as they were read, date-like and escaped strings included' {
        $existing = '{"Sid":"Dated","Effect":"Allow","Principal":{"AWS":"arn:aws:iam::444455556666:root"},"Action":"ecr:BatchGetImage",' +
            '"Condition":{"DateGreaterThan":{"aws:CurrentTime":"2026-01-01T00:00:00+05:00"},"StringEquals":{"aws:PrincipalTag/team":"a+b<c"}}}'
        $text = '{"Version":"2012-10-17","Statement":[' + $existing + ']}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text; Existing = $existing } {
            param($Text, $Existing)
            $merged = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text
            $merged.StartsWith('{"Version":"2012-10-17","Statement":[' + $Existing + ',{', [System.StringComparison]::Ordinal) | Should -BeTrue
        }
    }

    It 'adds a scoped grant beside a Lambda grant conditioned on something else, keeping it' {
        $text = '{"Version":"2012-10-17","Statement":[{"Sid":"OtherRuntime","Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},' +
            '"Action":["ecr:BatchGetImage","ecr:GetDownloadUrlForLayer"],"Condition":{"ArnLike":{"aws:SourceArn":"arn:aws:lambda:us-east-1:111122223333:runtime/other"}}}]}'
        InModuleScope $script:moduleName -Parameters @{ Text = $text } {
            param($Text)
            $p = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $Text | ConvertFrom-Json
            @($p.Statement.Sid) | Should -Be @('OtherRuntime', 'LambdaPSRuntimeImagePull')
            $p.Statement[0].Condition.ArnLike.'aws:SourceArn' | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/other'
            $p.Statement[1].Condition.StringEquals.'aws:SourceArn' | Should -Be 'arn:aws:lambda:us-east-1:111122223333:runtime/my-ps'
        }
    }

    It 'preserves an existing statement''s Condition and Version exactly' {
        InModuleScope $script:moduleName {
            $text = '{"Version":"2008-10-17","Statement":[{"Sid":"Scoped","Effect":"Allow","Principal":{"AWS":"*"},"Action":"ecr:BatchGetImage","Condition":{"StringEquals":{"aws:PrincipalOrgID":"o-abc123"}}}]}'
            $p = Merge-LambdaPSEcrPullPolicy @scope -PolicyText $text | ConvertFrom-Json
            $p.Version | Should -Be '2008-10-17'
            $p.Statement[0].Condition.StringEquals.'aws:PrincipalOrgID' | Should -Be 'o-abc123'
        }
    }
}
