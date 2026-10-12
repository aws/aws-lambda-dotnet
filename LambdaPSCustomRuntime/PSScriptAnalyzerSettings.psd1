# PSScriptAnalyzer contract for the MODULE SOURCE (Public/, Private/, Config/, the .psm1 and .psd1).
#
# This scope is held to the full default rule set with NO rule exclusions, and `build.ps1 -Task Lint`
# fails on ANY finding here, at any severity. The source is kept at zero findings, and the gate is
# worth keeping: a rule excluded here would be excluded everywhere in the
# shipping cmdlet code.
#
# Findings that are genuinely wrong are suppressed AT THE SITE with a
# [Diagnostics.CodeAnalysis.SuppressMessageAttribute] carrying a Justification, so the reason lives
# next to the code a reviewer is reading. The suppressed rules, and why (each site's Justification
# says which case it is):
#
#   PSUseOutputTypeCorrectly          A comma-wrapped return (`return , ([string[]]$x)`) emits one
#                                     object whose runtime type is the declared one, or a collected
#                                     @(...) is unrolled on emit; the rule infers Object[] from the
#                                     static shape.
#   PSShouldProcess                   The scaffolders declare SupportsShouldProcess and leave every
#                                     write to helpers that gate themselves.
#   PSUseShouldProcessForState-       New-* helpers that only build an in-memory object or
#   ChangingFunctions                 ErrorRecord change no state.
#   PSUsePSCredentialType,            -Credential takes an AWS.Tools AWSCredentials object, as on every
#   PSAvoidUsingPlainTextForPassword  AWS.Tools cmdlet, not a PSCredential or a password string.
#
# `grep -rn SuppressMessageAttribute src/` lists every site.
#
# Tests/ and Templates/ are NOT linted with this file -- they are different contexts with their own
# legitimate patterns (mockable stub signatures, bootstrap console logging). See
# PSScriptAnalyzerSettings.Relaxed.psd1.
@{
    IncludeDefaultRules = $true

    Severity = @(
        'Error'
        'Warning'
        'Information'
    )
}
