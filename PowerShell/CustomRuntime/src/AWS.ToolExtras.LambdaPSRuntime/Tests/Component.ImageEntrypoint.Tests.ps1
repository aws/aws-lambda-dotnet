BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))
    $script:entrypointTemplate = [System.IO.Path]::Combine($script:moduleRoot, 'Templates', 'RuntimeImage', 'lambda-entrypoint.sh')

    # A copy of the template whose absolute paths point at stand-ins in the test drive: a bootstrap
    # and a Runtime Interface Emulator that print what they were started with.
    $script:root = [System.IO.Path]::Combine($TestDrive, 'entrypoint')
    $null = New-Item -ItemType Directory -Path $script:root
    $script:bootstrap = [System.IO.Path]::Combine($script:root, 'bootstrap')
    $script:rie = [System.IO.Path]::Combine($script:root, 'aws-lambda-rie')
    [System.IO.File]::WriteAllText($script:bootstrap, "#!/bin/sh`necho `"bootstrap handler=[`$_HANDLER] args=[`$*]`"`n")
    [System.IO.File]::WriteAllText($script:rie, "#!/bin/sh`necho `"rie args=[`$*]`"`n")
    $script:entrypoint = [System.IO.Path]::Combine($script:root, 'lambda-entrypoint.sh')
    $text = [System.IO.File]::ReadAllText($script:entrypointTemplate).Replace('/usr/local/bin/aws-lambda-rie', $script:rie).Replace('/var/runtime/bootstrap', $script:bootstrap)
    [System.IO.File]::WriteAllText($script:entrypoint, $text)
    if (-not $IsWindows) {
        foreach ($file in $script:bootstrap, $script:rie, $script:entrypoint) {
            [System.IO.File]::SetUnixFileMode($file, [System.IO.UnixFileMode]'UserRead, UserWrite, UserExecute')
        }
    }

    # Runs the entrypoint with the given arguments and environment, as Lambda starts the image.
    function Invoke-Entrypoint {
        param([string[]]$Argument = @(), [hashtable]$Environment = @{})
        $saved = @{}
        foreach ($name in '_HANDLER', 'AWS_LAMBDA_RUNTIME_API') {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
            [Environment]::SetEnvironmentVariable($name, $Environment[$name])
        }
        try { & $script:entrypoint @Argument 2>&1 }
        finally { foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) } }
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Templates/RuntimeImage/lambda-entrypoint.sh' -Tag 'Component' -Skip:$IsWindows {
    It 'sets the handler from its first argument (a function image''s CMD), over the _HANDLER Lambda sets' {
        Invoke-Entrypoint -Argument 'handler.ps1' -Environment @{ _HANDLER = 'other.ps1'; AWS_LAMBDA_RUNTIME_API = '127.0.0.1:9001' } |
            Should -Be 'bootstrap handler=[handler.ps1] args=[]'
        $LASTEXITCODE | Should -Be 0
    }

    It 'keeps the _HANDLER Lambda sets when it is started with no argument, as a customer-managed runtime is' {
        Invoke-Entrypoint -Environment @{ _HANDLER = 'handler.ps1::Invoke'; AWS_LAMBDA_RUNTIME_API = '127.0.0.1:9001' } |
            Should -Be 'bootstrap handler=[handler.ps1::Invoke] args=[]'
    }

    # runtime.ps1 then reports the empty handler as an init error, which the invoke shows.
    It 'still starts the bootstrap with no handler at all' {
        Invoke-Entrypoint -Environment @{ AWS_LAMBDA_RUNTIME_API = '127.0.0.1:9001' } |
            Should -Be 'bootstrap handler=[] args=[]'
    }

    It 'runs the bootstrap under the Runtime Interface Emulator when there is no Runtime API (a local docker run)' {
        Invoke-Entrypoint -Argument 'handler.ps1' | Should -Be "rie args=[$script:bootstrap]"
    }

    It 'is LF-only and starts with a shebang, since the kernel runs it directly' {
        $bytes = [System.IO.File]::ReadAllBytes($script:entrypointTemplate)
        [System.Text.Encoding]::ASCII.GetString($bytes, 0, 2) | Should -Be '#!'
        $bytes | Should -Not -Contain ([byte]13)
    }
}
