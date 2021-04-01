# Amazon.Lambda.Logging.AspNetCore

This package contains an implementation of ASP.NET Core's ILogger class, allowing an application to use the standard ASP.NET Core logging functionality to write CloudWatch Log events.

# Configuration

Lambda logging can be configured through code or using a file configuration loaded through the `IConfiguration` interface.
The two below examples set the same logging options, but do it either through code or a JSON config file. 

## Configuration through code

```csharp
public void Configure(ILoggerFactory loggerFactory)
{
    // Create and populate LambdaLoggerOptions object
    var loggerOptions = new LambdaLoggerOptions();
    loggerOptions.IncludeCategory = false;
    loggerOptions.IncludeLogLevel = false;
    loggerOptions.IncludeNewline = true;
    loggerOptions.IncludeException = true;
    loggerOptions.IncludeEventId = true;
    loggerOptions.IncludeScopes = true;

    // Configure Filter to only log some 
    loggerOptions.Filter = (category, logLevel) =>
    {
        // For some categories, only log events with minimum LogLevel
        if (string.Equals(category, "Default", StringComparison.Ordinal))
        {
            return (logLevel >= LogLevel.Debug);
        }
        if (string.Equals(category, "Microsoft", StringComparison.Ordinal))
        {
            return (logLevel >= LogLevel.Information);
        }

        // Log everything else
        return true;
    };

    // Configure Lambda logging
    loggerFactory
        .AddLambdaLogger(loggerOptions);
}
```

## Configuration through IConfiguration

Configuration file, `appsettings.json`:
```json
{
  "Lambda.Logging": {
    "IncludeCategory": false,
    "IncludeLogLevel": false,
    "IncludeNewline":  true,
    "IncludeException": true,
    "IncludeEventId": true,
    "IncludeScopes": true,
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

Creating `LambdaLoggerOptions` from the configuration file:
```csharp
public void Configure(ILoggerFactory loggerFactory)
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile(APPSETTINGS_PATH)
        .Build();

    var loggerOptions = new LambdaLoggerOptions(configuration);

    // Configure Lambda logging
    loggerFactory
        .AddLambdaLogger(loggerOptions);
}
```

# Using scopes
When `loggerOptions.IncludeScopes` is set to `true`.
```csharp
using(defaultLogger.BeginScope(awsRequestId))
{
    defaultLogger.LogInformation("Hello");

    using(defaultLogger.BeginScope("Second {0}", "scope456"))
    {
        defaultLogger.LogError("In 2nd scope");
        defaultLogger.LogInformation("that's enough");
    }
}
```
