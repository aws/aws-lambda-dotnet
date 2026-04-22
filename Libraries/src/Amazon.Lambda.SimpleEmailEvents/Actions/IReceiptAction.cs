namespace Amazon.Lambda.SimpleEmailEvents.Actions
{
    /// <summary>
    /// Represents an action that can be performed on a receipt.
    /// </summary>
    public interface IReceiptAction
    {
        /// <summary>
        /// Gets or sets the type identifier associated with the current instance.
        /// </summary>
        string Type { get; set; }
    }
}
