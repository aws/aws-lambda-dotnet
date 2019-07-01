namespace Amazon.Lambda.ApplicationLoadBalancerIdentity
{
    using System;

    /// <summary>
    /// A class containing the options used for configuring the Application Load Balancer Identity Middleware.
    /// </summary>
    public class ALBIdentityMiddlewareOptions
    {
        /// <summary>
        /// Gets or sets the maximum memory (in MB) that the identity cache will use.
        /// 
        /// The default value is 10MB.
        /// 
        /// If you set this property to null, in-memory caching will be disabled.
        /// 
        /// If you have set the <see cref="ValidateTokenSignature"/> property, enabling caching will usually reduce the overhead by double digit milliseconds on subsequent requests.
        /// 
        /// The size of each entry corresponds to the base64 encoded payload length in the JWT.
        /// Since this data is provided by the userinfo endpoint, it could be of any size.
        /// To calculate the *minimum* value for your traffic, identify your average userinfo payload length,
        /// and multiply that by the expected number of concurrent users per [active] token lifetime interval.
        /// 
        /// For example:
        /// Your average userinfo payload is 1.4KB, and you are expecting 10,000 concurrent users.
        /// If a token is valid for 1 day, then:
        /// 1.4KB * 10,000 * 1 = 13.6MB minimum
        /// 
        /// If your concurrent users are rapidly cycling (e.g. 10,000 *different* users every 15 minutes for 1 hour each)
        /// then you will want to alter the estimate to ensure that your cache is not evicting active entries.
        /// 1.4KB * 10,000 * (96 / 24) (15 min intervals in 1 day for 1 hour each) = 54.6MB minimum.
        /// 
        /// When the cache reaches it's maximum size, it will remove the oldest 20% of values, so it is
        /// recommended you use a value at least 20% larger than the minimum value calculated above.
        /// It does not allocate memory in advance, only when used, so if you have RAM to spare then estimate higher.
        /// </summary>
        public ushort? MaxCacheSizeMB { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum amount of time an identity will remain in the in-memory cache.
        /// 
        /// The default value is 24 hours.
        /// </summary>
        public TimeSpan? MaxCacheLifetime { get; set; } = TimeSpan.FromHours(24);

        /// <summary>
        /// Gets or sets the percentage of entries that will be removed when a cache hits the maximum size.
        /// 
        /// The default value is 10.
        /// 
        /// The minimum value you can set is 1, the maximum is 50.
        /// If you specify a value that is out of range, the default value will be used.
        /// </summary>
        public byte CacheCompactionPercentage { get; set; } = 10;

        /// <summary>
        /// Gets or sets a flag indicating whether token signatures should be validated to ensure that they were issued by the Application Load Balancer.
        /// 
        /// The default value is true. If you set this to false, you will improve performance but will be sacrificing security.
        /// 
        /// When you enable token verification, you are validating that the token was issued by the Application Load Balancer.
        /// This involves download the public signing key from the ALB key distribution endpoint (one time, then cached),
        /// and using the signature section of the JWT to ensure that the JWT was signed using the ALB's key.
        /// 
        /// If you enable this, it is recommended that you enable identity caching by setting the <see cref="MaxCacheSizeMB"/> property.
        /// If you do not enable caching, this validation will be performed on every request.
        /// </summary>
        public bool ValidateTokenSignature { get; set; } = true;

        /// <summary>
        /// Gets or sets the name of the role claim type, for use in the "IsInRole" method and [Authorize] attributes.
        /// 
        /// The default value is "group".
        /// </summary>
        public string RoleClaimType { get; set; } = "group";

        /// <summary>
        /// Gets or sets a flag indicating whether token lifetime should be validated.
        /// 
        /// The default value is true. This should never be disabled except for testing purposes.
        /// </summary>
        internal bool ValidateTokenLifetime { get; set; } = true;
    }
}
