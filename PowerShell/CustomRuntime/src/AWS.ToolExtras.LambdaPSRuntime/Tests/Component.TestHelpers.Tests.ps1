BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FreshRunspace.ps1'))
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'FakeRuntimeApi.ps1'))
}

AfterAll {
    Remove-LambdaPSTestModule
}

# The helpers in Helpers/FreshRunspace.ps1 run the module where no Pester try encloses it. These pin
# what the tests that use them rely on.
Describe 'Invoke-InFreshRunspace' -Tag 'Component' {
    It 'runs in another process, so the module is imported into a fresh AppDomain' {
        $result = Invoke-InFreshRunspace -Command '$PID'

        $result.Output | Should -Not -BeNullOrEmpty
        $result.Output[0] | Should -Not -Be $PID
    }

    It 'returns the error records and the call log' {
        $stub = { function script:Get-LambdaPSPartition { $script:LambdaPSCalls.Add('Get-LambdaPSPartition'); Write-Error -Message 'stub error' -ErrorId 'StubError' } }

        $result = Invoke-InFreshRunspace -Stub $stub.ToString() -Command "& (Get-Module $script:moduleName) { Get-LambdaPSPartition -Region us-east-1 }"

        $result.Calls | Should -Be @('Get-LambdaPSPartition')
        $result.Errors.FullyQualifiedErrorId | Should -Contain 'StubError,Get-LambdaPSPartition'
    }
}

Describe 'Invoke-InChildPwsh' -Tag 'Component' {
    It 'returns its output with LF line endings, so callers can split it on `n' {
        $result = Invoke-InChildPwsh -Command '[Console]::Out.Write("What if: line one`r`nWhat if: line two`r`n")'

        @($result.Output -split "`n" | Where-Object { $_ -like 'What if:*' }) | Should -Be @('What if: line one', 'What if: line two')
    }
}

# A stand-in runtime written per test: $Body runs after $api and Get-LambdaPSNextEvent are defined.
# Get-LambdaPSNextEvent asks /next and returns the request id; Send-LambdaPSResponse posts its result.
Describe 'Invoke-InChildPwsh when the child ends early' -Tag 'Component' {
    It 'throws with the child''s exit code and output when the child exits before reporting its calls' {
        { Invoke-InChildPwsh -Command "'printed before exit'; exit 4" } |
            Should -Throw '*exited with code 4 before it reported its calls*printed before exit*'
    }
}

