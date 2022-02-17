using Amazon.Lambda.LexEvents;

namespace BlueprintBaseName._1;

/// <summary>
/// This class contains the results of validating the current state of all slot values. This is used to send information
/// back to the user to fix bad slot values.
/// </summary>
public class ValidationResult
{
    public static readonly ValidationResult VALID_RESULT = new ValidationResult(true, null, null);

    public ValidationResult(bool isValid, string? violationSlot, string? message)
    {
        this.IsValid = isValid;
        this.ViolationSlot = violationSlot;

        if (!string.IsNullOrEmpty(message))
        {
            this.Message = new LexResponse.LexMessage { ContentType = "PlainText", Content = message };
        }
    }

    /// <summary>
    /// If the slot values are currently correct.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Which slot value is invalid so the user can correct the value.
    /// </summary>
    public string? ViolationSlot { get; }

    /// <summary>
    /// The message explaining to the user what is wrong with the slot value.
    /// </summary>
    public LexResponse.LexMessage? Message { get; }
}