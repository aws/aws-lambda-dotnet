namespace Amazon.Lambda.ApplicationLoadBalancerIdentity
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.IdentityModel.JsonWebTokens;
    using Microsoft.IdentityModel.Tokens;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.OpenSsl;

    /// <summary>
    /// A middleware class that extends the Application Load Balancer (ALB) OpenId Connect authentication functionality into ASP.NET Core.
    /// </summary>
    public class ALBIdentityMiddleware
    {
        internal const string OidcDataHeader = "x-amzn-oidc-data";
        internal const string AWSRegionEnvironmentVariable = "AWS_REGION";
        internal const string ALBPublicKeyUrlFormatString = "https://public-keys.auth.elb.{0}.amazonaws.com/{1}";

        internal static HttpClient InternalHttpClient;

        private const string NameClaimType = "sub";
        private const string AuthenticationType = "oidc";
        private const string ContentTypeHeader = "Content-Type";
        private const string ContentTypeValue = "text/plain";

        /// Caching related properties
        private static uint[] byteLookup;
        private readonly bool cacheIdentities;
        private readonly MemoryCache cachedIds;
        private readonly SHA1 sha;
        private readonly TimeSpan cacheDuration;

        // Validation related properties
        private readonly string region;
        private readonly ConcurrentDictionary<string, TokenValidationParameters> cachedValidationParameters;
        private readonly JsonWebTokenHandler tokenHandler;

        // Properties from DI
        private readonly ILogger<ALBIdentityMiddleware> logger;
        private readonly ALBIdentityMiddlewareOptions options;
        private readonly RequestDelegate next;

        /// <summary>
        /// Initializes a new instance of the <see cref="ALBIdentityMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next <see cref="RequestDelegate"/> to call in the chain.</param>
        /// <param name="logger"><see cref="ILogger{TCategoryName}"/> implementation.</param>
        /// <param name="options">An instance of the <see cref="ALBIdentityMiddlewareOptions"/> class containing the configuration settings for the Middleware.</param>
        public ALBIdentityMiddleware(RequestDelegate next, ILogger<ALBIdentityMiddleware> logger, ALBIdentityMiddlewareOptions options)
        {
            this.next = next;
            this.logger = logger;
            this.options = options;

            if (this.options.ValidateTokenSignature)
            {
                // Unit tests will set this property, so we only create a new client if it doesn't already have a value.
                if (InternalHttpClient == null) InternalHttpClient = new HttpClient();
                this.region = Environment.GetEnvironmentVariable(AWSRegionEnvironmentVariable);
                this.cachedValidationParameters = new ConcurrentDictionary<string, TokenValidationParameters>();
                this.tokenHandler = new JsonWebTokenHandler();
            }

            this.cacheIdentities = this.options.MaxCacheSizeMB.HasValue;
            if (this.cacheIdentities)
            {
                // TimeSpan.MaxValue isn't a real thing. If the underlying library that uses it converts it to an integer (which a lot do),
                // then it will throw an ArgumentOutOfRange exception if you specify a timespan with more seconds in it than
                // the max value of an Int32 has. To use an *actual* MaxValue that doesn't fail, you need to create a timespan from
                // the maximum int size in seconds.
                var maxCacheLife = TimeSpan.FromSeconds(int.MaxValue);

                // Ensure that the cache compaction percentage has a sane value.
                if (this.options.CacheCompactionPercentage < 1 || this.options.CacheCompactionPercentage > 50)
                    this.options.CacheCompactionPercentage = 10;

                this.cachedIds = new MemoryCache(new MemoryCacheOptions
                {
                    // Evict 20% of cache entries on max size.
                    CompactionPercentage = this.options.CacheCompactionPercentage / 100,

                    // Convert the user supplied value (in MB) to bytes.
                    SizeLimit = Convert.ToInt64(this.options.MaxCacheSizeMB.Value) * 1048576,

                    // Disable expiration scan if MaxCacheLifetime has not been set.
                    ExpirationScanFrequency = this.options.MaxCacheLifetime.HasValue ? TimeSpan.FromMinutes(5) : maxCacheLife
                });

                // Set the cache duration property.
                if (this.options.MaxCacheLifetime.HasValue && this.options.MaxCacheLifetime < maxCacheLife)
                    this.cacheDuration = this.options.MaxCacheLifetime.Value;
                else
                    this.cacheDuration = maxCacheLife;

                // Populate the byte lookup array.
                this.sha = SHA1.Create();
                byteLookup = new uint[256];
                for (int i = 0; i < 256; i++)
                {
                    var s = i.ToString("X2");
                    byteLookup[i] = s[0] + ((uint)s[1] << 16);
                }
            }
        }

        /// <summary>
        /// Method to invoke the middleware. Called by the system.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> of the current request.</param>
        public async Task Invoke(HttpContext httpContext)
        {
            if (!httpContext.Request.Headers.TryGetValue(OidcDataHeader, out StringValues oidcData))
            {
                await BadRequest(httpContext, $"Missing header '{OidcDataHeader}'");
                return;
            }

            if (this.cacheIdentities)
            {
                // We're going to key the identities based on the SHA1 hash of the payload.
                // It would also be possible to parse the token and use the "Id" property,
                // but performance tests showed a huge difference (188 ticks for hash vs 65283 for JWT parsing).
                var hash = ByteArrayToHex(this.sha.ComputeHash(Encoding.UTF8.GetBytes(oidcData)));

                try
                {
                    httpContext.User = await this.cachedIds.GetOrCreateAsync(hash, (entry) => this.GetUser(new JsonWebToken(oidcData), entry));
                }
                catch (Exception ex)
                {
                    this.cachedIds.Remove(hash);
                    this.logger?.LogError($"{ex}");
                    await BadRequest(httpContext, $"Invalid data in header '{OidcDataHeader}'");
                    return;
                }
            }
            else
            {
                try
                {
                    httpContext.User = await this.GetUser(new JsonWebToken(oidcData), null);
                }
                catch (Exception ex)
                {
                    this.logger?.LogError($"{ex}");
                    await BadRequest(httpContext, $"Invalid data in header '{OidcDataHeader}': {ex.Message}");
                    return;
                }
            }

            // Invoke the next delegate in the pipeline
            await this.next.Invoke(httpContext);
        }

        private async Task<ClaimsPrincipal> GetUser(JsonWebToken jwt, ICacheEntry cacheEntry)
        {
            this.logger?.LogDebug("User Principal is '{0}' (issued by '{1}')", jwt.Subject, jwt.Issuer);

            if (this.options.ValidateTokenSignature)
            {
                if (!this.cachedValidationParameters.TryGetValue(jwt.Kid, out TokenValidationParameters validationParameters))
                {
                    var uri = string.Format(ALBPublicKeyUrlFormatString, this.region, jwt.Kid);
                    this.logger?.LogInformation("Retrieving ALB public key from '{0}'", uri);
                    var publicRsa = await InternalHttpClient.GetStringAsync(uri);

                    validationParameters = new TokenValidationParameters
                    {
                        RequireExpirationTime = true,
                        RequireSignedTokens = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = ConvertPemToSecurityKey(publicRsa),
                        ValidateAudience = false,
                        ValidateIssuer = false,
                        ValidateLifetime = this.options.ValidateTokenLifetime,
                        ClockSkew = TimeSpan.FromMinutes(2)
                    };

                    this.cachedValidationParameters.TryAdd(jwt.Kid, validationParameters);
                }

                var validationResult = this.tokenHandler.ValidateToken(jwt.EncodedToken, validationParameters);
                this.logger?.LogDebug("Token Validation result: {0}", validationResult.IsValid);

                if (!validationResult.IsValid)
                    throw validationResult.Exception;
            }

            if (cacheEntry != null)
            {
                cacheEntry.SetSize(jwt.EncodedPayload.Length)
                    .SetAbsoluteExpiration(this.cacheDuration);
            }

            var identity = new ClaimsIdentity(jwt.Claims, AuthenticationType, NameClaimType, this.options.RoleClaimType);
            return new ClaimsPrincipal(identity);
        }

        /// <summary>
        /// Converts a byte array to a hex string via lookup.
        /// Fastest method according to https://stackoverflow.com/a/624379
        /// </summary>
        private static string ByteArrayToHex(byte[] bytes)
        {
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = byteLookup[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }

            return new string(result);
        }

        private static async Task BadRequest(HttpContext httpContext, string message)
        {
            await httpContext.Response.WriteAsync(message);
            httpContext.Response.StatusCode = 400;
            httpContext.Response.Headers.Add(ContentTypeHeader, ContentTypeValue);
        }

        private static ECDsaSecurityKey ConvertPemToSecurityKey(string pem)
        {
            using (TextReader publicKeyTextReader = new StringReader(pem))
            {
                var ec = (ECPublicKeyParameters)new PemReader(publicKeyTextReader).ReadObject();
                var ecpar = new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint
                    {
                        X = ec.Q.XCoord.GetEncoded(),
                        Y = ec.Q.YCoord.GetEncoded()
                    }
                };

                return new ECDsaSecurityKey(ECDsa.Create(ecpar));
            }
        }
    }
}
