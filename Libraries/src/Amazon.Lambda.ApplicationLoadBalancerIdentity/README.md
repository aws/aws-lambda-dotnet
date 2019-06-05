# Amazon.Lambda.ApplicationLoadBalancerIdentity

This package contains Middleware that can be used in conjunction with ALB's OpenId Connect integration to populate the "User" property in the RequestContext.

While it is in the Amazon.Lambda namespace, it can also be used in other ASP.NET Core deployment scenarios e.g. Fargate or EC2.

# Configuring the Middleware
## ALBIdentityMiddlewareOptions class
This is the class used to configure the middleware. It has the following properties:

#### MaxCacheSizeMB
Gets or sets the maximum memory (in MB) that the identity cache will use. If you do not set this property, in-memory caching will be disabled.

If you have set the VerifyTokenSignature property, enabling caching will usually reduce the overhead by double digit milliseconds on subsequent requests.

The size of each entry corresponds to the base64 encoded payload length in the JWT.
Since this data is provided by the userinfo endpoint, it could be of any size.
To calculate the *minimum* value for your traffic, identify your average userinfo payload length,
and multiply that by the expected number of concurrent users per [active] token lifetime interval.

For example:
Your average userinfo payload is 1.4KB, and you are expecting 10,000 concurrent users.
If a token is valid for 1 day, then:
1.4KB * 10,000 * 1 = 13.6MB minimum

If your concurrent users are rapidly cycling (e.g. 10,000 *different* users every 15 minutes for 1 hour each)
then you will want to alter the estimate to ensure that your cache is not evicting active entries.
1.4KB * 10,000 * (96 / 24) (15 min intervals in 1 day for 1 hour each) = 54.6MB minimum.

When the cache reaches it's maximum size, it will remove the oldest 20% of values, so it is
recommended you use a value at least 20% larger than the minimum value calculated above.
It does not allocate memory in advance, only when used, so if you have RAM to spare then estimate higher.

#### MaxCacheLifetime
Gets or sets the maximum amount of time an identity will remain in the in-memory cache.

If not specified, the default will be 24 days.

#### VerifyTokenSignature
Gets or sets a flag indicating whether tokens should be verified.
If this is specified, it is recommended that you enable identity caching by setting the "MaxCacheSizeMB" property.

#### RoleClaimType
Gets or sets the name of the role claim type, for use in the "IsInRole" method and [Authorize] attributes.

The default value is "group".

#### ValidateTokenLifetime
Gets or sets a flag indicating whether token lifetime should be validated.

The default value is true.


## Example Startup.cs

The middleware requires Logging services be registered to the service collection, as well as an
instance of the ALBIdentityMiddlewareOptions class (containing the configuration information).

The middleware needs to be registered before Mvc.

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
		services
			.AddLogging()
			.AddSingleton(new ALBIdentityMiddlewareOptions
			{
				MaxCacheSizeMB = 10,
				MaxCacheLifetime = TimeSpan.FromHours(1),
				VerifyTokenSignature = true
			})
			.AddMvc();
    }

	public void Configure(IApplicationBuilder app)
	{
		app
			.UseMiddleware<ALBIdentityMiddleware>()
			.UseMvc();
	}
}
```

