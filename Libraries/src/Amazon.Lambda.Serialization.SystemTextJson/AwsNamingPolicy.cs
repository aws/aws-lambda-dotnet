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

            return JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
