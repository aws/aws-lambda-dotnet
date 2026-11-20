function Test-LambdaPSImageEntrypoint {
    <#
    .SYNOPSIS
        Checks that a runtime or function image sets the entrypoint Lambda runs (R-Entrypoint).
    .DESCRIPTION
        Rule evaluator for the CMR and Container targets. Lambda runs the image's ENTRYPOINT, for a
        customer-managed runtime as for a function image. An image that sets none keeps
        provided.al2023's /lambda-entrypoint.sh, which exits with status 142 ("entrypoint requires the
        handler name to be the first argument") when it is started without a handler argument, as a
        customer-managed runtime is: every invoke then fails with Runtime.ExitError. The scaffolded
        Dockerfile sets ENTRYPOINT [ "/var/runtime/lambda-entrypoint.sh" ], which takes the handler
        from its first argument (a function image's CMD) or from the _HANDLER Lambda sets, and execs
        /var/runtime/bootstrap.

        Reads the project's Dockerfile and finds the ENTRYPOINT of its final stage: the last
        ENTRYPOINT after the last FROM, or, when that FROM names an earlier stage, the one that stage
        has. Instruction names match case-insensitively, as docker reads them; comment lines are
        skipped and a line ending in '\' continues on the next.

          - Passes for the exec form of the scaffold's entrypoint, ENTRYPOINT [ "/var/runtime/lambda-entrypoint.sh" ].
          - Fails when the final stage sets no ENTRYPOINT and is built FROM
            public.ecr.aws/lambda/provided, or when its ENTRYPOINT runs /lambda-entrypoint.sh.
          - Is UNVERIFIED (passes, as a warning) for any other ENTRYPOINT (a different program, or the
            shell form, which a function image's CMD does not reach), for a final stage built FROM
            another image (its ENTRYPOINT is that image's), and for a Dockerfile with an escape parser
            directive, a here-document, no FROM, or that cannot be read.

        A project without a Dockerfile passes: it has no image ENTRYPOINT to check, and
        Build-LambdaPSRuntimeImage refuses to build it. The Layer and SelfContainedZip targets build
        no image, so the rule reports a passing, not-applicable finding for them.

        Returns one finding.
    .PARAMETER Path
        The full path of the project's Dockerfile.
    .PARAMETER Target
        The deployment target the project belongs to: 'CMR' (default), 'Layer', 'SelfContainedZip',
        or 'Container'.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Finding
    .EXAMPLE
        Test-LambdaPSImageEntrypoint -Path ./rt/Dockerfile -Target CMR

        Returns a finding describing whether the runtime image sets the entrypoint Lambda runs.
    #>
    [CmdletBinding()]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Finding')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [ValidateSet('CMR', 'Layer', 'SelfContainedZip', 'Container')]
        [string]$Target = 'CMR'
    )

    $ruleId = 'R-Entrypoint'
    $entrypoint = '/var/runtime/lambda-entrypoint.sh'
    $check = "the image's ENTRYPOINT sets _HANDLER (from its first argument, or the one Lambda sets) and execs /var/runtime/bootstrap, as $entrypoint does."
    $unverified = { param([string]$Construct) New-LambdaPSUnverifiedFinding -RuleId $ruleId -Subject 'the Dockerfile' -Construct $Construct -Check $check }

    if ($Target -ne 'CMR' -and $Target -ne 'Container') {
        return New-LambdaPSFinding -RuleId $ruleId -Passed $true -Message 'Not applicable: the Layer and SelfContainedZip targets build no image, so there is no ENTRYPOINT to check.'
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return New-LambdaPSFinding -RuleId $ruleId -Passed $true -Message 'Not applicable: the project has no Dockerfile, so there is no image ENTRYPOINT to check (Build-LambdaPSRuntimeImage needs one).'
    }
    try { $text = [System.IO.File]::ReadAllText($Path) }
    catch { return & $unverified "a Dockerfile that cannot be read ($($_.Exception.Message))" }

    $lines = $text.TrimStart([char]0xFEFF) -split '\r?\n'
    # A parser directive is a '# name=value' comment before the first instruction, blank line or
    # other comment. 'escape' changes the continuation character, which this reader assumes is '\'.
    foreach ($line in $lines) {
        $directive = [regex]::Match($line, '^\s*#\s*([A-Za-z]+)\s*=\s*(\S*)\s*$')
        if (-not $directive.Success) { break }
        if ($directive.Groups[1].Value -eq 'escape' -and $directive.Groups[2].Value -ne '\') {
            return & $unverified 'an escape parser directive'
        }
    }

    # Join continued lines into instructions; comment lines inside a continuation are skipped, as
    # docker does.
    $instructions = [System.Collections.Generic.List[string]]::new()
    $current = $null
    foreach ($line in $lines) {
        if ($line -match '^\s*#') { continue }
        if ($null -eq $current -and $line -match '^\s*$') { continue }
        $continued = $line -match '\\\s*$'
        $part = if ($continued) { $line -replace '\\\s*$', '' } else { $line }
        $current = if ($null -eq $current) { $part } else { "$current $part" }
        if (-not $continued) { $instructions.Add($current.Trim()); $current = $null }
    }
    if ($null -ne $current -and $current.Trim()) { $instructions.Add($current.Trim()) }

    # Each stage's ENTRYPOINT: $null until one is set, so the stage keeps its base image's.
    $stages = @{}
    $stage = $null
    foreach ($instruction in $instructions) {
        $keyword, $arguments = $instruction -split '\s+', 2
        $keyword = $keyword.ToUpperInvariant()
        if (@('RUN', 'COPY', 'ADD') -contains $keyword -and $arguments -match '<<-?\s*["'']?[A-Za-z_]') {
            return & $unverified 'a here-document'
        }
        if ($keyword -eq 'FROM') {
            $words = @(($arguments -split '\s+') | Where-Object { $_ -and -not $_.StartsWith('--') })
            $image = if ($words.Count -gt 0) { $words[0] } else { '' }
            $name = if ($words.Count -ge 3 -and $words[1] -eq 'AS') { $words[2].ToLowerInvariant() } else { $null }
            $parent = $stages[$image.ToLowerInvariant()]
            $stage = if ($parent) { $parent.Clone() } else { @{ Base = $image; Entrypoint = $null } }
            if ($name) { $stages[$name] = $stage }
        }
        elseif ($keyword -eq 'ENTRYPOINT' -and $null -ne $stage) {
            $value = [string]$arguments
            $parsed = $null
            if ($value.StartsWith('[')) {
                try { $parsed = ConvertFrom-Json -InputObject $value -NoEnumerate -ErrorAction Stop }
                catch { $parsed = $null }
            }
            # A JSON array of strings is the exec form; anything else docker runs with /bin/sh -c.
            $isExec = $parsed -is [array] -and @($parsed | Where-Object { $_ -isnot [string] }).Count -eq 0
            $stage.Entrypoint = if ($isExec) { @{ Exec = [string[]]$parsed } } else { @{ Shell = $value } }
        }
    }

    if ($null -eq $stage) { return & $unverified 'no FROM instruction' }
    $set = $stage.Entrypoint
    if ($null -eq $set) {
        if ($stage.Base -match '^public\.ecr\.aws/lambda/provided([:@]|$)') {
            return New-LambdaPSFinding -RuleId $ruleId -Passed $false -Message ("The Dockerfile sets no ENTRYPOINT, so the image keeps provided.al2023's /lambda-entrypoint.sh. Lambda runs it without a " +
                "handler argument, and it exits with status 142 ('entrypoint requires the handler name to be the first argument'), so every invoke fails with Runtime.ExitError. " +
                "Add ENTRYPOINT [ `"$entrypoint`" ] (New-LambdaPSRuntimeProject writes $entrypoint).")
        }
        return & $unverified "no ENTRYPOINT, so the image runs the one of its base image $($stage.Base)"
    }
    $exec = $set['Exec']
    if ($null -ne $exec -and $exec.Count -eq 0) { return & $unverified 'an empty ENTRYPOINT, so the image runs its CMD as the program' }
    if ($null -ne $exec -and $exec.Count -eq 1 -and $exec[0] -ceq $entrypoint) {
        return New-LambdaPSFinding -RuleId $ruleId -Passed $true -Message "The image's ENTRYPOINT is $entrypoint, which sets the handler and execs /var/runtime/bootstrap."
    }
    $program = if ($null -ne $exec) { $exec[0] } else { ($set.Shell -split '\s+', 2)[0] }
    if ($program -ceq '/lambda-entrypoint.sh') {
        return New-LambdaPSFinding -RuleId $ruleId -Passed $false -Message ("The image's ENTRYPOINT runs /lambda-entrypoint.sh, which exits with status 142 ('entrypoint requires the handler name to be the first argument') " +
            "when Lambda starts it without a handler argument, so every invoke fails with Runtime.ExitError. Use ENTRYPOINT [ `"$entrypoint`" ].")
    }
    if ($null -eq $exec) { return & $unverified "a shell-form ENTRYPOINT ($($set.Shell)), which an image's CMD does not reach" }
    & $unverified "an ENTRYPOINT other than the scaffold's ($($exec -join ' '))"
}
