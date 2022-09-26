namespace Amazon.Lambda.Core
{
#if NET6_0_OR_GREATER
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a structured log entry.
    /// </summary>
    public abstract class MessageEntry
    {
        /// <summary>
        /// The state data of the log entry.
        /// </summary>
        public abstract IReadOnlyList<KeyValuePair<string, object>> State { get; }

        /// <summary>
        /// The exception included in the entry. This property is NULL if the log does not contain exception.
        /// </summary>
        public abstract Exception Exception { get; }

        /// <summary>
        /// Gets the log entry's message.
        /// </summary>
        public abstract override string ToString();
    }
#endif
}
