function Test-LambdaPSPullConditionMet {
    <#
    .SYNOPSIS
        Tells whether Lambda's pull of a runtime image meets a repository policy statement's Condition.
    .DESCRIPTION
        Used by Merge-LambdaPSEcrPullPolicy to decide whether an existing Lambda grant already allows
        the pull, so that it need not add one. Lambda's pull carries aws:SourceAccount (the runtime's
        account) and aws:SourceArn (the runtime's ARN). A Condition is met only when every operator
        in it is one this function evaluates and every key it names is one of those two, with a value
        that matches:

          StringEquals                     exact match
          StringLike                       match with * and ?
          ArnEquals, ArnLike               aws:SourceArn only; each of the six colon-delimited ARN
                                           components matched on its own with * and ?, as IAM does
          (each optionally ...IfExists)

        Anything else -- a negated operator such as ArnNotLike, another condition key such as
        aws:PrincipalOrgID, or a different account or runtime -- returns $false, so the caller adds its
        own grant rather than relying on one the pull may not meet. Condition keys are
        case-insensitive; values are case-sensitive, as IAM compares them. A statement with no Condition
        is met. In a StringLike or Arn* value only * and ? are wildcards, as in IAM; [ and ] are
        literal.
    .PARAMETER Condition
        The statement's Condition block (a dictionary of operator to key/value dictionary), or $null.
    .PARAMETER AccountId
        The aws:SourceAccount of the pull.
    .PARAMETER RuntimeArn
        The aws:SourceArn of the pull.
    .OUTPUTS
        System.Boolean
    .EXAMPLE
        Test-LambdaPSPullConditionMet -Condition $statement['Condition'] -AccountId 111122223333 -RuntimeArn $arn

        Returns $true when Lambda's pull for that runtime meets the statement's Condition.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [AllowNull()]
        [object]$Condition,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$AccountId,

        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$RuntimeArn
    )

    if ($null -eq $Condition) { return $true }
    if ($Condition -isnot [System.Collections.IDictionary]) { return $false }

    $request = @{ 'aws:sourceaccount' = $AccountId; 'aws:sourcearn' = $RuntimeArn }
    # IAM's wildcards are * and ? only, so escape the rest (-clike also reads [...]).
    $iamLike = {
        param([string]$Value, [string]$Pattern)
        $Value -clike [System.Management.Automation.WildcardPattern]::Escape($Pattern).Replace('`*', '*').Replace('`?', '?')
    }

    foreach ($operator in $Condition.Keys) {
        $block = $Condition[$operator]
        $name = ([string]$operator) -replace 'IfExists$', ''
        $wildcard = switch -Exact ($name) {
            'StringEquals' { $false }
            'ArnEquals' { $true }
            'StringLike' { $true }
            'ArnLike' { $true }
            default { $null }
        }
        if ($null -eq $wildcard -or $block -isnot [System.Collections.IDictionary]) { return $false }
        $isArnOperator = $name -like 'Arn*'

        foreach ($key in $block.Keys) {
            $keyName = ([string]$key).ToLowerInvariant()
            $actual = $request[$keyName]
            if ($null -eq $actual) { return $false }
            # aws:SourceAccount is not an ARN, so an Arn* operator on it is not one this function evaluates.
            if ($isArnOperator -and $keyName -ne 'aws:sourcearn') { return $false }
            # A list of values is met when any one matches.
            $matched = @($block[$key] | Where-Object {
                    if ($isArnOperator) {
                        # IAM matches an ARN one component at a time, so a * does not cross a colon.
                        # The sixth component (the resource) keeps any colons it holds.
                        $actualParts = $actual.Split(':', 6)
                        $patternParts = ([string]$_).Split(':', 6)
                        if ($actualParts.Count -ne 6 -or $patternParts.Count -ne 6) { return $false }
                        for ($i = 0; $i -lt 6; $i++) {
                            if (-not (& $iamLike $actualParts[$i] $patternParts[$i])) { return $false }
                        }
                        $true
                    }
                    elseif ($wildcard) { & $iamLike $actual ([string]$_) }
                    else { $actual -ceq [string]$_ }
                }).Count -gt 0
            if (-not $matched) { return $false }
        }
    }
    return $true
}
