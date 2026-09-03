// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Amazon.Lambda.Core;

namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Controls when <see cref="FileSystemSerializer"/> writes a value to the filesystem.
/// </summary>
public enum FileSystemStorageMode
{
    /// <summary>
    /// Every value is written to a file; the checkpoint stores only a file pointer.
    /// Best for consistently large payloads or predictable checkpoint sizes.
    /// </summary>
    Always,

    /// <summary>
    /// The value is stored inline in the checkpoint unless it would exceed the durable
    /// execution checkpoint size limit (~256&#160;KB), in which case it overflows to a
    /// file. Best for mixed workloads where most payloads are small.
    /// </summary>
    Overflow,
}

/// <summary>
/// Controls how the durable execution ARN (directory) and entity id (file name) are
/// turned into filesystem path segments.
/// </summary>
public enum FileSystemPathEncoding
{
    /// <summary>
    /// Human-navigable paths. The per-execution directory is built from the ARN's
    /// function name, execution name and invocation id; the file name is the entity id,
    /// URL-encoded. If the ARN does not match the expected durable-execution shape, the
    /// whole ARN is URL-encoded into a single directory segment.
    /// </summary>
    Uri,

    /// <summary>
    /// The ARN (directory) and entity id (file name) are each replaced by their SHA-256
    /// hex digest — fixed length and always filesystem-safe, at the cost of readability.
    /// </summary>
    Hash,
}

/// <summary>
/// An <see cref="ILambdaSerializer"/> / <see cref="IDurableResultSerializer"/> that stores
/// serialized durable operation results on a filesystem, keeping only a small pointer in
/// the checkpoint. It wraps an <em>inner</em> serializer that performs the actual
/// value&#8596;bytes conversion (JSON, compressed JSON, a custom format, …), so the choice
/// of on-the-wire format is fully in the caller's control. Construct it without an inner
/// serializer (<see cref="FileSystemSerializer(string, FileSystemStorageMode, FileSystemPathEncoding)"/>)
/// to reuse the durable execution's globally-registered serializer as the inner.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠ Do NOT use with Lambda's ephemeral <c>/tmp</c> for values that must survive
/// replay.</b> <c>/tmp</c> is local to a single execution environment; on replay a
/// different environment may be used and the file will not be found. Use a durable,
/// shared mount such as Amazon EFS or Amazon S3 Files, which persist across invocations
/// and are visible to concurrent function instances.
/// </para>
/// <para>
/// The checkpoint stores a JSON envelope that is either
/// <c>{"data":"&lt;base64 inner bytes&gt;"}</c> (inline) or <c>{"file":"&lt;path&gt;"}</c>
/// (pointer). The envelope is serialized with a source generator, so this type is
/// Native-AOT-safe as long as the inner serializer is.
/// </para>
/// <para>
/// <b>Lifecycle:</b> this serializer never deletes result files. A write for a given
/// (execution, entity) overwrites its file in place, so retries and re-serializations
/// of the same operation do not accumulate; but the files of completed or abandoned
/// executions remain on the mount. Pair the base path with an external retention policy
/// (an EFS lifecycle policy, an S3 lifecycle rule, or a scheduled cleanup keyed by the
/// per-execution directory) so storage does not grow unbounded.
/// </para>
/// <para>
/// This serializer must be used through a durable operation's per-operation serializer
/// slot (for example <c>StepConfig.Serializer</c>) so it receives a
/// <see cref="DurableSerializationContext"/>. Using it as a plain
/// <see cref="ILambdaSerializer"/> (for example as the assembly-registered serializer)
/// throws, because there is then no execution/entity identity to build a safe path.
/// </para>
/// </remarks>
public sealed class FileSystemSerializer : ILambdaSerializer, IDurableResultSerializer, IDefaultInnerSerializer
{
    // The durable execution checkpoint size limit is ~256 KB; leave 1 KB of headroom for
    // the envelope wrapper and other checkpoint metadata.
    private const int OverflowThresholdBytes = (256 * 1024) - 1024;

