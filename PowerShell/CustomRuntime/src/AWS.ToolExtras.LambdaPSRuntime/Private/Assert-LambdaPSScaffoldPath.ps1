function Assert-LambdaPSScaffoldPath {
    <#
    .SYNOPSIS
        Checks that a directory can be scaffolded into, and returns what is already in it.
    .DESCRIPTION
        The up-front checks shared by New-LambdaPSRuntimeProject and New-LambdaPSFunctionProject. They
        run before anything is downloaded or written, so a failure never leaves a half-scaffolded
        directory behind:

          - -Path must not be blank (a parameter validation error).
          - -Path must not be a file (TargetIsFile), even with -Force. Get-ChildItem on a file returns
            the file itself, so without this check it would be reported as a non-empty directory and
            -Force would delete it.
          - On Linux and macOS, an existing -Path (after following a link) must be owned by the
            current user, or by root when no group or other user can write to it
            (TargetOwnedByAnotherUser), even with -Force. In a shared folder
            such as /tmp another user could create the directory first and change the project while
            it is written or built. The check is skipped when the caller is root.
          - A non-empty -Path needs -Force (TargetNotEmpty).

        Returns the existing children of -Path (empty when it does not exist or is empty), which
        Invoke-LambdaPSProjectScaffold removes before writing. Writes nothing.
    .PARAMETER Path
        The directory the project will be scaffolded into.
    .PARAMETER Force
        Whether the caller allows a non-empty directory to be replaced.
    .OUTPUTS
        System.Object[]
    .EXAMPLE
        $existing = Assert-LambdaPSScaffoldPath -Path ./rt -Force:$Force

        Throws if ./rt cannot be scaffolded into; otherwise returns its current contents.
    #>
    [CmdletBinding()]
    [OutputType([object[]])]
    # PSUseOutputTypeCorrectly: the return is comma-wrapped ('return , $existing') so an empty or
    # one-item array reaches the caller as an array, which is what OutputType declares.
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseOutputTypeCorrectly', '',
        Justification = 'Comma-wrapped return emits a single object[]; the declared OutputType is correct at runtime.')]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter()]
        [switch]$Force
    )

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.IOException]::new("Target '$Path' is a file, not a directory. Choose a directory path for the project."),
            'TargetIsFile', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path)
    }

    if (-not $IsWindows -and (Test-Path -LiteralPath $Path -PathType Container)) {
        $directory = Get-Item -LiteralPath $Path -Force
        if ($directory.LinkTarget) { $directory = Get-Item -LiteralPath $directory.ResolveLinkTarget($true).FullName -Force }
        # Root can change any directory already, so a root-owned target adds no risk unless other
        # users can write to it too (/tmp, /var/tmp, a shared drop folder). A caller running as root
        # (sudo, a CI container) is not refused its own user's checkout.
        $currentUserId = [long](& id -u)
        $ownerId = [long]$directory.UnixStat.UserId
        $sharedWrite = [System.IO.UnixFileMode]::GroupWrite -bor [System.IO.UnixFileMode]::OtherWrite
        $safeRootOwned = $ownerId -eq 0 -and ($directory.UnixFileMode -band $sharedWrite) -eq 0
        if ($currentUserId -ne 0 -and $ownerId -ne $currentUserId -and -not $safeRootOwned) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.UnauthorizedAccessException]::new("Target '$Path' is owned by another user ($($directory.User)). Choose a directory you own: in a shared folder such as /tmp, another user could change the project while it is written or built."),
                'TargetOwnedByAnotherUser', [System.Management.Automation.ErrorCategory]::PermissionDenied, $Path)
        }
    }

    $existing = @()
    if (Test-Path -LiteralPath $Path) {
        $existing = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)
        if ($existing.Count -gt 0 -and -not $Force) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.IOException]::new("Target '$Path' already exists and is not empty. Use -Force to replace it."),
                'TargetNotEmpty', [System.Management.Automation.ErrorCategory]::ResourceExists, $Path)
        }
    }

    return , $existing
}
