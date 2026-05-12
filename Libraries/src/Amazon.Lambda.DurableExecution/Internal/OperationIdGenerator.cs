using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Amazon.Util;

namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Generates deterministic operation IDs for durable operations. Each call
/// increments an internal counter and SHA-256 hashes <c>"&lt;parentId&gt;-&lt;counter&gt;"</c>
/// (or just <c>"&lt;counter&gt;"</c> at the root). The same workflow position
/// produces a stable, opaque ID across replays — and the human-readable step
/// name is carried separately on <c>OperationUpdate.Name</c>, so renaming a
/// step does not break replay correlation.
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
    /// first ID is <c>hash("1")</c>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Interlocked.Increment(ref int)"/> so concurrent callers
    /// (e.g. user code that wraps multiple <c>StepAsync</c> calls in
    /// <c>Task.WhenAll</c> with <c>Task.Run</c>, or future <c>ParallelAsync</c>/
    /// <c>MapAsync</c> branches that fan out before awaiting) cannot collide
    /// on the same ID. Determinism still requires that calls happen in a
    /// deterministic order — atomicity prevents duplicate IDs but not
    /// reordering between replays.
    /// </remarks>
    public string NextId()
    {
        var counter = Interlocked.Increment(ref _counter);
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
    /// Resets the counter (used for testing only). Not safe to call concurrently
    /// with <see cref="NextId"/>; tests must quiesce before resetting.
    /// </summary>
    internal void Reset()
    {
        Interlocked.Exchange(ref _counter, 0);
    }
}