    private static readonly Regex DurableExecutionArnPattern = new(
        @"^arn:[^:]*:lambda:[^:]*:[^:]*:function:([^:/]+):[^:/]+/durable-execution/([^/]+)/([^/]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private readonly ILambdaSerializer? _inner;
    private readonly string _basePath;
    private readonly FileSystemStorageMode _storageMode;
    private readonly FileSystemPathEncoding _pathEncoding;

    /// <summary>Creates a new <see cref="FileSystemSerializer"/>.</summary>
    /// <param name="inner">
    /// The serializer that converts values to/from bytes (for example the global
    /// <c>ILambdaContext.Serializer</c>, or a compressing/encrypting wrapper).
    /// </param>
    /// <param name="basePath">
    /// Directory under which result files are written (for example <c>/mnt/efs/durable</c>).
    /// Use a durable, shared mount — see the type remarks.
    /// </param>
    /// <param name="storageMode">When to write to a file. Defaults to <see cref="FileSystemStorageMode.Always"/>.</param>
    /// <param name="pathEncoding">How to encode path segments. Defaults to <see cref="FileSystemPathEncoding.Uri"/>.</param>
    public FileSystemSerializer(
        ILambdaSerializer inner,
        string basePath,
        FileSystemStorageMode storageMode = FileSystemStorageMode.Always,
        FileSystemPathEncoding pathEncoding = FileSystemPathEncoding.Uri)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        // Normalize once so stored file pointers are absolute and their resolution
        // does not depend on the current working directory of a later invocation
        // (which may run in a different execution environment). ValidatePathWithinBase
        // still re-resolves for symlink safety, but the stored base is now stable.
        _basePath = Path.GetFullPath(basePath ?? throw new ArgumentNullException(nameof(basePath)));
        _storageMode = storageMode;
        _pathEncoding = pathEncoding;
    }

    /// <summary>
    /// Creates a new <see cref="FileSystemSerializer"/> that uses the durable execution's
    /// globally-registered <see cref="ILambdaSerializer"/> (the assembly-level
    /// <c>[assembly: LambdaSerializer(...)]</c> serializer, or the one passed to
    /// <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>) as its inner serializer.
    /// </summary>
    /// <remarks>
    /// The inner serializer is supplied by the durable runtime when this instance is used
    /// through a per-operation serializer slot (for example <c>StepConfig.Serializer</c>).
    /// It saves you from having to thread <c>ctx.Serializer</c> in yourself when the
    /// on-the-wire format is just the function's normal serializer.
    /// </remarks>
    /// <param name="basePath">
    /// Directory under which result files are written (for example <c>/mnt/efs/durable</c>).
    /// Use a durable, shared mount — see the type remarks.
    /// </param>
    /// <param name="storageMode">When to write to a file. Defaults to <see cref="FileSystemStorageMode.Always"/>.</param>
    /// <param name="pathEncoding">How to encode path segments. Defaults to <see cref="FileSystemPathEncoding.Uri"/>.</param>
    public FileSystemSerializer(
        string basePath,
        FileSystemStorageMode storageMode = FileSystemStorageMode.Always,
        FileSystemPathEncoding pathEncoding = FileSystemPathEncoding.Uri)
    {
        _inner = null;
        // Normalize once (see the inner-taking constructor) so a stored file pointer is
        // absolute and stable across invocations, independent of the current directory.
        _basePath = Path.GetFullPath(basePath ?? throw new ArgumentNullException(nameof(basePath)));
        _storageMode = storageMode;
        _pathEncoding = pathEncoding;
    }

    // When constructed without an explicit inner serializer, the durable runtime binds the
    // globally-registered serializer here before the operation runs (see the parameterless-inner
    // constructor). Returns a bound copy; a caller-supplied inner always wins and is left as-is.
    ILambdaSerializer IDefaultInnerSerializer.WithDefaultInner(ILambdaSerializer inner)
    {
        if (inner is null) throw new ArgumentNullException(nameof(inner));
        return _inner is not null
            ? this
            : new FileSystemSerializer(inner, _basePath, _storageMode, _pathEncoding);
    }

    // ---- context-aware path (used by durable execution) ----

    /// <inheritdoc />
    public void Serialize<T>(T value, Stream stream, DurableSerializationContext context)
    {
        string path;
        if (_storageMode == FileSystemStorageMode.Overflow)
        {
            // Overflow needs the serialized size to decide inline-vs-file, so it buffers.
            byte[] bytes = InnerSerialize(value);
            var inline = new FileSystemEnvelope { Data = Convert.ToBase64String(bytes) };
            var inlineJson = JsonSerializer.Serialize(inline, FileSystemJsonContext.Default.FileSystemEnvelope);
            if (Encoding.UTF8.GetByteCount(inlineJson) <= OverflowThresholdBytes)
            {
                var inlineBytes = Encoding.UTF8.GetBytes(inlineJson);
                stream.Write(inlineBytes, 0, inlineBytes.Length);
                return;
            }
            path = WriteBytesToFile(bytes, context);
        }
        else
        {
            // Always: stream the inner serialization straight to the file — no large in-memory
            // intermediate, which is the whole point of offloading big payloads.
            path = WriteStreamingToFile(value, context);
        }

        var envelope = new FileSystemEnvelope { File = path };
        JsonSerializer.Serialize(stream, envelope, FileSystemJsonContext.Default.FileSystemEnvelope);
    }

