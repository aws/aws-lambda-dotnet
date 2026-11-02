function Assert-LambdaPSScaffoldPath {
    <#
    .SYNOPSIS
        Checks that a directory can be scaffolded into, and returns what is already in it.
    .DESCRIPTION
        The up-front checks shared by New-LambdaPSRuntimeProject and New-LambdaPSFunctionProject. They
        run before anything is downloaded or written, so a failure never leaves a half-scaffolded
        directory behind:

          - -Path must not be blank (a parameter validation error).
          - -Path must not be a file, a link to a file, or a link to nothing (TargetIsFile), even with -Force.
            Get-ChildItem on a file returns the file itself, so without this check it would be
            reported as a non-empty directory and -Force would delete it.
          - On Linux and macOS, an existing -Path (after following a link) must be owned by the
            current user, or by root when no group or other user can write to it
            (TargetOwnedByAnotherUser), and must not be writable by every user without the sticky bit
            (TargetWritableByOthers), even with -Force. In a shared folder
            such as /tmp another user could create the directory first and change the project while
            it is written or built. The check is skipped when the caller is root.
          - An existing -Path must be listable (TargetNotReadable), even with -Force: a folder you
            cannot list could hold files the scaffold would write over.
          - A non-empty -Path needs -Force (TargetNotEmpty).
          - A non-empty -Path that -Force would empty must not be a symbolic link or a junction
            (TargetIsLink): the delete would empty the folder it points to, which the -WhatIf and
            -Confirm messages do not name. Nor may it be, once links are resolved, the current
            location, your home folder, the root of a drive, or a folder that contains one of them
            (TargetProtected): replacing it would delete the folder you are working in, or your files.

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
        # Test-Path calls a link to nothing a leaf: name the link, not a file. Whether the link reaches a
        # file is read from its final target: File.Exists is true for a link to nothing too, and a link
        # loop (which ResolveLinkTarget throws on) reaches nothing.
        $link = (Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue).LinkTarget
        $reachesFile = $false
        if ($link) {
            try { $reachesFile = [System.IO.FileInfo]::new((Resolve-LambdaPSPath -Path $Path)).ResolveLinkTarget($true).Exists }
            catch [System.IO.IOException] { $reachesFile = $false }
        }
        $message = if ($link -and -not $reachesFile) { "Target '$Path' is a link to '$link', which does not exist. Choose a directory path for the project." }
        elseif ($link) { "Target '$Path' is a link to the file '$link', not a directory. Choose a directory path for the project." }
        else { "Target '$Path' is a file, not a directory. Choose a directory path for the project." }
        # `throw`, not $PSCmdlet.ThrowTerminatingError: the public cmdlet that called this rethrows the error with
        # its own $PSCmdlet, and an error raised through this helper's $PSCmdlet would also reach the
        # caller's -ErrorVariable naming this internal helper.
        throw [System.Management.Automation.ErrorRecord]::new(
            [System.IO.IOException]::new($message),
            'TargetIsFile', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path)
    }

    if (-not $IsWindows -and (Test-Path -LiteralPath $Path -PathType Container)) {
        $directory = Get-Item -LiteralPath $Path -Force
        if ($directory.LinkTarget) { $directory = Get-Item -LiteralPath $directory.ResolveLinkTarget($true).FullName -Force }
        # Root can change any directory already, so a root-owned target adds no risk unless other
        # users can write to it too (/tmp, /var/tmp, a shared drop folder). A caller running as root
        # (sudo, a CI container) is not refused its own user's checkout.
        # Run by its resolved path, so a function or alias named 'id' in the session cannot answer.
        $idCommand = Get-Command -Name 'id' -CommandType Application -ErrorAction Stop | Select-Object -First 1
        $currentUserId = [long](& $idCommand.Source -u)
        $ownerId = [long]$directory.UnixStat.UserId
        $sharedWrite = [System.IO.UnixFileMode]::GroupWrite -bor [System.IO.UnixFileMode]::OtherWrite
        $safeRootOwned = $ownerId -eq 0 -and ($directory.UnixFileMode -band $sharedWrite) -eq 0
        if ($currentUserId -ne 0 -and $ownerId -ne $currentUserId -and -not $safeRootOwned) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.UnauthorizedAccessException]::new("Target '$Path' is owned by another user ($($directory.User)). Choose a directory you own: in a shared folder such as /tmp, another user could change the project while it is written or built."),
                'TargetOwnedByAnotherUser', [System.Management.Automation.ErrorCategory]::PermissionDenied, $Path)
        }
        # A directory every user can write to is shared whoever owns it: another user could change the
        # project in it too. With the sticky bit (1777) other users cannot replace your files, so it is
        # accepted. A group-writable one you own is accepted too, since a user-private group
        # (umask 002) makes that the default for your own folders.
        $mode = $directory.UnixFileMode
        if ($currentUserId -ne 0 -and $ownerId -eq $currentUserId -and ($mode -band [System.IO.UnixFileMode]::OtherWrite) -ne 0 -and ($mode -band [System.IO.UnixFileMode]::StickyBit) -eq 0) {
            $octal = [System.Convert]::ToString([int]$mode, 8)
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.UnauthorizedAccessException]::new("Target '$Path' has mode $octal, so every user can write to it and could change the project while it is written or built. Remove the others' write permission (chmod o-w '$Path'), or choose a directory only you can write to."),
                'TargetWritableByOthers', [System.Management.Automation.ErrorCategory]::PermissionDenied, $Path)
        }
    }

    $existing = @()
    if (Test-Path -LiteralPath $Path) {
        try { $existing = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction Stop) }
        catch {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.UnauthorizedAccessException]::new("Target '$Path' cannot be listed, so it cannot be checked for files the scaffold would write over: $($_.Exception.Message) Choose a directory you can read.", $_.Exception),
                'TargetNotReadable', [System.Management.Automation.ErrorCategory]::PermissionDenied, $Path)
        }
        if ($existing.Count -gt 0 -and -not $Force) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.IOException]::new("Target '$Path' already exists and is not empty. Use -Force to replace it."),
                'TargetNotEmpty', [System.Management.Automation.ErrorCategory]::ResourceExists, $Path)
        }
    }

    # What -Force would empty. Checked only when there is something to delete.
    if ($existing.Count -gt 0) {
        $linkTarget = (Get-Item -LiteralPath $Path -Force).LinkTarget
        if ($linkTarget) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.IOException]::new("Target '$Path' is a link to '$linkTarget'. -Force would delete the contents of '$linkTarget', so a link is not replaced. Pass the folder itself as -Path if it is the one to replace."),
                'TargetIsLink', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path)
        }
        # Compared with links resolved, so a path that reaches one of these another way is refused too.
        # Windows and macOS file systems ignore case by default.
        $comparison = if ($IsLinux) { [System.StringComparison]::Ordinal } else { [System.StringComparison]::OrdinalIgnoreCase }
        $separator = [System.IO.Path]::DirectorySeparatorChar
        $target = Get-LambdaPSRealPath -Path (Resolve-LambdaPSPath -Path $Path)
        $protected = [ordered]@{ 'the current location' = (Get-Location -PSProvider FileSystem).ProviderPath }
        if ($HOME) { $protected['your home folder'] = $HOME }
        $reason = $null
        foreach ($name in @($protected.Keys)) {
            $real = Get-LambdaPSRealPath -Path $protected[$name]
            if ($target.Equals($real, $comparison)) { $reason = "is $name"; break }
            if ($real.StartsWith($target.TrimEnd($separator) + $separator, $comparison)) { $reason = "contains $name ('$real')"; break }
        }
        if (-not $reason -and $target.Equals([System.IO.Path]::GetPathRoot($target), $comparison)) { $reason = 'is the root of a drive' }
        if ($reason) {
            throw [System.Management.Automation.ErrorRecord]::new(
                [System.IO.IOException]::new("Target '$Path' $reason, so -Force will not delete its contents. Choose a folder of its own for the project."),
                'TargetProtected', [System.Management.Automation.ErrorCategory]::InvalidArgument, $Path)
        }
    }

    return , $existing
}
