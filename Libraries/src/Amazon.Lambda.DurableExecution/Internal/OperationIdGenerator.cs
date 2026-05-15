using System.Security.Cryptography;
using System.Text;
using Amazon.Util;

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
        var hash = SHA256.HashData(bytes);
        return AWSSDKUtils.ToHex(hash, lowercase: true);
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