    /// <inheritdoc />
    public T Deserialize<T>(Stream stream, DurableSerializationContext context)
    {
        var envelope = JsonSerializer.Deserialize(stream, FileSystemJsonContext.Default.FileSystemEnvelope)
            ?? throw new InvalidOperationException("FileSystemSerializer: empty or invalid envelope.");

        if (envelope.File is not null)
        {
            // Guard against a tampered/corrupted checkpoint pointing outside the base path.
            ValidatePathWithinBase(envelope.File);
            if (!File.Exists(envelope.File))
                throw new FileNotFoundException(
                    $"FileSystemSerializer: offloaded payload file not found: '{envelope.File}'. " +
                    "If the base path is Lambda's /tmp, the value cannot survive replay on a different " +
                    "execution environment — use a durable shared mount such as EFS or S3 Files.",
                    envelope.File);

            // Stream the offloaded payload straight to the inner serializer instead of
            // buffering the whole file into a byte[] — symmetric with the streaming
            // write path (WriteStreamingToFile), so a large offloaded value is never
            // fully materialized in memory just to be read back.
            //
            // FileShare.Delete (in addition to Read): the write path replaces a file in
            // place via File.Move(overwrite: true). On POSIX/Linux that rename-over is
            // atomic and an open read handle never blocks it (the reader keeps the old
            // inode). On Windows/macOS, a held read handle opened WITHOUT FileShare.Delete
            // blocks the rename-over, so a concurrent replay-read racing a re-serialize of
            // the same entity would throw IOException. Granting Delete share keeps those
            // non-Linux readers from blocking the atomic replace; the atomic-replace
            // concurrency-safety itself is a POSIX/Linux property. (Prod runs Linux, but
            // dev/test frequently run Windows/macOS.)
            var inner = RequireInner();
            using var fileStream = new FileStream(
                envelope.File, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return inner.Deserialize<T>(fileStream);
        }

        if (envelope.Data is not null)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(envelope.Data);
            }
            catch (FormatException ex)
            {
                // Sibling checks in this method throw descriptive InvalidOperationExceptions;
                // a corrupted inline envelope should be no different (a bare FormatException
                // gives no context about which envelope field was malformed).
                throw new InvalidOperationException(
                    "FileSystemSerializer: inline envelope 'data' is not valid base64 — the checkpoint " +
                    "is corrupted or was not written by this serializer.", ex);
            }
            return InnerDeserialize<T>(bytes);
        }

