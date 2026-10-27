using Amazon.OCI.Builder;
using Xunit;

namespace AWS.OCI.Builder.Tests;

public class UnixPermissionsTests
{
    private const UnixFileMode Mode0755 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    private const UnixFileMode Mode0644 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    private const UnixFileMode Mode0600 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [Theory]
    [InlineData("755")]
    [InlineData("0755")]
    [InlineData("0o755")]
    [InlineData("0O755")]
    [InlineData(" 755 ")]
    public void FromOctal_string_parses_755(string input)
        => Assert.Equal(Mode0755, UnixPermissions.FromOctal(input));

    [Theory]
    [InlineData("644", 0b110_100_100)]
    [InlineData("600", 0b110_000_000)]
    [InlineData("777", 0b111_111_111)]
    [InlineData("000", 0)]
    public void FromOctal_string_maps_bits_correctly(string input, int expectedBits)
        => Assert.Equal((UnixFileMode)expectedBits, UnixPermissions.FromOctal(input));

    [Fact]
    public void FromOctal_known_values()
    {
        Assert.Equal(Mode0644, UnixPermissions.FromOctal("644"));
        Assert.Equal(Mode0600, UnixPermissions.FromOctal("600"));
    }

    [Theory]
    [InlineData(755)]
    [InlineData(644)]
    [InlineData(600)]
    public void FromOctal_int_matches_string(int value)
        => Assert.Equal(UnixPermissions.FromOctal(value.ToString()), UnixPermissions.FromOctal(value));

    [Fact]
    public void FromOctal_int_literal_755_is_octal_0755()
        => Assert.Equal(Mode0755, UnixPermissions.FromOctal(755));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("75")]        // too few digits
    [InlineData("7555")]      // too many (non-zero leading)
    [InlineData("788")]       // 8 is not an octal digit
    [InlineData("7x5")]       // non-digit
    public void FromOctal_string_rejects_invalid(string input)
        => Assert.Throws<ArgumentException>(() => UnixPermissions.FromOctal(input));

    [Theory]
    [InlineData(-1)]
    [InlineData(778)]         // digit 8 out of octal range
    [InlineData(800)]         // > 777
    [InlineData(1000)]        // out of range
    public void FromOctal_int_rejects_invalid(int value)
        => Assert.Throws<ArgumentException>(() => UnixPermissions.FromOctal(value));

    [Fact]
    public void FromOctal_result_is_usable_as_an_explicit_entry_mode()
    {
        // Confirms the helper's output plugs straight into the FileManifest mode overload.
        var files = new FileManifest();
        files.Add("/some/local/path", "/app/tool", UnixPermissions.FromOctal("750"));

        Assert.Single(files.Entries);
        Assert.Equal(UnixPermissions.FromOctal("750"), files.Entries[0].Mode);
    }
}
