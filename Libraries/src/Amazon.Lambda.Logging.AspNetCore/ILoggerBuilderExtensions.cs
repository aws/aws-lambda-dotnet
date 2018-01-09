using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;

// Same namespace as ILoggingBuilder, to make these extensions appear
// without the user needing to including our namespace first.
namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// ILoggingBuilder extensions
    /// </summary>
    public static class ILoggerBuilderExtensions
    {
        /// <summary>
        /// Adds a Lambda logger provider with default options.
        /// </summary>
        /// <param name="builder">ILoggingBuilder to add Lambda logger to.</param>
        /// <returns>Updated ILoggingBuilder.</returns>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public static ILoggingBuilder AddLambdaLogger(this ILoggingBuilder builder)
        {
            var options = new LambdaLoggerOptions();
            return AddLambdaLogger(builder, options);
        }

        /// <summary>
        /// Adds a Lambda logger provider with specified options.
        /// </summary>
        /// <param name="builder">ILoggingBuilder to add Lambda logger to.</param>
        /// <param name="options">Lambda logging options.</param>
        /// <returns>Updated ILoggingBuilder.</returns>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public static ILoggingBuilder AddLambdaLogger(this ILoggingBuilder builder, LambdaLoggerOptions options)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var provider = new LambdaILoggerProvider(options);
            builder.AddProvider(provider);
            return builder;
        }

        /// <summary>
        /// Adds a Lambda logger provider with options loaded from the specified subsection of the
        /// configuration section.
        /// </summary>
        /// <param name="builder">ILoggingBuilder to add Lambda logger to.</param>
        /// <param name="configuration">IConfiguration to use when construction logging options.</param>
        /// <param name="loggingSectionName">Name of the logging section with required settings.</param>
        /// <returns>Updated ILoggingBuilder.</returns>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public static ILoggingBuilder AddLambdaLogger(this ILoggingBuilder builder, IConfiguration configuration, string loggingSectionName)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (string.IsNullOrEmpty(loggingSectionName))
            {
                throw new ArgumentNullException(nameof(loggingSectionName));
            }

            var options = new LambdaLoggerOptions(configuration, loggingSectionName);
            var provider = new LambdaILoggerProvider(options);
            builder.AddProvider(provider);
            return builder;
        }
    }
}
