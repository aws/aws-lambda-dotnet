using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Options that can be used to configure Lambda logging.
    /// </summary>
    public class LambdaLoggerOptions
    {
        // Default configuration section.
        // Customer configuration will be fetched from this section, unless otherwise specified.
        internal const string DEFAULT_SECTION_NAME = "Lambda.Logging";

        private const string INCLUDE_LOG_LEVEL_KEY = "IncludeLogLevel";
        private const string INCLUDE_CATEGORY_KEY = "IncludeCategory";
        private const string INCLUDE_NEWLINE_KEY = "IncludeNewline";
        private const string INCLUDE_EXCEPTION_KEY = "IncludeException";
		private const string INCLUDE_EVENT_ID_KEY = "IncludeEventId";
		private const string INCLUDE_SCOPES_KEY = "IncludeScopes";
        private const string LOG_LEVEL_KEY = "LogLevel";

        /// <summary>
        /// Flag to indicate if LogLevel should be part of logged message.
        /// Default is true.
        /// </summary>
        public bool IncludeLogLevel { get; set; }

        /// <summary>
        /// Flag to indicate if Category should be part of logged message.
        /// Default is true.
        /// </summary>
        public bool IncludeCategory { get; set; }

        /// <summary>
        /// Flag to indicate if logged messages should have a newline appended
        /// to them, if one isn't already there.
        /// Default is true.
        /// </summary>
        public bool IncludeNewline { get; set; }

        /// <summary>
        /// Flag to indicate if Exception should be part of logged message.
        /// Default is false.
        /// </summary>
        public bool IncludeException { get; set; }

        /// <summary>
        /// Flag to indicate if EventId should be part of logged message.
        /// Default is false.
        /// </summary>
        public bool IncludeEventId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether scopes should be included in the message.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool IncludeScopes { get; set; }

		/// <summary>
        /// Function used to filter events based on the log level.
        /// Default value is null and will instruct logger to log everything.
        /// </summary>
        [CLSCompliant(false)]  // https://github.com/aspnet/Logging/issues/500
        public Func<string, LogLevel, bool> Filter { get; set; }

        /// <summary>
        /// Constructs instance of LambdaLoggerOptions with default values.
        /// </summary>
        public LambdaLoggerOptions()
        {
            IncludeCategory = true;
            IncludeLogLevel = true;
            IncludeNewline = true;
            IncludeException = false;
            IncludeEventId = false;
            IncludeScopes = false;
            Filter = null;
        }

        /// <summary>
        /// Constructs instance of LambdaLoggerOptions with values from "Lambda.Logging"
        /// subsection of the specified configuration.
        /// The following configuration keys are supported:
        ///  IncludeLogLevel - boolean flag indicates if LogLevel should be part of logged message.
        ///  IncludeCategory - boolean flag indicates if Category should be part of logged message.
        ///  LogLevels - category-to-LogLevel mapping which indicates minimum LogLevel for a category.
        /// </summary>
        /// <param name="configuration"></param>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public LambdaLoggerOptions(IConfiguration configuration)
            : this(configuration, DEFAULT_SECTION_NAME)
        { }

        /// <summary>
        /// Constructs instance of LambdaLoggerOptions with values from specified
        /// subsection of the configuration.
        /// The following configuration keys are supported:
        ///  IncludeLogLevel - boolean flag indicates if LogLevel should be part of logged message.
        ///  IncludeCategory - boolean flag indicates if Category should be part of logged message.
        ///  LogLevels - category-to-LogLevel mapping which indicates minimum LogLevel for a category.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="loggingSectionName"></param>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public LambdaLoggerOptions(IConfiguration configuration, string loggingSectionName)
            : this()
        {
            // Input validation
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (string.IsNullOrEmpty(loggingSectionName))
            {
                throw new ArgumentNullException(nameof(loggingSectionName));
            }
            var loggerConfiguration = configuration.GetSection(loggingSectionName);
            if (loggerConfiguration == null)
            {
                throw new ArgumentOutOfRangeException(nameof(loggingSectionName), $"Unable to find section '{loggingSectionName}' in current configuration.");
            }

            // Parse settings

            if (TryGetString(loggerConfiguration, INCLUDE_CATEGORY_KEY, out string includeCategoryString))
            {
                IncludeCategory = bool.Parse(includeCategoryString);
            }

            if (TryGetString(loggerConfiguration, INCLUDE_LOG_LEVEL_KEY, out string includeLogLevelString))
            {
                IncludeLogLevel = bool.Parse(includeLogLevelString);
            }

            if (TryGetString(loggerConfiguration, INCLUDE_EXCEPTION_KEY, out string includeExceptionString))
            {
                IncludeException = bool.Parse(includeExceptionString);
            }

            if (TryGetString(loggerConfiguration, INCLUDE_EVENT_ID_KEY, out string includeEventIdString))
            {
                IncludeEventId = bool.Parse(includeEventIdString);
            }

            if (TryGetString(loggerConfiguration, INCLUDE_NEWLINE_KEY, out string includeNewlineString))
            {
                IncludeNewline = bool.Parse(includeNewlineString);
            }

            if (TryGetSection(loggerConfiguration, LOG_LEVEL_KEY, out IConfiguration logLevelsSection))
            {
                Filter = CreateFilter(logLevelsSection);
            }

            if (TryGetString(loggerConfiguration, INCLUDE_SCOPES_KEY, out string includeScopesString))
            {
                IncludeScopes = bool.Parse(includeScopesString);
            }
        }

        // Retrieves configuration value for key, if one exists
        private static bool TryGetString(IConfiguration configuration, string key, out string value)
        {
            value = configuration[key];
            return (value != null);
        }
        // Retrieves configuration section for key, if one exists
        private static bool TryGetSection(IConfiguration configuration, string key, out IConfiguration value)
        {
            value = configuration.GetSection(key);
            return (value != null);
        }
        // Creates filter for log levels section
        private static Func<string, LogLevel, bool> CreateFilter(IConfiguration logLevelsSection)
        {
            // Empty section means log everything
            var logLevels = logLevelsSection.GetChildren().ToList();
            if (logLevels.Count == 0)
            {
                return null;
            }

            // Populate mapping of category to LogLevel
            var logLevelsMapping = new Dictionary<string, LogLevel>(StringComparer.Ordinal);
            foreach (var logLevel in logLevels)
            {
                var category = logLevel.Key;
                var minLevelValue = logLevel.Value;
                LogLevel minLevel;
                if (!Enum.TryParse(minLevelValue, out minLevel))
                {
                    throw new InvalidCastException($"Unable to convert level '{minLevelValue}' for category '{category}' to LogLevel.");
                }

                if (category.Contains("*"))
                {
                    // Only 1 wildcard is supported
                    var wildcardCount = category.Count(x => x == '*');
                    if (wildcardCount > 1)
                    {
                        throw new ArgumentOutOfRangeException($"Category '{category}' is invalid - only 1 wildcard is supported in a category.");
                    }

                    // Wildcards are only supported at the end of a Category name!
                    var wildcardLocation = category.IndexOf('*');
                    if (wildcardLocation != category.Length - 1)
                    {
                        throw new ArgumentException($"Category '{category}' is invalid - wilcards are only supported at the end of a category.");
                    }

                    var trimmedCategory = category.TrimEnd('*');
                    logLevelsMapping[trimmedCategory] = minLevel;
                }
                else
                {
                    logLevelsMapping[category] = minLevel;
                }
            }

            // Extract a reverse sorted list of categories. This allows us to quickly search for most specific match to least specific.
            var orderedCategories = logLevelsMapping.Keys
                .OrderByDescending(categoryKey => categoryKey)
                .ToList();

            // Filter lambda that examines mapping
            return (string category, LogLevel logLevel) =>
            {
                LogLevel minLevel;

                // Exact match takes priority
                if (logLevelsMapping.TryGetValue(category, out minLevel))
                {
                    return logLevel >= minLevel;
                }

                // Find the most specific wildcard or namespace prefix category that matches the logger category
                var matchedCategory = orderedCategories.FirstOrDefault(category.StartsWith);
                if (matchedCategory != null)
                {
                    minLevel = logLevelsMapping[matchedCategory];
                    return logLevel >= minLevel;
                }

                // If no log filters then default to logging the log message.
                return true;
            };
        }
    }
}
