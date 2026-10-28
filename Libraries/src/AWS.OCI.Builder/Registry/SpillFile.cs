namespace AWS.OCI.Builder.Registry;

/// <summary>
/// Helpers for creating the temporary "spill" files this library writes layer/blob bytes to, with owner-only
/// access on Unix.
/// </summary>
/// <remarks>
/// These files land in the shared system temp directory. With the usual umask (022) a newly created file is
/// world-readable (0644), so another local user could read a private image layer — including files a manifest
/// mapped as 0600 — for the lifetime of the handle. The permissions recorded <em>inside</em> the tar do not
/// protect the outer spill file. Creating the file 0600 from the start (atomically, via
/// <see cref="FileStreamOptions.UnixCreateMode"/>) closes that window. On Windows the mode is not applicable and
/// is left unset; default ACLs already restrict the per-user temp directory.
/// </remarks>
internal static class SpillFile
{
    // Owner read+write only (0600).
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>
    /// Opens a new file for writing with owner-only permissions on Unix. Fails if the file already exists
    /// (<see cref="FileMode.CreateNew"/>), matching the unique-temp-name contract the callers rely on.
    /// </summary>
    public static FileStream CreateWrite(string path, int bufferSize = 81920)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = bufferSize,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnly;
        }
        return new FileStream(path, options);
    }
}
