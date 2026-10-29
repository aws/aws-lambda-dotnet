BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'PayloadStream.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Invoke-LambdaPSFunction' -Tag 'Unit' {
    Context 'payload serialization' {
        BeforeEach {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{}'); LogResult = $null; FunctionError = $null }
            }
        }

        It 'serializes an object payload to JSON before sending' {
            $null = Invoke-LambdaPSFunction -FunctionName hello -Payload @{ name = 'world' }

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Payload -eq '{"name":"world"}' }
        }

        It 'sends a string payload verbatim (assumed already JSON)' {
            $null = Invoke-LambdaPSFunction -FunctionName hello -Payload '{"name":"world"}'

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Payload -eq '{"name":"world"}' }
        }

        It 'sends an array payload as an array (<Case>)' -ForEach @(
            @{ Case = 'one element'; Value = @(@{ a = 1 }); Json = '[{"a":1}]' }
            @{ Case = 'empty'; Value = @(); Json = '[]' }
        ) {
            $null = Invoke-LambdaPSFunction -FunctionName hello -Payload $Value
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Payload -eq $Json }
        }

        It 'sends no payload when -Payload is omitted' {
            $null = Invoke-LambdaPSFunction -FunctionName hello

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $null -eq $Payload }
        }

        It 'passes -ProfileName, -ProfileLocation and -Credential to Invoke-LMFunction, and nothing when omitted' {
            $anonymous = [Amazon.Runtime.AnonymousAWSCredentials]::new()
            # A credential file of the test's own that defines the profile.
            $credentialFile = [System.IO.Path]::Combine($TestDrive, 'credentials')
            Set-Content -LiteralPath $credentialFile -Value "[dev]`naws_access_key_id = AKIAEXAMPLE`naws_secret_access_key = example"
            $null = Invoke-LambdaPSFunction -FunctionName hello -ProfileName dev -ProfileLocation $credentialFile -Credential $anonymous
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                $ProfileName -eq 'dev' -and $ProfileLocation -eq $credentialFile -and [object]::ReferenceEquals($Credential, $anonymous)
            }

            $null = Invoke-LambdaPSFunction -FunctionName hello
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter {
                [string]::IsNullOrEmpty($ProfileName) -and [string]::IsNullOrEmpty($ProfileLocation) -and $null -eq $Credential
            }
        }

        It 'takes FunctionName and Region from a piped deployment result' {
            $deployments = @(
                [PSCustomObject]@{ FunctionName = 'fn-a'; Region = 'us-west-2' }
                [PSCustomObject]@{ FunctionName = 'fn-b'; Region = 'eu-central-1' }
            )
            $null = $deployments | Invoke-LambdaPSFunction

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $FunctionName -eq 'fn-a' -and $Region -eq 'us-west-2' }
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $FunctionName -eq 'fn-b' -and $Region -eq 'eu-central-1' }
        }

        It 'invokes each function name piped in as a string' {
            $null = 'fn-a', 'fn-b' | Invoke-LambdaPSFunction

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $FunctionName -eq 'fn-a' }
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $FunctionName -eq 'fn-b' }
        }

        It 'uses the region of a function ARN when -Region is omitted (<Case>)' -ForEach @(
            @{ Case = 'unqualified'; Name = 'arn:aws:lambda:eu-west-1:111122223333:function:hello' }
            @{ Case = 'qualified'; Name = 'arn:aws:lambda:eu-west-1:111122223333:function:hello:live' }
            @{ Case = 'other partition'; Name = 'arn:aws-cn:lambda:eu-west-1:111122223333:function:hello' }
        ) {
            $null = Invoke-LambdaPSFunction -FunctionName $Name

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'eu-west-1' }
        }

        It 'takes no region from <Case>' -ForEach @(
            @{ Case = 'a partial ARN'; Name = '111122223333:function:hello' }
            @{ Case = 'an ARN whose account has a non-ASCII digit'; Name = "arn:aws:lambda:eu-west-1:11112222333$([char]0x0663):function:hello" }
        ) {
            $null = Invoke-LambdaPSFunction -FunctionName $Name

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $null -eq $Region }
        }

        It 'refuses a -Region that differs from the region in a function ARN, before invoking' {
            $err = { Invoke-LambdaPSFunction -FunctionName 'arn:aws:lambda:eu-west-1:111122223333:function:hello' -Region us-west-2 } |
                Should -Throw -ErrorId 'FunctionRegionMismatch,Invoke-LambdaPSFunction' -PassThru
            $err.Exception.Message | Should -BeLike "*region 'eu-west-1', but -Region is 'us-west-2'. Omit -Region, or pass -Region eu-west-1."
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        # Region names are matched without regard to case, so the same region in capitals is no mismatch.
        It 'accepts a -Region that differs from the ARN''s only in case' {
            $null = Invoke-LambdaPSFunction -FunctionName 'arn:aws:lambda:eu-west-1:111122223333:function:hello' -Region EU-WEST-1

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'EU-WEST-1' }
        }

        It 'accepts a -Region equal to the ARN''s, and adds none for a plain name' {
            $null = Invoke-LambdaPSFunction -FunctionName 'arn:aws:lambda:eu-west-1:111122223333:function:hello' -Region eu-west-1
            $null = Invoke-LambdaPSFunction -FunctionName hello

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Region -eq 'eu-west-1' }
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $FunctionName -eq 'hello' -and $null -eq $Region }
        }

        It 'threads -Region and -Qualifier through to Invoke-LMFunction' {
            $null = Invoke-LambdaPSFunction -FunctionName hello -Region us-west-2 -Qualifier prod

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $Region -eq 'us-west-2' -and $Qualifier -eq 'prod' }
        }
    }

    Context 'output parsing' {
        It 'returns the parsed function output when the payload is JSON' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{"message":"Hello, world","count":3}'); LogResult = $null; FunctionError = $null }
            }

            $result = Invoke-LambdaPSFunction -FunctionName hello -Payload @{ name = 'world' }

            $result.message | Should -Be 'Hello, world'
            $result.count | Should -Be 3
        }

        It 'returns a JSON array as an array, including one element and none' -ForEach @(
            @{ Json = '[1]'; Count = 1 }, @{ Json = '[]'; Count = 0 }, @{ Json = '[1,2]'; Count = 2 }
        ) {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith ([scriptblock]::Create(
                "[PSCustomObject]@{ Payload = (New-PayloadStream '$Json'); LogResult = `$null; FunctionError = `$null }"))

            $result = Invoke-LambdaPSFunction -FunctionName hello

            , $result | Should -BeOfType ([array])
            $result.Count | Should -Be $Count
        }

        It 'returns the raw string when the payload is not JSON' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream 'plain text'); LogResult = $null; FunctionError = $null }
            }

            $result = Invoke-LambdaPSFunction -FunctionName hello

            $result | Should -Be 'plain text'
        }

        # A caller that checks -ErrorVariable must not see a successful invoke as a failed one.
        It 'leaves -ErrorVariable empty for a successful invoke whose payload is <Case>' -ForEach @(
            @{ Case = 'not JSON'; Body = 'plain text'; Expected = 'plain text' }
            @{ Case = 'JSON'; Body = '{"ok":"yes"}'; Expected = 'yes' }
        ) {
            $script:okBody = $Body
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream $script:okBody); LogResult = $null; FunctionError = $null }
            }

            $result = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors

            @($errors).Count | Should -Be 0
            $(if ($result -is [string]) { $result } else { $result.ok }) | Should -Be $Expected
        }

        # ConvertFrom-Json refuses keys that differ only in case, so such JSON is returned as text, and no error is recorded.
        It 'returns JSON whose keys differ only in case as text, and leaves -ErrorVariable empty' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{"a":1,"A":2}'); LogResult = $null; FunctionError = $null }
            }

            $result = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors

            @($errors).Count | Should -Be 0
            $result | Should -BeOfType ([string])
            $result | Should -BeExactly '{"a":1,"A":2}'
        }

        # ConvertFrom-Json refuses an empty key at any depth, so such JSON is returned as text, and no error is recorded.
        It 'returns JSON with an empty key <Case> as text, and leaves -ErrorVariable empty' -ForEach @(
            @{ Case = 'at the top level'; Body = '{"":1}' }
            @{ Case = 'in a nested object'; Body = '{"a":{"":1}}' }
            @{ Case = 'in an object in an array'; Body = '{"a":[{"":1}]}' }
        ) {
            $script:emptyKeyBody = $Body
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream $script:emptyKeyBody); LogResult = $null; FunctionError = $null }
            }

            $result = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors

            @($errors).Count | Should -Be 0
            $result | Should -BeOfType ([string])
            $result | Should -BeExactly $Body
        }

        It 'returns nothing for an empty payload' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream ''); LogResult = $null; FunctionError = $null }
            }

            $result = Invoke-LambdaPSFunction -FunctionName hello

            $result | Should -BeNullOrEmpty
        }
    }

    Context 'execution log' {
        It 'requests the log and surfaces the decoded log on the Information stream with -IncludeLog' {
            $logText = "START RequestId: abc`nEND RequestId: abc"
            $encoded = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($logText))
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{}'); LogResult = $encoded; FunctionError = $null }
            }

            $null = Invoke-LambdaPSFunction -FunctionName hello -IncludeLog -InformationVariable info

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $LogType -eq 'Tail' }
            ($info | ForEach-Object { $_.MessageData }) -join "`n" | Should -Match 'START RequestId'
        }

        It 'does not request the log by default' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{}'); LogResult = $null; FunctionError = $null }
            }

            $null = Invoke-LambdaPSFunction -FunctionName hello

            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly `
                -ParameterFilter { $null -eq $LogType }
        }
    }

    Context 'function error' {
        It 'writes FunctionInvocationError, with the parsed error payload as its target, when the response reports a FunctionError' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{"errorMessage":"boom","errorType":"RuntimeException"}'); LogResult = $null; FunctionError = 'Unhandled' }
            }

            { Invoke-LambdaPSFunction -FunctionName hello -Payload @{ x = 1 } -ErrorAction Stop } |
                Should -Throw -ErrorId 'FunctionInvocationError,Invoke-LambdaPSFunction'
            $result = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors -ErrorAction SilentlyContinue
            $result | Should -BeNullOrEmpty
            $errors[0].TargetObject.errorType | Should -Be 'RuntimeException'
        }

        It 'still invokes the functions piped in after one that returns an error' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                if ($FunctionName -eq 'bad') {
                    [PSCustomObject]@{ Payload = (New-PayloadStream '{"errorMessage":"boom"}'); LogResult = $null; FunctionError = 'Unhandled' }
                }
                else { [PSCustomObject]@{ Payload = (New-PayloadStream '{"ok":true}'); LogResult = $null; FunctionError = $null } }
            }

            $results = @('bad', 'good' | ForEach-Object { [PSCustomObject]@{ FunctionName = $_ } } |
                    Invoke-LambdaPSFunction -ErrorVariable errors -ErrorAction SilentlyContinue)
            $results.Count | Should -Be 1
            $results[0].ok | Should -BeTrue
            @($errors).Count | Should -Be 1
        }

        It 'reports a function error whose JSON keys differ only in case as the one record, with the text as its target' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{"errorType":"E","ErrorType":"F"}'); LogResult = $null; FunctionError = 'Unhandled' }
            }

            $null = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors -ErrorAction SilentlyContinue

            @($errors).Count | Should -Be 1
            $errors[0].FullyQualifiedErrorId | Should -Be 'FunctionInvocationError,Invoke-LambdaPSFunction'
            $errors[0].TargetObject | Should -BeExactly '{"errorType":"E","ErrorType":"F"}'
        }

        It 'reports a function error whose JSON has an empty key as the one record, with the text as its target' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{"errorType":"E","":"x"}'); LogResult = $null; FunctionError = 'Unhandled' }
            }

            $null = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors -ErrorAction SilentlyContinue

            @($errors).Count | Should -Be 1
            $errors[0].FullyQualifiedErrorId | Should -Be 'FunctionInvocationError,Invoke-LambdaPSFunction'
            $errors[0].TargetObject | Should -BeExactly '{"errorType":"E","":"x"}'
        }

        It 'reports a function error whose payload is <Case>' -ForEach @(
            @{ Case = 'not JSON'; Body = 'Task timed out'; Target = 'Task timed out'; Message = "*returned a function error (Unhandled): Task timed out" }
            @{ Case = 'empty'; Body = ''; Target = ''; Message = '*returned a function error (Unhandled).' }
        ) {
            $script:errorBody = $Body
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream $script:errorBody); LogResult = $null; FunctionError = 'Unhandled' }
            }

            $null = Invoke-LambdaPSFunction -FunctionName hello -ErrorVariable errors -ErrorAction SilentlyContinue

            # The function error is the only record: finding that the payload is not JSON adds none.
            $record = @($errors)
            $record.Count | Should -Be 1
            $record[0].FullyQualifiedErrorId | Should -Be 'FunctionInvocationError,Invoke-LambdaPSFunction'
            $record[0].TargetObject | Should -Be $Target
            $record[0].Exception.Message | Should -BeLike $Message
        }
    }

    Context 'payload depth' {
        BeforeEach {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                [PSCustomObject]@{ Payload = (New-PayloadStream '{}'); LogResult = $null; FunctionError = $null }
            }
        }

        # ConvertTo-Json only warns when it cuts an object short, so a deep payload would arrive truncated.
        It 'refuses a payload nested deeper than 32 levels, before invoking' {
            $payload = @{ leaf = 'value' }
            foreach ($level in 1..40) { $payload = @{ inner = $payload } }

            $err = { Invoke-LambdaPSFunction -FunctionName hello -Payload $payload } |
                Should -Throw -ErrorId 'PayloadTooDeep,Invoke-LambdaPSFunction' -PassThru

            $err.Exception.Message | Should -BeLike '*nested deeper than 32 levels*'
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 0 -Exactly
        }

        It 'sends a payload nested 30 levels deep in full' {
            $payload = @{ leaf = 'value' }
            foreach ($level in 1..30) { $payload = @{ inner = $payload } }

            $null = Invoke-LambdaPSFunction -FunctionName hello -Payload $payload -WarningVariable warnings

            @($warnings).Count | Should -Be 0
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly -ParameterFilter { $Payload -like '*"leaf":"value"*' }
        }
    }

    # Unlike a function error, a request that fails (permissions, a missing function) stops the cmdlet.
    Context 'request failure' {
        It 'stops at a failed request instead of invoking the functions piped in after it' {
            Mock 'Invoke-LMFunction' -ModuleName $script:moduleName -MockWith {
                if ($FunctionName -eq 'missing') { throw [System.InvalidOperationException]::new('Function not found: missing') }
                [PSCustomObject]@{ Payload = (New-PayloadStream '{"ok":true}'); LogResult = $null; FunctionError = $null }
            }

            $piped = 'missing', 'good' | ForEach-Object { [PSCustomObject]@{ FunctionName = $_ } }
            { $piped | Invoke-LambdaPSFunction -ErrorAction SilentlyContinue } | Should -Throw '*Function not found: missing*'
            Should -Invoke 'Invoke-LMFunction' -ModuleName $script:moduleName -Times 1 -Exactly
        }
    }
}

# Set-DefaultAWSRegion and Set-AWSCredential without -Scope set the region and credentials in the scope
# they run in, such as a script, where Invoke-LMFunction, called from this module, cannot see them. The
# cmdlet reads them through its caller's session state. Run in a child pwsh with an empty credential store.
Describe 'Invoke-LambdaPSFunction from a script that set its own region and credentials' -Tag 'Unit' {
    It 'invokes in the region and with the credentials the script set, and an ARN''s region still wins' {
        $emptyStore = [System.IO.Path]::Combine($TestDrive, 'scoped-empty-store')
        Set-Content -LiteralPath $emptyStore -Value ''
        $invokeScript = [System.IO.Path]::Combine($TestDrive, 'scoped-invoke.ps1')
        Set-Content -LiteralPath $invokeScript -Value @'
Set-DefaultAWSRegion -Region eu-north-1
Set-AWSCredential -AccessKey AKIASCRIPTSCOPE -SecretKey example
$null = Invoke-LambdaPSFunction -FunctionName hello
$null = Invoke-LambdaPSFunction -FunctionName arn:aws:lambda:eu-west-1:111122223333:function:hello
'@
        $stub = {
            function script:Invoke-LMFunction {
                param($FunctionName, $Region, $ClientConfig, $Payload, $LogType, $Qualifier, $ProfileName, $ProfileLocation, $Credential, $ErrorAction)
                $key = if ($Credential) { $Credential.GetCredentials().AccessKey } else { 'none' }
                $script:LambdaPSCalls.Add("Invoke $Region $key")
                [PSCustomObject]@{ Payload = [System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes('{}')); LogResult = $null; FunctionError = $null }
            }
        }
        $command = "`$env:AWS_SHARED_CREDENTIALS_FILE = `$env:AWS_CONFIG_FILE = '$emptyStore'; `$env:AWS_REGION = 'ap-south-1'; & '$invokeScript'"

        $result = Invoke-InChildPwsh -Stub $stub.ToString() -Command $command

        $result.Output | Should -Not -BeLike '*ERROR:*'
        $result.Calls | Should -Be @('Invoke eu-north-1 AKIASCRIPTSCOPE', 'Invoke eu-west-1 AKIASCRIPTSCOPE')
    }
}