        throw new InvalidOperationException("FileSystemSerializer: envelope has neither 'file' nor 'data'.");
    }

    // ---- plain ILambdaSerializer path (only reachable outside durable execution) ----

    void ILambdaSerializer.Serialize<T>(T response, Stream responseStream) =>
        throw new NotSupportedException(
            "FileSystemSerializer must be used via a durable operation's per-operation serializer " +
            "(for example StepConfig.Serializer), which supplies the DurableSerializationContext needed " +
            "to build a safe, unique file path. It cannot be used as a plain ILambdaSerializer.");

    T ILambdaSerializer.Deserialize<T>(Stream requestStream) =>
        throw new NotSupportedException(
            "FileSystemSerializer must be used via a durable operation's per-operation serializer " +
            "(for example StepConfig.Serializer). It cannot be used as a plain ILambdaSerializer.");

    // ---- helpers ----

    // The inner serializer is either the one passed to the constructor or, for the
    // inner-less constructor, the globally-registered serializer bound by the durable
    // runtime via IDefaultInnerSerializer. If neither is present it means this instance
    // was used outside a durable operation slot, where no global serializer is available.
    private ILambdaSerializer RequireInner() =>
        _inner ?? throw new InvalidOperationException(
            "FileSystemSerializer was constructed without an inner serializer and the durable " +
            "runtime did not supply a globally-registered ILambdaSerializer to use as the inner. " +
            "Either pass an inner serializer to the constructor, or register one via " +
            "[assembly: LambdaSerializer(typeof(...))] / LambdaBootstrapBuilder.Create(handler, serializer).");

    private byte[] InnerSerialize<T>(T value)
    {
        var inner = RequireInner();
        using var ms = new MemoryStream();
        inner.Serialize(value, ms);
        return ms.ToArray();
    }

    private T InnerDeserialize<T>(byte[] bytes)
    {
        var inner = RequireInner();
        using var ms = new MemoryStream(bytes);
        return inner.Deserialize<T>(ms);
    }

    private string WriteStreamingToFile<T>(T value, DurableSerializationContext context)
    {
        var inner = RequireInner();
        var (path, tmp) = ResolveTargetPaths(context);
        try
        {
            using (var fileStream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                inner.Serialize(value, fileStream);
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
        return path;
    }

    private string WriteBytesToFile(byte[] bytes, DurableSerializationContext context)
    {
        var (path, tmp) = ResolveTargetPaths(context);
        try
        {
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
        return path;
    }

    // Resolves the final file path and a unique temp path in the SAME directory. Writing to the
    // temp file then atomically moving it means a concurrent replay/reader never observes a
    // partially-written or truncated file. The temp name is unique so two writers of the same
    // entity don't clobber each other's in-progress temp.
    private (string path, string tmp) ResolveTargetPaths(DurableSerializationContext context)
    {
        var dir = ResolveExecutionDir(context.DurableExecutionArn);
        // Guard the WRITE path the same way the read path is guarded: under Uri
        // encoding the ARN-derived segments are inserted into the path unescaped
        // (Uri.EscapeDataString does not escape '.', so it would NOT neutralize a
        // '..' traversal segment either), so a tampered/malformed durable execution
        // ARN containing '..' could otherwise resolve the per-execution directory
        // outside the configured base path.
        ValidateWriteDirWithinBase(dir);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, EncodeSegment(context.EntityId) + ".bin");
        var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        return (path, tmp);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    // Rejects a checkpoint file pointer that resolves outside the configured base path, so a
    // tampered or corrupted envelope cannot turn deserialization into an arbitrary file read.
    private void ValidatePathWithinBase(string filePath)
    {
        if (!IsWithinBase(filePath))
            throw new InvalidOperationException(
                $"FileSystemSerializer: refusing to read an offloaded payload outside the configured " +
                $"base path. Path '{filePath}' does not resolve under '{_basePath}'.");
    }

    // Rejects a resolved per-execution write directory that lands outside the configured base
    // path, so a tampered/malformed durable execution ARN (e.g. containing '..' traversal
    // segments) cannot write offloaded payloads to an arbitrary filesystem location.
    private void ValidateWriteDirWithinBase(string dir)
    {
        if (!IsWithinBase(dir))
            throw new InvalidOperationException(
                $"FileSystemSerializer: refusing to write an offloaded payload outside the configured " +
                $"base path. Resolved directory '{dir}' does not resolve under '{_basePath}'. This can " +
                "happen when the durable execution ARN contains path-traversal segments.");
    }

    // True when the fully-resolved candidate path lies under the fully-resolved base path.
    private bool IsWithinBase(string candidate)
    {
        // Compare using the filesystem's casing rules. Ordinal alone would falsely
        // reject an in-base path that differs only by case on Windows/macOS.
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var fullBase = ResolveReal(Path.GetFullPath(_basePath));
        if (!fullBase.EndsWith(Path.DirectorySeparatorChar))
            fullBase += Path.DirectorySeparatorChar;

        var fullPath = ResolveReal(Path.GetFullPath(candidate));
        return fullPath.StartsWith(fullBase, comparison);
    }

    // Best-effort resolution of a path to its real on-disk location, following a
    // symlink on the leaf file/directory and on its immediate parent directory.
    // Path.GetFullPath only collapses '..'/separators and does NOT follow symlinks,
    // so without this a symlink planted under the base but pointing outside it would
    // pass a purely lexical prefix check and defeat the containment guard. Both the
    // base and the candidate are resolved the same way so a symlinked mount root
    // (for example macOS /var -> /private/var) does not cause false rejections.
    private static string ResolveReal(string fullPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (dir != null && Directory.Exists(dir))
            {
                var realDir = Directory.ResolveLinkTarget(dir, returnFinalTarget: true)?.FullName ?? dir;
                fullPath = Path.Combine(realDir, Path.GetFileName(fullPath));
            }
            if (File.Exists(fullPath))
                fullPath = File.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName ?? fullPath;
            else if (Directory.Exists(fullPath))
                fullPath = Directory.ResolveLinkTarget(fullPath, returnFinalTarget: true)?.FullName ?? fullPath;
        }
        catch
        {
            // Best effort — fall back to the lexical full path.
        }
        return fullPath;
    }

    private string ResolveExecutionDir(string arn)
    {
        if (_pathEncoding == FileSystemPathEncoding.Uri)
        {
            var match = DurableExecutionArnPattern.Match(arn);
            if (match.Success)
            {
                return Path.Combine(
                    _basePath,
                    match.Groups[1].Value,   // function name
                    match.Groups[2].Value,   // execution name
                    match.Groups[3].Value);  // invocation id
            }
        }
        return Path.Combine(_basePath, EncodeSegment(arn));
    }

    private string EncodeSegment(string value) =>
        _pathEncoding == FileSystemPathEncoding.Hash
            ? Sha256Hex(value)
            : Uri.EscapeDataString(value);

    private static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>The checkpoint envelope written by <see cref="FileSystemSerializer"/>.</summary>
internal sealed class FileSystemEnvelope
{
    /// <summary>Inline payload (base64 of the inner-serialized bytes). Set when stored inline.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }

    /// <summary>Path to the file holding the inner-serialized bytes. Set when offloaded.</summary>
    [JsonPropertyName("file")]
    public string? File { get; set; }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FileSystemEnvelope))]
internal partial class FileSystemJsonContext : JsonSerializerContext
{
}
