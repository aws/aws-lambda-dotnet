function Merge-LambdaPSEcrPullPolicy {
    <#
    .SYNOPSIS
        Adds the Lambda image-pull grant to an ECR repository policy, preserving everything else.
    .DESCRIPTION
        Pure policy transform used by Initialize-LambdaPSEcrRepository. Lambda needs ecr:BatchGetImage
        and ecr:GetDownloadUrlForLayer on the repository to pull a runtime image, or
        New-LMCRuntimeVersion fails with "Lambda does not have permission to access the ECR image".

        The statement this adds is scoped to one runtime: aws:SourceAccount must be -AccountId and
        aws:SourceArn must be -RuntimeArn. Lambda sends both keys when it pulls a runtime image, at
        publish and at a function's cold start, so the grant lets Lambda pull the image for that
        runtime only, not for another account's or another runtime's request.

        Set-ECRRepositoryPolicy REPLACES the whole policy, so writing a fixed policy would silently
        delete every other grant on the repository -- cross-account pulls, CI roles, scanners. This
        function instead returns the existing policy with one statement appended, or $null when no
        write is needed at all:

          * No existing policy        -> a new policy containing only the scoped Lambda statement.
          * Already grants this pull  -> $null (nothing to change). An Allow statement for the
                                         lambda.amazonaws.com service principal whose actions include
                                         both required actions, 'ecr:*' or '*' covers the pull when
                                         it has no Condition, or when this pull meets every condition
                                         in it (see Test-LambdaPSPullConditionMet). Such a statement
                                         is left as it is, with a warning unless one condition key
                                         pins the account: all of its aws:SourceAccount values, or
                                         the account components of all of its aws:SourceArn values,
                                         are free of * and ?. A key under an ...IfExists operator
                                         does not pin it: a request without the key also meets it.
          * Anything else             -> the existing statements unchanged, plus the scoped statement.
                                         That includes a Lambda grant conditioned on something else
                                         (another runtime or account, a negated operator, a key this
                                         function does not evaluate): adding a grant scoped to this
                                         runtime does not widen what it allows. Existing statements
                                         are written back with the same keys, order and values.

        Deny statements are not evaluated. A Deny aimed at Lambda still blocks the pull, so publishing
        the version fails as it does with no grant.

        Performs no AWS call and no disk access.
    .PARAMETER PolicyText
        The repository's current policy JSON, or $null/empty when it has none.
    .PARAMETER AccountId
        The account that owns the runtime: the aws:SourceAccount the grant requires.
    .PARAMETER RuntimeArn
        The runtime's ARN (arn:<partition>:lambda:<region>:<account>:runtime/<name>): the
        aws:SourceArn the grant requires.
    .OUTPUTS
        System.String
    .EXAMPLE
        Merge-LambdaPSEcrPullPolicy -PolicyText $existing -AccountId 111122223333 -RuntimeArn arn:aws:lambda:us-east-1:111122223333:runtime/my-ps

        Returns the policy JSON to write, or $null when the repository already grants that pull.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter()]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$PolicyText,

        [Parameter(Mandatory)]
        [ValidatePattern('^[0-9]{12}\z')]
        [string]$AccountId,

        [Parameter(Mandatory)]
        # Options = 'None': ValidatePattern ignores case by default, and ARN partitions and regions are lowercase.
        [ValidatePattern('^arn:[a-z0-9-]+:lambda:[a-z0-9-]+:[0-9]{12}:runtime/.+\z', Options = 'None')]
        [string]$RuntimeArn
    )

    $requiredActions = @('ecr:BatchGetImage', 'ecr:GetDownloadUrlForLayer')
    # StringEquals for both keys: the ARN is compared literally, so a * or ? in a runtime name cannot
    # widen the grant to other runtimes.
    $lambdaStatement = [ordered]@{
        Sid       = 'LambdaPSRuntimeImagePull'
        Effect    = 'Allow'
        Principal = [ordered]@{ Service = 'lambda.amazonaws.com' }
        Action    = $requiredActions
        Condition = [ordered]@{
            StringEquals = [ordered]@{ 'aws:SourceAccount' = $AccountId; 'aws:SourceArn' = $RuntimeArn }
        }
    }

    if ([string]::IsNullOrWhiteSpace($PolicyText)) {
        return ([ordered]@{ Version = '2012-10-17'; Statement = @($lambdaStatement) } | ConvertTo-Json -Depth 32 -Compress)
    }

    # Read the statements to decide; -AsHashtable keeps unknown keys and nesting. The policy written
    # back is edited as JSON nodes instead (below), because ConvertFrom-Json turns date-like strings
    # into DateTime values and ConvertTo-Json would write them back changed.
    $policy = $PolicyText | ConvertFrom-Json -AsHashtable -ErrorAction Stop

    # 'Statement' may be a single object rather than an array; both are valid policy grammar.
    $statements = @($policy['Statement'] | Where-Object { $null -ne $_ })

    $unmetGrants = 0
    foreach ($statement in $statements) {
        if ([string]$statement['Effect'] -ne 'Allow') { continue }

        $principal = $statement['Principal']
        $services = if ($principal -is [System.Collections.IDictionary]) { @($principal['Service']) } else { @() }
        if ($services -notcontains 'lambda.amazonaws.com') { continue }

        $actions = @($statement['Action'])
        $covers = ($actions -contains '*') -or ($actions -contains 'ecr:*') -or
            (@($requiredActions | Where-Object { $actions -notcontains $_ }).Count -eq 0)
        if (-not $covers) { continue }

        if (Test-LambdaPSPullConditionMet -Condition $statement['Condition'] -AccountId $AccountId -RuntimeArn $RuntimeArn) {
            # A met Condition names only aws:SourceArn and aws:SourceAccount. Unless one of them pins the
            # account (every value free of * and ?; for an ARN, its account component), the grant is open
            # to any account's Lambda (a confused deputy). It is not edited, but say so. An ...IfExists
            # operator is also met by a request without the key, so its keys pin nothing.
            $condition = $statement['Condition']
            $pinned = $false
            if ($condition -is [System.Collections.IDictionary]) {
                foreach ($operator in $condition.Keys) {
                    if (([string]$operator) -like '*IfExists') { continue }
                    $block = $condition[$operator]
                    foreach ($key in $block.Keys) {
                        $accounts = @($block[$key] | ForEach-Object {
                                $value = [string]$_
                                if (([string]$key) -eq 'aws:SourceArn') {
                                    $parts = $value.Split(':', 6)
                                    if ($parts.Count -eq 6) { $parts[4] } else { '*' }
                                }
                                else { $value }
                            })
                        if ($accounts.Count -gt 0 -and @($accounts | Where-Object { $_ -match '[*?]' }).Count -eq 0) { $pinned = $true }
                    }
                }
            }
            if (-not $pinned) {
                $sid = if ($statement['Sid']) { " '$($statement['Sid'])'" } else { '' }
                # Suggest the account-wide pattern too: other runtimes may share the repository.
                $accountRuntimes = $RuntimeArn -replace ':runtime/.+\z', ':runtime/*'
                Write-Warning ("The repository policy statement$sid lets any account's Lambda functions pull its images. " +
                    "Scope it with aws:SourceAccount '$AccountId' plus an aws:SourceArn of this runtime ('$RuntimeArn'), " +
                    "or '$accountRuntimes' with ArnLike to allow every runtime in the account.")
            }
            return $null
        }
        $unmetGrants++
    }

    if ($unmetGrants -gt 0) {
        $grants, $was, $them = if ($unmetGrants -eq 1) { 'grant', 'was', 'it' } else { 'grants', 'were', 'them' }
        Write-Verbose ("$unmetGrants Lambda image-pull $grants with a Condition that this pull does not meet " +
            "$was left unchanged; a grant scoped to '$RuntimeArn' is added beside $them.")
    }

    # Keep the Sid unique so the policy stays valid if a statement of that name already exists.
    $existingSids = @($statements | ForEach-Object { [string]$_['Sid'] })
    $sid = $lambdaStatement.Sid
    for ($n = 2; $existingSids -contains $sid; $n++) { $sid = "$($lambdaStatement.Sid)$n" }
    $lambdaStatement.Sid = $sid

    # Append to the policy as parsed JSON nodes, so every existing statement is written back with the
    # same keys, key order and string values.
    $document = [System.Text.Json.Nodes.JsonNode]::Parse($PolicyText)
    $existing = $document['Statement']
    if ($existing -isnot [System.Text.Json.Nodes.JsonArray]) {
        $statementArray = [System.Text.Json.Nodes.JsonArray]::new()
        if ($null -ne $existing) {
            $null = $document.Remove('Statement')
            $statementArray.Add($existing)
        }
        $document['Statement'] = $statementArray
        $existing = $statementArray
    }
    $existing.Add([System.Text.Json.Nodes.JsonNode]::Parse(($lambdaStatement | ConvertTo-Json -Depth 32 -Compress)))
    if ($null -eq $document['Version']) { $document['Version'] = '2012-10-17' }

    # The relaxed encoder writes characters such as + and < as they are rather than as \u escapes.
    $options = [System.Text.Json.JsonSerializerOptions]::new()
    $options.Encoder = [System.Text.Encodings.Web.JavaScriptEncoder]::UnsafeRelaxedJsonEscaping
    return $document.ToJsonString($options)
}