Describe 'Invoke-LambdaPSRuntimeLoop' -Tag 'Component' {
    BeforeAll {
        $script:runtimePrelude = @'
$api = "http://$env:AWS_LAMBDA_RUNTIME_API/2018-06-01/runtime"
function Get-LambdaPSNextEvent { [string](Invoke-WebRequest -Uri "$api/invocation/next" -NoProxy -TimeoutSec 600).Headers['Lambda-Runtime-Aws-Request-Id'] }
function Send-LambdaPSResponse { param($Id) $null = Invoke-WebRequest -Method Post -Uri "$api/invocation/$Id/response" -Body '{}' -NoProxy }
'@
        $script:taskRoot = [System.IO.Path]::Combine($TestDrive, 'task')
        $null = New-Item -ItemType Directory -Path $script:taskRoot -Force
    }

    It 'records the exit code of a runtime that exits by itself' {
        $runtime = [System.IO.Path]::Combine($TestDrive, 'exit-3.ps1')
        Set-Content -LiteralPath $runtime -Value ($script:runtimePrelude + "`nSend-LambdaPSResponse (Get-LambdaPSNextEvent); exit 3")

        $posts = @(Invoke-LambdaPSRuntimeLoop -RuntimePath $runtime -TaskRoot $script:taskRoot -Handler 'x.ps1' -EventBody '{}' -ExpectExit -ExitCodeVariable exitCode -TimeoutSeconds 30)

        $posts | Should -HaveCount 1
        $exitCode | Should -Be 3
    }

    It 'records no exit code for a runtime it had to stop' {
        $runtime = [System.IO.Path]::Combine($TestDrive, 'keeps-running.ps1')
        Set-Content -LiteralPath $runtime -Value ($script:runtimePrelude + "`nwhile (`$true) { Send-LambdaPSResponse (Get-LambdaPSNextEvent) }")
        $exitCode = 'unset'

        $null = Invoke-LambdaPSRuntimeLoop -RuntimePath $runtime -TaskRoot $script:taskRoot -Handler 'x.ps1' -EventBody '{}' -ExitCodeVariable exitCode -TimeoutSeconds 30

        $exitCode | Should -BeNullOrEmpty
    }

    It 'starts -TimeoutSeconds when the first event is served, not when the runtime starts' {
        $runtime = [System.IO.Path]::Combine($TestDrive, 'slow-start.ps1')
        Set-Content -LiteralPath $runtime -Value ($script:runtimePrelude + "`nStart-Sleep -Seconds 6; while (`$true) { Send-LambdaPSResponse (Get-LambdaPSNextEvent) }")

        $posts = @(Invoke-LambdaPSRuntimeLoop -RuntimePath $runtime -TaskRoot $script:taskRoot -Handler 'x.ps1' -EventBody '{}' -TimeoutSeconds 3)

        $posts | Should -HaveCount 1
    }

    It 'still fails when an event is not done within -TimeoutSeconds of being served' {
        $runtime = [System.IO.Path]::Combine($TestDrive, 'slow-event.ps1')
        Set-Content -LiteralPath $runtime -Value ($script:runtimePrelude + "`n`$null = Get-LambdaPSNextEvent; Start-Sleep -Seconds 60")

        { Invoke-LambdaPSRuntimeLoop -RuntimePath $runtime -TaskRoot $script:taskRoot -Handler 'x.ps1' -EventBody '{}' -TimeoutSeconds 2 } |
            Should -Throw '*still had 1 of 1 event(s) open after 2 seconds*'
    }

    It 'sends -ClientContext, -CognitoIdentity and -TenantId as the Lambda-Runtime-Client-Context, Lambda-Runtime-Cognito-Identity and Lambda-Runtime-Aws-Tenant-Id headers' {
        $runtime = [System.IO.Path]::Combine($TestDrive, 'echo-headers.ps1')
        Set-Content -LiteralPath $runtime -Value ($script:runtimePrelude + @'

while ($true) {
    $next = Invoke-WebRequest -Uri "$api/invocation/next" -NoProxy -TimeoutSec 600
    $body = "$($next.Headers['Lambda-Runtime-Client-Context'])|$($next.Headers['Lambda-Runtime-Cognito-Identity'])|$($next.Headers['Lambda-Runtime-Aws-Tenant-Id'])"
    $null = Invoke-WebRequest -Method Post -Uri "$api/invocation/$($next.Headers['Lambda-Runtime-Aws-Request-Id'])/response" -Body $body -NoProxy
}
'@)

        $posts = @(Invoke-LambdaPSRuntimeLoop -RuntimePath $runtime -TaskRoot $script:taskRoot -Handler 'x.ps1' -EventBody '{}', '{}' -ClientContext '{"client":{"app_title":"a"}}' -CognitoIdentity '', '{"cognitoIdentityId":"id-1"}' -TenantId 'tenant-a' -TimeoutSeconds 30)

        $posts.Body | Should -Be @('{"client":{"app_title":"a"}}||tenant-a', '|{"cognitoIdentityId":"id-1"}|')
    }

    It 'fails when the runtime neither asks for an event nor exits within -StartupTimeoutSeconds' {
        $runtime = [System.IO.Path]::Combine($TestDrive, 'never-asks.ps1')
        Set-Content -LiteralPath $runtime -Value 'Start-Sleep -Seconds 60'

        { Invoke-LambdaPSRuntimeLoop -RuntimePath $runtime -TaskRoot $script:taskRoot -Handler 'x.ps1' -EventBody '{}' -StartupTimeoutSeconds 3 } |
            Should -Throw '*neither asked for its first event nor exited within 3 seconds*'
    }
}

# This host may have docker installed, so each case runs in a child pwsh whose PATH has none.
Describe 'Add-LambdaPSDockerStub' -Tag 'Component' {
    BeforeAll {
        # Runs $Body in a child pwsh with Helpers/DockerStub.ps1 loaded and an empty folder as PATH.
        function Invoke-WithoutDocker {
            param([Parameter(Mandatory)] [string]$Body)
            $emptyPath = [System.IO.Path]::Combine($TestDrive, "no-docker-$([guid]::NewGuid().ToString('n'))")
            $null = New-Item -ItemType Directory -Path $emptyPath
            $command = @"
`$env:PATH = '$emptyPath'
. '$([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'DockerStub.ps1'))'
$Body
"@
            (Invoke-InChildPwsh -Command $command).Output -split "`n"
        }
    }

    It 'puts a docker executable first on PATH that records each call''s arguments' {
        $output = Invoke-WithoutDocker -Body @'
$stub = Add-LambdaPSDockerStub
'RESOLVED:' + (Get-Command -Name docker -CommandType Application).Source
'STUB:' + $stub.Path
& docker build --tag x .
& docker version
'CALLS:' + ((Get-Content -LiteralPath $stub.CallLog) -join '|')
'FUNCTION:' + (Test-Path -LiteralPath 'function:docker')
'@
        $stubPath = ($output | Where-Object { $_ -like 'STUB:*' }).Substring(5)
        $output | Should -Contain "RESOLVED:$stubPath"
        $output | Should -Contain 'CALLS:build --tag x .|version'
        $output | Should -Contain 'FUNCTION:False'
    }

    It 'is undone by Remove-LambdaPSDockerStub' {
        $output = Invoke-WithoutDocker -Body @'
$before = $env:PATH
$stub = Add-LambdaPSDockerStub
Remove-LambdaPSDockerStub
'PATH RESTORED:' + ($env:PATH -eq $before)
'RESOLVES:' + [bool](Get-Command -Name docker -ErrorAction SilentlyContinue)
'FOLDER LEFT:' + (Test-Path -LiteralPath $stub.Directory)
'@
        $output | Should -Contain 'PATH RESTORED:True'
        $output | Should -Contain 'RESOLVES:False'
        $output | Should -Contain 'FOLDER LEFT:False'
    }
}
