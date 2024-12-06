namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Standardized CLI return codes for Commands.
/// </summary>
public abstract class CommandReturnCodes
{
    /// <summary>
    /// Command completed and honored user's intention.
    /// </summary>
    public const int Success = 0;
    /// <summary>
    /// A command could not finish its work because an unexpected
    /// exception was thrown.  This usually means there is an intermittent io problem
    /// or bug in the code base.
    /// <para />
    /// Unexpected exceptions are any exception that do not inherit from
    /// <see cref="TestToolException"/>
    /// </summary>
    public const int UnhandledException = -1;
    /// <summary>
    /// A command could not finish of an expected problem like a user
    /// configuration or system configuration problem.  For example, a required
    /// dependency like Docker is not installed.
    /// <para />
    /// Expected problems are usually indicated by throwing an exception that
    /// inherits from <see cref="TestToolException"/>
    /// </summary>
    public const int UserError = 1;
}