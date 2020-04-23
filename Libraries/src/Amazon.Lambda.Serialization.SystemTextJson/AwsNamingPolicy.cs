using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Amazon.Lambda.Serialization.SystemTextJson
{
    /// <summary>
    /// Custom AWS naming policy
    /// </summary>
    public class AwsNamingPolicy : JsonNamingPolicy
    {
        readonly IDictionary<string, string> _customNameMappings = new Dictionary<string, string>
            {
                {"XAmzId2", "x-amz-id-2" },
                {"XAmzRequestId", "x-amz-request-id" }
            };

        private readonly JsonNamingPolicy _fallbackNamingPolicy;

        /// <summary>
        /// Creates the AWS Naming policy. If the name matches one of the reserved AWS words it will return the
        /// appropriate mapping for it. Otherwise the name will be returned as is like the JsonDefaultNamingPolicy.
        /// </summary>
        public AwsNamingPolicy()
        {
            
        }

        /// <summary>
        /// Creates the AWS Naming policy. If the name matches one of the reserved AWS words it will return the
        /// appropriate mapping for it. Otherwise the JsonNamingPolicy passed in will be used to map the name.
        /// </summary>
        /// <param name="fallbackNamingPolicy"></param>
        public AwsNamingPolicy(JsonNamingPolicy fallbackNamingPolicy)
        {
            _fallbackNamingPolicy = fallbackNamingPolicy;
        }

        /// <summary>
        /// Map names that don't camel case.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override string ConvertName(string name)
        {
            if (_customNameMappings.TryGetValue(name, out var mapNamed))
            {
                return mapNamed;
            }

            // If no naming policy given then just return the name like the JsonDefaultNamingPolicy policy.
            // https://github.com/dotnet/runtime/blob/master/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/JsonDefaultNamingPolicy.cs
            return _fallbackNamingPolicy?.ConvertName(name) ?? name;
        }
    }
}
