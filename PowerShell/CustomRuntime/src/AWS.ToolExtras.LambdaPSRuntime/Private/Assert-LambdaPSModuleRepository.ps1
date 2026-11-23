function Assert-LambdaPSModuleRepository {
    <#
    .SYNOPSIS
        Stops with ModuleRepositoryNotFound when a -ModuleRepository name is not a registered
        PSResourceGet repository.
    .DESCRIPTION
        Accepts what Save-PSResource -Repository accepts: names, compared ignoring case, or patterns
        whose only wildcard is '*' ('Internal*'), each of which must match at least one registered
        repository, matched with -like as PSResourceGet resolves them. '?' and '[' (which PSResourceGet
        discards), a backtick (which it reads as an escape) and a pattern mixed with plain names (which
        it refuses) are refused here too. Anything else fails at once, instead of a download searching
        somewhere else or a run succeeding without using it. Reads the local repository registration
        only; makes no network call and writes nothing. When PSResourceGet is not available, nothing
        can be checked and the download itself reports ModuleInstallerNotFound.
    .PARAMETER Name
        The repository names or wildcard patterns to check.
    .PARAMETER InstallerDefault
        The names are the AWS.Tools.Installer default (PSGallery), not names the user passed: the
        message then says so and suggests -ModuleRepository instead.
    .OUTPUTS
        None.
    .EXAMPLE
        Assert-LambdaPSModuleRepository -Name 'InternalMirror'

        Returns nothing when InternalMirror is registered; otherwise throws ModuleRepositoryNotFound.
    #>
    [CmdletBinding()]
    [OutputType([void])]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string[]]$Name,

        [Parameter()]
        [switch]$InstallerDefault
    )

    if ($null -eq (Get-Command 'Get-PSResourceRepository' -ErrorAction SilentlyContinue)) {
        return
    }
    $registered = @(Get-PSResourceRepository -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $known = if ($registered.Count -gt 0) { $registered -join ', ' } else { 'none' }

    # Only '*' is a wildcard, as in Save-PSResource -Repository: it discards a name with '?' or '[', and
    # reads a backtick as an escape. A ']' on its own is an ordinary character there.
    $unsupported = @($Name | Where-Object { $_.IndexOfAny([char[]]@('?', '[', '`')) -ge 0 })
    $patterns = @($Name | Where-Object { $_.Contains('*') })
    $reason = if ($unsupported.Count -gt 0) {
        "-ModuleRepository $(@($unsupported | ForEach-Object { "'$_'" }) -join ', ') uses '?', '[' or a " +
        "backtick, which PSResourceGet does not support in a repository name: only '*' is a wildcard."
    }
    elseif ($patterns.Count -gt 0 -and $patterns.Count -lt $Name.Count) {
        '-ModuleRepository mixes a wildcard pattern with plain names, which PSResourceGet does not ' +
        'support: name the repositories, or give wildcard patterns only.'
    }
    if ($reason) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error
        # with its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.ArgumentException]::new("$reason (registered: $known)", 'ModuleRepository'),
            'ModuleRepositoryNotFound', [System.Management.Automation.ErrorCategory]::InvalidArgument, [string[]]$Name)
    }

    $unknown = @(foreach ($pattern in $Name) {
            # Plain -like, as PSResourceGet resolves names: with '?', '[' and backticks refused, '*' is the
            # only wildcard left.
            if (@($registered | Where-Object { $_ -like $pattern }).Count -eq 0) { $pattern }
        })
    if ($unknown.Count -eq 0) {
        return
    }

    # A pattern mixed with names is refused above, so the unknown entries are all names or all patterns.
    $quoted = @($unknown | ForEach-Object { "'$_'" }) -join ', '
    $message = if ($InstallerDefault) {
        'AWS.Tools.Installer is downloaded from PSGallery when -ModuleRepository is not given, and ' +
        "PSGallery is not a registered PSResourceGet repository (registered: $known). Register it " +
        '(Register-PSResourceRepository -PSGallery), or pass -ModuleRepository with the repository ' +
        'that serves it (an internal mirror).'
    }
    elseif ($patterns.Count -gt 0) {
        $subject = if ($unknown.Count -eq 1) { "pattern $quoted matches" } else { "patterns $quoted match" }
        "-ModuleRepository $subject no registered PSResourceGet repository (registered: $known). " +
        'Register a matching repository with Register-PSResourceRepository, or give a pattern that ' +
        'matches a registered one.'
    }
    elseif ($unknown.Count -eq 1) {
        "-ModuleRepository names $quoted, which is not a registered PSResourceGet repository " +
        "(registered: $known). Register it with Register-PSResourceRepository, or name a registered one."
    }
    else {
        "-ModuleRepository names $quoted, which are not registered PSResourceGet repositories " +
        "(registered: $known). Register them with Register-PSResourceRepository, or name registered ones."
    }
    $target = if ($unknown.Count -eq 1) { $unknown[0] } else { [string[]]$unknown }
    throw [System.Management.Automation.ErrorRecord]::new(
        [System.ArgumentException]::new($message, 'ModuleRepository'),
        'ModuleRepositoryNotFound', [System.Management.Automation.ErrorCategory]::ObjectNotFound, $target)
}
