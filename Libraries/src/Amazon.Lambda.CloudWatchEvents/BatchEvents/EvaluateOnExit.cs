namespace Amazon.Lambda.CloudWatchEvents.BatchEvents
{
    /// <summary>
    /// Specifies a set of conditions to be met, and an action to take (<c>RETRY</c> or <c>EXIT</c>) if all conditions are met.
    /// </summary>
    public class EvaluateOnExit
    {
        /// <summary>
        /// Specifies the action to take if all of the specified conditions (<c>onStatusReason</c>, <c>onReason</c>, and <c>onExitCode</c>) are met. The values aren't case sensitive.
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Contains a glob pattern to match against the decimal representation of the <c>ExitCode</c> returned for a job. The pattern can be up to 512 characters in length. 
        /// It can contain only numbers, and can optionally end with an asterisk (*) so that only the start of the string needs to be an exact match.
        /// </summary>
        public string OnExitCode { get; set; }

        /// <summary>
        /// Contains a glob pattern to match against the <c>Reason</c> returned for a job. The pattern can be up to 512 characters in length. 
        /// It can contain letters, numbers, periods (.), colons (:), and white space (including spaces and tabs). 
        /// It can optionally end with an asterisk (*) so that only the start of the string needs to be an exact match.
        /// </summary>
        public string OnReason { get; set; }

        /// <summary>
        /// Contains a glob pattern to match against the <c>StatusReason</c> returned for a job. The pattern can be up to 512 characters in length. 
        /// It can contain letters, numbers, periods (.), colons (:), and white space (including spaces or tabs). 
        /// It can optionally end with an asterisk (*) so that only the start of the string needs to be an exact match.
        /// </summary>
        public string OnStatusReason { get; set; }
    }
}
