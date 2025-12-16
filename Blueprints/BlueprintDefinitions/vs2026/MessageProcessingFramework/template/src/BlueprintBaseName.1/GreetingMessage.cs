namespace BlueprintBaseName._1;

/// <summary>
/// This class represents the message contents that are sent and received
/// </summary>
public class GreetingMessage
{
    /// <summary>
    /// Username of who sent the message
    /// </summary>
    public string? SenderName { get; set; }

    /// <summary>
    /// User's greeting
    /// </summary>
    public string? Greeting { get; set; }
}
