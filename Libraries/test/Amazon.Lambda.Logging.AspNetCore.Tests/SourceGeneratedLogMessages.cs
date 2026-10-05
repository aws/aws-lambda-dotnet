using Microsoft.Extensions.Logging;

namespace Amazon.Lambda.Tests
{
    internal static partial class SourceGeneratedLogMessages
    {
        // "unreferenced" is intentionally not in the template. The generated state still enumerates it
        // after orderId, which used to shift the appended scope values onto the wrong placeholders in JSON mode.
#pragma warning disable SYSLIB1015
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Order {OrderId} placed")]
        public static partial void OrderPlaced(ILogger logger, int orderId, string unreferenced);
#pragma warning restore SYSLIB1015
    }
}
