# PSScriptAnalyzer contract for Tests/ and Templates/ -- code that is NOT shipping cmdlet source and
# is legitimately written to different rules. `build.ps1 -Task Lint` reports findings from this scope
# but does not fail on them (it still fails on Error and ParseError severity).
#
# Each exclusion below is excluded because the pattern it flags is CORRECT in these directories. None
# of these rules is excluded for the module source, which is linted with the strict
# PSScriptAnalyzerSettings.psd1 instead.
@{
    IncludeDefaultRules = $true

    # ParseError is listed so a file that does not parse is reported: it yields only ParseError records.
    Severity = @(
        'ParseError'
        'Error'
        'Warning'
        'Information'
    )

    ExcludeRules = @(
        # Templates/: the runtime (runtime.ps1 and the runtime interface client modules) logs to the
        # console on purpose. Lambda captures stdout/stderr into CloudWatch Logs, and Write-Host is how
        # the runtime reports progress before any structured logging exists. (All in Templates/. The
        # cmdlet source has none, and the strict settings keep it that way.)
        'PSAvoidUsingWriteHost'

        # Tests/: many tests declare a stub such as `function New-LMCRuntime { param($Name, $Region, ...) }`
        # purely to give Pester a command with the right parameter names to mock. The parameters are
        # unused BY DESIGN -- the stub body is empty and the mock replaces it. (All in Tests/.)
        'PSReviewUnusedParameter'

        # Tests/ and Templates/: plural nouns in helper names that are not exported cmdlets.
        'PSUseSingularNouns'
    )
}
