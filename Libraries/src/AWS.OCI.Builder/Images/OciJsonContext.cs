using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AWS.OCI.Builder.Registry;

namespace AWS.OCI.Builder.Images;

/// <summary>
/// The source-generated <see cref="JsonSerializerContext"/> for the strongly-typed documents this library
/// serializes or deserializes. Routing System.Text.Json calls through this context (rather than the
/// reflection-based <c>JsonSerializer</c> overloads) is what makes those paths trim- and Native-AOT-safe: the
/// serialization metadata is emitted at compile time, so nothing needs runtime reflection or dynamic code
/// generation.
/// </summary>
/// <remarks>
/// <para>
/// Output is compact and omits null-valued properties. The manifest's digest/tag is computed over these exact
/// bytes, so the formatting is part of the contract. (The <see cref="ImageConfig"/> document is a free-form
/// <c>JsonObject</c> rather than a modeled type, so it is serialized directly through a <c>Utf8JsonWriter</c> in
/// that class rather than through this context.)
/// </para>
/// <para>
/// The relaxed JSON encoder (which leaves characters such as <c>+</c> unescaped) cannot be set on
/// <see cref="JsonSourceGenerationOptionsAttribute"/>, so it is applied via <see cref="RelaxedOptions"/> — a
/// <see cref="JsonSerializerOptions"/> that chains this context's source-generated resolver and adds the encoder.
/// Serializing through the <see cref="JsonTypeInfo{T}"/> these options produce stays AOT-safe while matching the
/// byte-for-byte escaping other container tooling emits (so the computed digests agree).
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Manifest))]
[JsonSerializable(typeof(ImageIndex))]
[JsonSerializable(typeof(Descriptor))]
[JsonSerializable(typeof(PlatformManifestDescriptor))]
[JsonSerializable(typeof(Platform))]
[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class OciJsonContext : JsonSerializerContext
{
    // Chains the source-generated resolver and adds the relaxed encoder. Built lazily (not in a static field
    // initializer) because the generated `Default` singleton is itself lazily initialized: referencing it from
    // this class's own static-field initializers races its construction and throws a NullReferenceException in
    // the type initializer (notably under Native AOT). Lazy<T> defers the reference until first use, by which
    // point `Default` is available.
    private static readonly Lazy<JsonSerializerOptions> RelaxedOptions = new(() =>
        new JsonSerializerOptions(Default!.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static readonly Lazy<JsonTypeInfo<Manifest>> ManifestInfoLazy = new(() =>
        (JsonTypeInfo<Manifest>)RelaxedOptions.Value.GetTypeInfo(typeof(Manifest)));

    private static readonly Lazy<JsonTypeInfo<ImageIndex>> ImageIndexInfoLazy = new(() =>
        (JsonTypeInfo<ImageIndex>)RelaxedOptions.Value.GetTypeInfo(typeof(ImageIndex)));

    private static readonly Lazy<JsonTypeInfo<TokenResponse>> TokenResponseInfoLazy = new(() =>
        (JsonTypeInfo<TokenResponse>)RelaxedOptions.Value.GetTypeInfo(typeof(TokenResponse)));

    /// <summary>Type metadata for <see cref="Images.Manifest"/> with the relaxed (digest-faithful) encoder.</summary>
    internal static JsonTypeInfo<Manifest> ManifestInfo => ManifestInfoLazy.Value;

    /// <summary>Type metadata for <see cref="Images.ImageIndex"/> with the relaxed (digest-faithful) encoder.</summary>
    internal static JsonTypeInfo<ImageIndex> ImageIndexInfo => ImageIndexInfoLazy.Value;

    /// <summary>Type metadata for <see cref="Registry.TokenResponse"/> (relaxed encoder; this type is only ever read).</summary>
    internal static JsonTypeInfo<TokenResponse> TokenResponseInfo => TokenResponseInfoLazy.Value;
}
