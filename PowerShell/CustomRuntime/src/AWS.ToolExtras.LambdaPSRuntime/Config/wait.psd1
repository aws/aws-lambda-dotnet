@{
    # How long the deploys wait for Lambda to settle a resource: up to Attempts reads, IntervalSeconds
    # apart. The timeout errors report Attempts * IntervalSeconds as the time waited.

    # A created or updated function, until it is Active with no update InProgress.
    Function       = @{
        Attempts        = 60
        IntervalSeconds = 2
    }

    # A published runtime version, until it is no longer CREATING.
    RuntimeVersion = @{
        Attempts        = 60
        IntervalSeconds = 3
    }
}
