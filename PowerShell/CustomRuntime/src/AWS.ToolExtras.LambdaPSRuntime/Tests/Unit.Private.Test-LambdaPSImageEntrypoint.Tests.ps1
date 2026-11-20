BeforeAll {
    . ([System.IO.Path]::Combine($PSScriptRoot, 'Helpers', 'Module.ps1'))

    # Writes a Dockerfile with this content to its own folder and returns its path.
    function Write-TestDockerfile {
        param([string]$Content)
        $dir = [System.IO.Path]::Combine($TestDrive, [guid]::NewGuid().ToString('n'))
        $null = New-Item -ItemType Directory -Path $dir
        $path = [System.IO.Path]::Combine($dir, 'Dockerfile')
        [System.IO.File]::WriteAllText($path, $Content)
        $path
    }
}

AfterAll {
    Remove-LambdaPSTestModule
}

Describe 'Test-LambdaPSImageEntrypoint' -Tag 'Unit' {
    It 'passes for <Target> with <Case>' -ForEach @(
        foreach ($target in 'CMR', 'Container') {
            @{ Target = $target; Case = 'the scaffold''s ENTRYPOINT'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]`n" }
            @{ Target = $target; Case = 'a lower-case instruction and no spaces'; Content = "from public.ecr.aws/lambda/provided:al2023`nentrypoint [`"/var/runtime/lambda-entrypoint.sh`"]" }
            @{ Target = $target; Case = 'a CRLF Dockerfile with a byte order mark'; Content = "$([char]0xFEFF)FROM public.ecr.aws/lambda/provided:al2023`r`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]`r`n" }
            @{ Target = $target; Case = 'a continued ENTRYPOINT'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT \`n  [ `"/var/runtime/lambda-entrypoint.sh`" ]" }
            @{ Target = $target; Case = 'a final stage built FROM an earlier stage that sets it'; Content = "FROM public.ecr.aws/lambda/provided:al2023 AS runtime`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]`nFROM runtime`nCMD [ `"handler.ps1`" ]" }
            @{ Target = $target; Case = 'a later ENTRYPOINT replacing an earlier one'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/lambda-entrypoint.sh`" ]`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]" }
            @{ Target = $target; Case = 'an ENTRYPOINT before FROM, which is not an instruction of any stage'; Content = "ARG X=1`nFROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]" }
        }
    ) {
        $path = Write-TestDockerfile -Content $Content
        InModuleScope $script:moduleName -Parameters @{ Path = $path; Target = $Target } {
            param($Path, $Target)
            $finding = Test-LambdaPSImageEntrypoint -Path $Path -Target $Target
            $finding.RuleId | Should -Be 'R-Entrypoint'
            $finding.Passed | Should -BeTrue -Because $finding.Message
            $finding.Severity | Should -Be 'Information'
        }
    }

    It 'fails for <Target> with <Case>' -ForEach @(
        foreach ($target in 'CMR', 'Container') {
            @{ Target = $target; Case = 'no ENTRYPOINT on provided.al2023'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nCMD [ `"handler.ps1`" ]"; Expect = '*sets no ENTRYPOINT*status 142*' }
            @{ Target = $target; Case = 'no ENTRYPOINT on a digest-pinned provided base'; Content = "FROM --platform=linux/amd64 public.ecr.aws/lambda/provided@sha256:0123"; Expect = '*sets no ENTRYPOINT*' }
            @{ Target = $target; Case = 'the ENTRYPOINT only in a comment'; Content = "FROM public.ecr.aws/lambda/provided:al2023`n# ENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]"; Expect = '*sets no ENTRYPOINT*' }
            @{ Target = $target; Case = 'the ENTRYPOINT in an earlier stage only'; Content = "FROM public.ecr.aws/lambda/provided:al2023 AS build`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]`nFROM public.ecr.aws/lambda/provided:al2023"; Expect = '*sets no ENTRYPOINT*' }
            @{ Target = $target; Case = 'the base image''s /lambda-entrypoint.sh'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/lambda-entrypoint.sh`" ]"; Expect = '*runs /lambda-entrypoint.sh*' }
            @{ Target = $target; Case = '/lambda-entrypoint.sh in the shell form'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT /lambda-entrypoint.sh handler.ps1"; Expect = '*runs /lambda-entrypoint.sh*' }
        }
    ) {
        $path = Write-TestDockerfile -Content $Content
        InModuleScope $script:moduleName -Parameters @{ Path = $path; Target = $Target; Expect = $Expect } {
            param($Path, $Target, $Expect)
            $finding = Test-LambdaPSImageEntrypoint -Path $Path -Target $Target
            $finding.Passed | Should -BeFalse
            $finding.Severity | Should -Be 'Error'
            $finding.Message | Should -BeLike $Expect
        }
    }

    It 'is unverified with <Case>' -ForEach @(
        @{ Case = 'another ENTRYPOINT'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/var/runtime/bootstrap`" ]"; Expect = "*an ENTRYPOINT other than the scaffold's (/var/runtime/bootstrap)*" }
        @{ Case = 'the scaffold''s entrypoint with an argument'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`", `"handler.ps1`" ]"; Expect = '*other than the scaffold*' }
        @{ Case = 'the scaffold''s entrypoint in the shell form'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT /var/runtime/lambda-entrypoint.sh"; Expect = '*shell-form ENTRYPOINT*' }
        @{ Case = 'an empty ENTRYPOINT'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT []"; Expect = '*empty ENTRYPOINT*' }
        @{ Case = 'another base image'; Content = "FROM example.com/pwsh-runtime:1"; Expect = '*base image example.com/pwsh-runtime:1*' }
        @{ Case = 'a base image from a build argument'; Content = "ARG BASE=public.ecr.aws/lambda/provided:al2023`nFROM `$BASE"; Expect = '*base image $BASE*' }
        @{ Case = 'no FROM'; Content = "ENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]"; Expect = '*no FROM instruction*' }
        @{ Case = 'an escape parser directive'; Content = "# escape=``n`nFROM public.ecr.aws/lambda/provided:al2023`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]"; Expect = '*escape parser directive*' }
        @{ Case = 'a here-document'; Content = "FROM public.ecr.aws/lambda/provided:al2023`nRUN <<EOF`nENTRYPOINT [ `"/var/runtime/lambda-entrypoint.sh`" ]`nEOF"; Expect = '*here-document*' }
    ) {
        $path = Write-TestDockerfile -Content $Content
        InModuleScope $script:moduleName -Parameters @{ Path = $path; Expect = $Expect } {
            param($Path, $Expect)
            $finding = Test-LambdaPSImageEntrypoint -Path $Path -Target CMR
            $finding.Passed | Should -BeTrue
            $finding.Unverified | Should -BeTrue
            $finding.Severity | Should -Be 'Warning'
            $finding.Message | Should -BeLike "Not verified: the Dockerfile uses $Expect"
        }
    }

    It 'passes, as not applicable, for <Case>' -ForEach @(
        @{ Case = 'the Layer target'; Target = 'Layer'; Write = $true; Expect = '*build no image*' }
        @{ Case = 'the SelfContainedZip target'; Target = 'SelfContainedZip'; Write = $true; Expect = '*build no image*' }
        @{ Case = 'a CMR project with no Dockerfile'; Target = 'CMR'; Write = $false; Expect = '*has no Dockerfile*' }
    ) {
        $path = Write-TestDockerfile -Content 'FROM public.ecr.aws/lambda/provided:al2023'
        if (-not $Write) { Remove-Item -LiteralPath $path }
        InModuleScope $script:moduleName -Parameters @{ Path = $path; Target = $Target; Expect = $Expect } {
            param($Path, $Target, $Expect)
            $finding = Test-LambdaPSImageEntrypoint -Path $Path -Target $Target
            $finding.Passed | Should -BeTrue
            $finding.Severity | Should -Be 'Information'
            $finding.Message | Should -BeLike "Not applicable:$Expect"
        }
    }
}
