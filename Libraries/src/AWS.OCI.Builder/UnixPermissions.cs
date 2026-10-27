namespace Amazon.OCI.Builder;

/// <summary>
/// Helpers for expressing Unix file permissions the way <c>chmod</c> does — in octal — and converting them to the
/// <see cref="UnixFileMode"/> flags the layer builder uses.
/// </summary>
/// <remarks>
/// C# has no octal integer literal, so the familiar <c>0755</c> cannot be written directly (and <c>0755</c> in C#
/// would be the <em>decimal</em> 755, the wrong bits). Use <see cref="FromOctal(string)"/> with <c>"755"</c>, or
/// <see cref="FromOctal(int)"/> where each decimal digit is read as one octal permission digit (so the C# literal
/// <c>755</c> means octal 0755). Each of the three permission digits is a 0–7 value whose bits are
/// read(4)/write(2)/execute(1) for owner, group, and other respectively.
/// </remarks>
public static class UnixPermissions
{
    /// <summary>
    /// Parses an octal permission string such as <c>"755"</c>, <c>"0644"</c>, or <c>"0o700"</c> into a
    /// <see cref="UnixFileMode"/>. A leading <c>0</c> or <c>0o</c> prefix is accepted and ignored. Only the nine
    /// standard permission bits are supported; set-uid/set-gid/sticky bits (a fourth leading digit other than 0)
    /// are rejected.
    /// </summary>
    /// <param name="octal">Three octal digits, optionally prefixed with <c>0</c> or <c>0o</c>.</param>
    /// <exception cref="ArgumentException">The value is null/empty or not valid octal permission notation.</exception>
    public static UnixFileMode FromOctal(string octal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(octal);

        string digits = octal.Trim();
        // Accept a leading "0o"/"0O" prefix, or a single leading "0".
        if (digits.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 4 && digits[0] == '0')
        {
            digits = digits[1..];
        }

        if (digits.Length != 3)
        {
            throw new ArgumentException(
                $"'{octal}' is not a valid octal permission. Use three octal digits, e.g. \"755\" or \"0644\" " +
                "(set-uid/set-gid/sticky bits are not supported).",
                nameof(octal));
        }

        int value = 0;
        foreach (char c in digits)
        {
            if (c is < '0' or > '7')
            {
                throw new ArgumentException(
                    $"'{octal}' contains '{c}', which is not an octal digit (0–7).", nameof(octal));
            }
            value = (value << 3) | (c - '0');
        }

        // The nine low bits map directly onto UnixFileMode, which uses the same rwx-per-triad layout.
        return (UnixFileMode)value;
    }

    /// <summary>
    /// Parses a permission value whose <em>decimal</em> digits are read as octal — so the C# literal <c>755</c>
    /// means octal <c>0755</c>, and <c>644</c> means <c>0644</c>. This lets callers write the number they would
    /// type into <c>chmod</c> without a string. Equivalent to <c>FromOctal(value.ToString())</c>.
    /// </summary>
    /// <param name="octalDigits">A value in the range 0–777 whose decimal digits are each 0–7.</param>
    /// <exception cref="ArgumentException">The value is out of range or has a digit outside 0–7.</exception>
    public static UnixFileMode FromOctal(int octalDigits)
    {
        if (octalDigits is < 0 or > 777)
        {
            throw new ArgumentException(
                $"{octalDigits} is out of range; pass the chmod-style digits, e.g. 755 or 644 (0–777).",
                nameof(octalDigits));
        }
        return FromOctal(octalDigits.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
    }
}
