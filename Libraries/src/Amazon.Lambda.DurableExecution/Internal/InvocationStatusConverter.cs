namespace Amazon.Lambda.DurableExecution.Internal;

/// <summary>
/// Concrete subclass for <see cref="InvocationStatus"/>. Source-generator JSON
/// contexts can only instantiate converters that are concrete and parameterless
/// when referenced via <c>[JsonConverter(typeof(...))]</c>.
/// </summary>
internal sealed class InvocationStatusConverter : UpperSnakeCaseEnumConverter<InvocationStatus> { }
