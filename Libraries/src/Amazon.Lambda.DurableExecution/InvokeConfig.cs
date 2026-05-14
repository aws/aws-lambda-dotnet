namespace Amazon.Lambda.DurableExecution;

/// <summary>
/// Configuration for chained invoke operations.
/// </summary>
/// <remarks>
/// Use with <see cref="IDurableContext.InvokeAsync{TPayload, TResult}(string, TPayload, string?, InvokeConfig?, System.Threading.CancellationToken)"/>
/// to configure a single chained invocation. Payload/result serialization is
/// performed by the <see cref="Amazon.Lambda.Core.ILambdaSerializer"/> registered on
/// <see cref="Amazon.Lambda.Core.ILambdaContext.Serializer"/> (typically configured via
/// <c>LambdaBootstrapBuilder.Create(handler, serializer)</c>); there are
/// intentionally no serializer fields here, matching the pattern established
/// by <see cref="StepConfig"/>.
/// </remarks>
public sealed class InvokeConfig
{
    /// <summary>
    /// Optional tenant identifier propagated to the chained invocation via
    /// <c>ChainedInvokeOptions.TenantId</c>. Used to route the invocation to a
    /// tenant-isolated function. Matches the <c>tenantId</c> field on the
    /// Python, JavaScript, and Java SDKs.
    /// </summary>
    public string? TenantId { get; set; }
}
