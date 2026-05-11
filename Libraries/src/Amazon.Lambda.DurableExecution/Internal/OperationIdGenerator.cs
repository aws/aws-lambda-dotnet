using System.Security.Cryptography;
using System.Text;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Generates deterministic operation IDs for durable operations. Each call
/// increments an internal counter and SHA-256 hashes <c>"&lt;parentId&gt;-&lt;counter&gt;"</c>
/// (or just <c>"&lt;counter&gt;"</c> at the root). Hashing matches the wire format
/// used by the Java/JS/Python SDKs so the same workflow position produces a
/// stable, opaque ID across replays — and the human-readable step name is
/// carried separately on <c>OperationUpdate.Name</c>, so renaming a step does
/// not break replay correlation.
/// </summary>
internal sealed class OperationIdGenerator
{
    private int _counter;
    private readonly string _prefix;

    /// <summary>
    /// Creates a root-level generator.
    /// </summary>
    public OperationIdGenerator()
        : this(parentId: null)
    {
    }

    /// <summary>
    /// Creates a child generator scoped under a parent operation. The parent
    /// ID (already hashed) becomes part of the prefix, so child IDs are
    /// <c>hash("&lt;parentHash&gt;-1")</c>, <c>hash("&lt;parentHash&gt;-2")</c>, etc.
    /// </summary>
    public OperationIdGenerator(string? parentId)
    {
        _counter = 0;
        ParentId = parentId;
        _prefix = parentId != null ? parentId + "-" : string.Empty;
    }

    /// <summary>
    /// Gets the parent operation ID, if any.
    /// </summary>
    public string? ParentId { get; }

    /// <summary>
    /// Generates the next operation ID. The counter is pre-incremented so the
    /// first ID is <c>hash("1")</c>, matching the reference SDKs.
    /// </summary>
    public string NextId()
    {
        var counter = ++_counter;
        return HashOperationId(_prefix + counter.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// SHA-256 hashes <paramref name="rawId"/> and returns a 64-char lowercase
    /// hex digest. Public so tests and child-context construction can reproduce
    /// the same hashing logic.
    /// </summary>
    public static string HashOperationId(string rawId)
    {
        var bytes = Encoding.UTF8.GetBytes(rawId);
        Span<byte> hash = stackalloc byte[32];
#if NET8_0_OR_GREATER
        SHA256.HashData(bytes, hash);
#else
        using var sha = SHA256.Create();
        var computed = sha.ComputeHash(bytes);
        computed.CopyTo(hash);
#endif
        return ToHex(hash);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        const string Hex = "0123456789abcdef";
        var chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = Hex[bytes[i] >> 4];
            chars[i * 2 + 1] = Hex[bytes[i] & 0xF];
        }
        return new string(chars);
    }

    /// <summary>
    /// Creates a child generator scoped under an operation ID from this generator.
    /// </summary>
    public OperationIdGenerator CreateChild(string operationId)
    {
        return new OperationIdGenerator(operationId);
    }

    /// <summary>
    /// Resets the counter (used for testing only).
    /// </summary>
    internal void Reset()
    {
        _counter = 0;
    }
}
